using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Camera
{
    /// <summary>
    /// Takes away the one side of the room the camera is looking through, indoors.
    ///
    /// It used to cast from the camera to the player and remove whatever the ray
    /// touched. That worked and was tiring to look at: several walls qualify at any
    /// angle, which ones qualify changes continuously as the view turns, and the
    /// result was walls appearing and disappearing while the player stood still.
    ///
    /// So the choice is made by side instead. A room has four faces, the camera is on
    /// one side of it, and that side is the one in the way — the whole face goes, wall
    /// and windows and siding and shutters together, and it stays gone until the
    /// camera has moved round far enough for a different side to be the obvious
    /// answer. Removing only the wall panel was the first attempt and left the player
    /// behind a cage of window frames. Four possible states instead of a
    /// per-frame answer, and the partitions no longer matter at all because they are
    /// rebuilt at 2 m and nothing has to be done about them.
    ///
    /// The margin is what makes it calm. Without it, a camera parked on a diagonal
    /// swaps between two walls every few frames, which is the flicker this replaced.
    ///
    /// Per screen. Nothing in the simulation reads it, so the two players can have
    /// different walls missing and neither gains anything: you can only see into the
    /// room you are already standing in.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorCutawayView : MonoBehaviour
    {
        /// <summary>
        /// How much better a different side has to look before the view switches to
        /// it. Compared against a dot product, so this is roughly 20 degrees of
        /// camera rotation past the halfway line.
        /// </summary>
        [SerializeField, Range(0f, 0.5f)]
        private float switchMargin = 0.12f;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        private HouseInterior _room;
        private InteriorShellScreen _screen;
        private int _removed = -1;
        private Transform _followed;
        private PlayerInteriorState _state;

        /// <summary>
        /// How many panels are out of the way. One indoors, none outside — a count is
        /// something a test can read, and "the wall disappeared" is not.
        /// </summary>
        public int HiddenCount => _removed >= 0 ? 1 : 0;

        /// <summary>
        /// Which face it is, so a test can check it is the one between the camera and
        /// the player rather than just any of the four.
        /// </summary>
        public int RemovedFace => _removed;

        /// <summary>
        /// The room being looked into, exposed for the same reason.
        /// </summary>
        public InteriorShellScreen Screen => _screen;

        public void Configure(
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// The local player's own state, resolved every frame until found: which role
        /// this machine controls is handed out by the host after the lobby, so binding
        /// it at scene-build time would watch the wrong character.
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

        /// <summary>
        /// The room the player is in and its shell, found by id rather than kept from
        /// the last frame — the player may have walked into a different house.
        /// </summary>
        private void ResolveRoom(int interiorId)
        {
            if (_room != null && _room.InteriorId == interiorId)
            {
                return;
            }

            _room = null;
            _screen = null;
            foreach (HouseInterior candidate in
                FindObjectsByType<HouseInterior>(FindObjectsSortMode.None))
            {
                if (candidate.InteriorId != interiorId)
                {
                    continue;
                }

                _room = candidate;
                _screen = candidate.GetComponent<InteriorShellScreen>();
                break;
            }
        }

        private void PutEverythingBack()
        {
            if (_screen != null)
            {
                _screen.ShowEverything();
            }

            _removed = -1;
        }

        /// <summary>
        /// How much a face is on the camera's side of the room. The face the camera is
        /// behind scores near 1; the opposite one near -1.
        /// </summary>
        private float FacingScore(int face, Vector3 towardCamera)
        {
            if (_screen == null || _screen.PartsOn(face) == 0)
            {
                return -2f;
            }

            Vector3 outward =
                _screen.CentreOf(face) - _screen.Centre;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.01f
                ? Vector3.Dot(outward.normalized, towardCamera)
                : -2f;
        }

        public void Tick()
        {
            PlayerInteriorState state = ResolveLocalState();
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (state == null || !state.IsIndoors || view == null)
            {
                PutEverythingBack();
                _room = null;
                _screen = null;
                return;
            }

            ResolveRoom(state.CurrentInteriorId);
            if (_room == null || _screen == null)
            {
                return;
            }

            // From the middle of the room, which is not this object's origin: the
            // model is re-centred on a silhouette that includes its porch.
            Vector3 towardCamera =
                view.transform.position - _screen.Centre;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude <= 0.01f)
            {
                return;
            }

            towardCamera.Normalize();

            int best = -1;
            float bestScore = -2f;
            for (int face = 0; face < _screen.FaceCount; face++)
            {
                float score = FacingScore(face, towardCamera);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = face;
                }
            }

            // Held on to unless something is clearly better. A camera sitting on a
            // corner scores two faces almost equally, and without this it would
            // alternate between them from one frame to the next.
            if (_removed >= 0 && _removed != best)
            {
                float held = FacingScore(_removed, towardCamera);
                if (bestScore - held < switchMargin)
                {
                    return;
                }
            }

            if (best == _removed)
            {
                return;
            }

            PutEverythingBack();
            if (best >= 0)
            {
                _screen.SetHidden(best, true);
            }

            _removed = best;
        }

        private void OnDisable()
        {
            PutEverythingBack();
        }

        private void LateUpdate()
        {
            Tick();
        }
    }
}
