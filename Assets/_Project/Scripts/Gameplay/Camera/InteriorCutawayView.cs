using System.Collections.Generic;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Camera
{
    /// <summary>
    /// Hides whatever stands between the camera and the player, indoors.
    ///
    /// The rooms are the house model, which means four exterior walls, five partition
    /// walls and a room's worth of furniture, all of it taller than the character and
    /// all of it between the camera and them at some angle. Turning the camera to
    /// find a gap is not a solution: at the pitch the walls allow there is often no
    /// gap, and hunting for one is the player doing the camera's job.
    ///
    /// So the wall comes out instead. A cast from the camera to the player collects
    /// everything in the way and switches those renderers off; anything that was
    /// switched off and is no longer in the way goes back. The player is never one of
    /// them — they are the thing being looked at.
    ///
    /// A cutaway rather than a fade because these are single meshes: one wall is one
    /// renderer for a whole side of the house, so there is no "part near the player"
    /// to dissolve without a shader that knows where the player is. Removing the
    /// whole panel is what the top-down games this borrows from do, and it reads
    /// cleanly at this camera distance.
    ///
    /// Per screen. Nothing in the simulation reads it, so the two players can have
    /// different walls missing, and neither gains anything: you can only see into the
    /// room you are already standing in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorCutawayView : MonoBehaviour
    {
        /// <summary>
        /// A fat cast, not a line. A hairline ray slips between a door frame and a
        /// wall and the panel flickers back once a frame, which is worse to look at
        /// than the wall was.
        /// </summary>
        [SerializeField, Min(0.05f)]
        private float castRadius = 0.55f;

        /// <summary>
        /// Aimed a little above the feet. Casting at the pivot clips the floor the
        /// player is standing on and hides it.
        /// </summary>
        [SerializeField]
        private Vector3 targetOffset = new(0f, 1.1f, 0f);

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        private const int MaxHits = 32;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly HashSet<InteriorOccluder> _hidden = new();
        private readonly HashSet<InteriorOccluder> _stillBlocking = new();
        private Transform _followed;
        private PlayerInteriorState _state;

        /// <summary>
        /// How many panels are currently out of the way. Exposed because "the wall
        /// disappeared" is not something a screenshot test can assert, and a count
        /// is.
        /// </summary>
        public int HiddenCount => _hidden.Count;

        public void Configure(
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// The local player's own state, resolved every frame until found: which role
        /// this machine controls is handed out by the host after the lobby, so
        /// binding it at scene-build time would watch the wrong character.
        /// </summary>
        private PlayerInteriorState ResolveLocalState()
        {
            if (_state != null)
            {
                return _state;
            }

            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (!assigned.HasValue)
            {
                if (roleSelector == null)
                {
                    return null;
                }

                assigned = roleSelector.ActiveRole;
            }

            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role != assigned.Value)
                {
                    continue;
                }

                _state = candidate.GetComponent<PlayerInteriorState>();
                _followed = candidate.transform;
                return _state;
            }

            return null;
        }

        private void RevealEverything()
        {
            foreach (InteriorOccluder occluder in _hidden)
            {
                if (occluder != null)
                {
                    occluder.SetHidden(false);
                }
            }

            _hidden.Clear();
        }

        public void Tick()
        {
            PlayerInteriorState state = ResolveLocalState();
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (state == null
                || !state.IsIndoors
                || _followed == null
                || view == null)
            {
                RevealEverything();
                return;
            }

            Vector3 target = _followed.position + targetOffset;
            Vector3 from = view.transform.position;
            Vector3 toTarget = target - from;
            float distance = toTarget.magnitude;
            if (distance <= 0.01f)
            {
                RevealEverything();
                return;
            }

            _stillBlocking.Clear();

            // Triggers ignored, as everywhere that asks "what is in the way" — the
            // map is full of them and a doorway trigger is not a wall.
            int count = Physics.SphereCastNonAlloc(
                from,
                castRadius,
                toTarget / distance,
                _hits,
                distance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider hit = _hits[index].collider;
                if (hit == null)
                {
                    continue;
                }

                InteriorOccluder occluder =
                    hit.GetComponentInParent<InteriorOccluder>();
                if (occluder == null)
                {
                    continue;
                }

                _stillBlocking.Add(occluder);
                occluder.SetHidden(true);
                _hidden.Add(occluder);
            }

            // Put back anything that has stopped blocking. Iterated over a copy,
            // because the set is being written to.
            if (_hidden.Count == _stillBlocking.Count)
            {
                return;
            }

            var toReveal = new List<InteriorOccluder>();
            foreach (InteriorOccluder occluder in _hidden)
            {
                if (occluder == null || !_stillBlocking.Contains(occluder))
                {
                    toReveal.Add(occluder);
                }
            }

            foreach (InteriorOccluder occluder in toReveal)
            {
                occluder?.SetHidden(false);
                _hidden.Remove(occluder);
            }
        }

        private void OnDisable()
        {
            RevealEverything();
        }

        private void LateUpdate()
        {
            Tick();
        }
    }
}
