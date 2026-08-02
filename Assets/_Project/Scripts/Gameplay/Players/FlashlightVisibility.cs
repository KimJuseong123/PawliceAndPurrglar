using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// Hides the thief from the police unless the torch is on them.
    ///
    /// This is what makes the night mean something: a fast officer who cannot
    /// see has to use the dog, the alarms and the noise, which is the whole
    /// reason those systems exist.
    ///
    /// Only renderers are switched. The world is not darkened and geometry is
    /// never hidden — the fixed overhead camera needs the town visible to be
    /// playable at all, and fog of war over 3D geometry is a rendering feature
    /// this does not need. Hiding the one thing that matters gets the "he was
    /// right there" moment for the cost of toggling a renderer.
    ///
    /// Presentation only, and deliberately local. It runs on the police's own
    /// machine and changes nothing the host simulates: the thief still moves,
    /// still carries loot and can still be arrested while invisible. If this
    /// component were removed the match would play out identically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlashlightVisibility : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity viewer;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        /// <summary>
        /// Taken from <see cref="FlashlightCone"/>, which is also what the light
        /// and the outline drawn on the ground use. They were separate numbers
        /// and had already drifted: the rule opened 30° while the light opened
        /// 23°, so the thief appeared in a band that was never lit.
        /// </summary>
        [SerializeField, Range(10f, 90f)]
        private float halfAngleDegrees =
            FlashlightCone.HalfAngleDegrees;

        [SerializeField, Min(1f)]
        private float rangeMeters = FlashlightCone.RangeMeters;

        /// <summary>
        /// Always visible within this distance regardless of facing. Somebody
        /// close enough to arrest must never be invisible, or the police is
        /// grappling with thin air.
        /// </summary>
        [SerializeField, Min(0f)]
        private float alwaysSeenRadius =
            FlashlightCone.AlwaysSeenRadius;

        private IMatchStateReader _matchState;
        private PlayerRoleIdentity _target;
        private Renderer[] _targetRenderers;
        private float _revealUntil;

        public bool IsTargetVisible { get; private set; } = true;
        public bool IsRevealed => Time.time < _revealUntil;

        /// <summary>
        /// Where the reveal came from, in world space.
        ///
        /// Remembered here rather than looked up, because the sensor that tripped
        /// is a runtime object the host removes once it has fired — asking the
        /// world "which sensor is flashing" found nothing, so the officer got no
        /// direction at all.
        /// </summary>
        public Vector3 RevealSource { get; private set; }

        /// <summary>
        /// Shows the other player regardless of the cone for a while.
        ///
        /// This is the sensor light's whole payoff: the officer does not get told
        /// where the thief is, they get to see them. A marker on the edge of the
        /// screen would be the same information delivered worse — and at night,
        /// simply being visible is the strongest thing that can happen to
        /// somebody who is relying on not being.
        /// </summary>
        public void RevealFor(float seconds)
        {
            RevealFor(seconds, RevealSource);
        }

        public void RevealFor(float seconds, Vector3 source)
        {
            if (seconds <= 0f)
            {
                return;
            }

            RevealSource = source;
            _revealUntil = Mathf.Max(
                _revealUntil,
                Time.time + seconds);
        }

        public void Configure(
            PlayerRoleIdentity configuredViewer,
            IMatchStateReader configuredMatchState)
        {
            viewer = configuredViewer;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        /// <summary>
        /// Finds the opposing player and everything drawn for them, including
        /// their companion, at runtime. The role each machine controls is handed
        /// out by the host after the lobby, so it cannot be bound in the editor.
        /// </summary>
        private bool ResolveTarget()
        {
            if (_target != null && _targetRenderers != null)
            {
                return true;
            }

            if (viewer == null)
            {
                return false;
            }

            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role == viewer.Role)
                {
                    continue;
                }

                _target = candidate;
                _targetRenderers = CollectRenderers(candidate);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Everything drawn for the opposing side: the player, and the animal
        /// that follows them.
        ///
        /// The animal is not a child of the player. It is parented to the
        /// town's own Companions node so it can be left behind, sent ahead and
        /// lured away, none of which works from inside somebody's hierarchy.
        /// Asking the player for its children therefore returned the player and
        /// nothing else — and the cat stayed lit in the dark, twelve metres
        /// outside the torch, pointing at exactly where the thief was.
        ///
        /// Matched by owner rather than by name or by tag. An animal knows
        /// whose it is; a name is a thing somebody renames.
        /// </summary>
        private static Renderer[] CollectRenderers(PlayerRoleIdentity target)
        {
            var found = new System.Collections.Generic.List<Renderer>(
                target.GetComponentsInChildren<Renderer>(true));

            foreach (PawsAndLoot.Companions.CompanionAgent animal in
                FindObjectsByType<PawsAndLoot.Companions.CompanionAgent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                if (!BelongsTo(animal, target))
                {
                    continue;
                }

                found.AddRange(
                    animal.GetComponentsInChildren<Renderer>(true));
            }

            return found.ToArray();
        }

        /// <summary>
        /// Whether an animal follows the given player.
        ///
        /// Asked of the animal's own owner reference when it has one, and
        /// otherwise settled by role: the dog is the officer's and the cat is
        /// the thief's, which is a rule of the game rather than of the scene.
        /// </summary>
        private static bool BelongsTo(
            PawsAndLoot.Companions.CompanionAgent animal,
            PlayerRoleIdentity target)
        {
            PlayerRoleIdentity owner =
                animal.GetComponentInParent<PlayerRoleIdentity>();
            if (owner != null)
            {
                return owner == target;
            }

            return animal.CompanionKind
                == PawsAndLoot.Companions.CompanionKind.Cat
                ? target.Role == PlayerRole.Thief
                : target.Role == PlayerRole.Police;
        }

        private void SetVisible(bool visible)
        {
            if (IsTargetVisible == visible || _targetRenderers == null)
            {
                return;
            }

            IsTargetVisible = visible;
            foreach (Renderer renderer in _targetRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                // Line renderers are route debug draws, not the character.
                if (renderer is LineRenderer)
                {
                    continue;
                }

                // The stun stars stay up even on somebody outside the beam.
                //
                // A deliberate exception: stars only appear on a player who was
                // just hit, so the thrower already knew roughly where they were,
                // and hiding the one confirmation that a throw in the dark landed
                // would make throwing at night pointless.
                //
                // Tested against the ring itself, not against "does an ancestor
                // have a StunStarsView". That component sits on the player root,
                // so the looser question is true of every renderer on the
                // character — which exempted the entire thief and left them
                // visible from any direction. The whole feature quietly stopped
                // working and nothing failed.
                if (IsStunStar(renderer))
                {
                    continue;
                }

                renderer.enabled = visible;
            }
        }

        /// <summary>
        /// True for the star meshes themselves and nothing else on the character.
        /// </summary>
        private static bool IsStunStar(Renderer renderer)
        {
            PawsAndLoot.Animation.StunStarsView stars =
                renderer.GetComponentInParent<
                    PawsAndLoot.Animation.StunStarsView>();
            return stars != null
                && stars.RingRoot != null
                && renderer.transform.IsChildOf(stars.RingRoot);
        }

        /// <summary>
        /// True only on the machine actually playing the viewer's role.
        ///
        /// Without this check the thief's own machine would run the police's
        /// cone and hide the thief from themselves. Visibility is a per-screen
        /// decision, so it has to be gated on whose screen this is.
        /// </summary>
        private bool ViewerIsLocal()
        {
            if (viewer == null)
            {
                return false;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == viewer.Role;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null
                && selector.ActiveRole == viewer.Role;
        }

        private void LateUpdate()
        {
            if (!ViewerIsLocal())
            {
                SetVisible(true);
                return;
            }

            // Outside a match nothing is hidden, so the lobby, the countdown and
            // the result screen all show both characters.
            if (ResolveMatchState()?.IsGameplayActive != true)
            {
                SetVisible(true);
                return;
            }

            if (!ResolveTarget())
            {
                return;
            }

            // A tripped sensor overrides the cone entirely, including range. The
            // point is to catch somebody who thought they were away.
            if (IsRevealed)
            {
                SetVisible(true);
                return;
            }

            Vector3 delta = _target.transform.position
                - viewer.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            if (distance <= alwaysSeenRadius)
            {
                SetVisible(true);
                return;
            }

            if (distance > rangeMeters)
            {
                SetVisible(false);
                return;
            }

            Vector3 facing = viewer.transform.forward;
            facing.y = 0f;
            float angle = Vector3.Angle(facing, delta);
            SetVisible(angle <= halfAngleDegrees);
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }

        private void OnDisable()
        {
            // Never leave the other player invisible because this was switched
            // off mid-match.
            SetVisible(true);
        }
    }
}
