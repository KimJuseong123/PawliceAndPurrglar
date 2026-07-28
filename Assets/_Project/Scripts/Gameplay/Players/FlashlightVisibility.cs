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
            if (seconds <= 0f)
            {
                return;
            }

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
                _targetRenderers =
                    candidate.GetComponentsInChildren<Renderer>(true);
                return true;
            }

            return false;
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
                // A deliberate exception, not an oversight. Stars only appear on
                // a player who was just hit, so the thrower already knew roughly
                // where they were — and hiding the one confirmation that a throw
                // in the dark landed would make throwing at night pointless. It
                // is also stated here rather than left to chance: the stars are
                // built at runtime, so whether they ended up in this cached list
                // was previously a matter of which component ran first.
                if (renderer.GetComponentInParent<
                        PawsAndLoot.Animation.StunStarsView>() != null)
                {
                    continue;
                }

                renderer.enabled = visible;
            }
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
