using System.Collections.Generic;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Camera
{
    /// <summary>
    /// Takes away the one wall the camera is looking through, indoors.
    ///
    /// It used to cast from the camera to the player and remove whatever the ray
    /// touched. That worked and was tiring to look at: several walls qualify at any
    /// angle, which ones qualify changes continuously as the view turns, and the
    /// result was walls appearing and disappearing while the player stood still.
    ///
    /// So the choice is made by side instead. A room has four exterior walls, the
    /// camera is on one side of it, and that side is the one in the way — the whole
    /// panel goes, and it stays gone until the camera has moved round far enough for a
    /// different side to be the obvious answer. Four possible states instead of a
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

        private readonly List<InteriorOccluder> _panels = new();
        private HouseInterior _room;
        private InteriorOccluder _removed;
        private Transform _followed;
        private PlayerInteriorState _state;

        /// <summary>
        /// How many panels are out of the way. One indoors, none outside — a count is
        /// something a test can read, and "the wall disappeared" is not.
        /// </summary>
        public int HiddenCount => _removed != null ? 1 : 0;

        /// <summary>
        /// Which panel it is, so a test can check it is the one between the camera and
        /// the player rather than just any of the four.
        /// </summary>
        public InteriorOccluder RemovedPanel => _removed;

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
        /// The room the player is in, and its four walls.
        ///
        /// Collected from the room rather than handed over, and identified by being
        /// the only things in it that carry an occluder: the partitions are greybox
        /// now and take none, so whatever is left is the shell. No names involved,
        /// which is the point — a name list is what fails silently when a model part
        /// is renamed.
        /// </summary>
        private void ResolveRoom(int interiorId)
        {
            if (_room != null && _room.InteriorId == interiorId)
            {
                return;
            }

            _room = null;
            _panels.Clear();
            foreach (HouseInterior candidate in
                FindObjectsByType<HouseInterior>(FindObjectsSortMode.None))
            {
                if (candidate.InteriorId != interiorId)
                {
                    continue;
                }

                _room = candidate;
                _panels.AddRange(
                    candidate.GetComponentsInChildren<InteriorOccluder>(
                        true));
                break;
            }
        }

        private void PutEverythingBack()
        {
            foreach (InteriorOccluder panel in _panels)
            {
                if (panel != null)
                {
                    panel.SetHidden(false);
                }
            }

            _removed = null;
        }

        /// <summary>
        /// How much a panel is on the camera's side of the room. A wall the camera is
        /// behind scores near 1; the opposite wall scores near -1.
        /// </summary>
        private float FacingScore(
            InteriorOccluder panel,
            Vector3 towardCamera)
        {
            if (panel == null || panel.View == null || _room == null)
            {
                return -2f;
            }

            Vector3 outward =
                panel.View.bounds.center - _room.transform.position;
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
                _panels.Clear();
                return;
            }

            ResolveRoom(state.CurrentInteriorId);
            if (_room == null || _panels.Count == 0)
            {
                return;
            }

            Vector3 towardCamera =
                view.transform.position - _room.transform.position;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude <= 0.01f)
            {
                return;
            }

            towardCamera.Normalize();

            InteriorOccluder best = null;
            float bestScore = -2f;
            foreach (InteriorOccluder panel in _panels)
            {
                float score = FacingScore(panel, towardCamera);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = panel;
                }
            }

            // Held on to unless something is clearly better. A camera sitting on a
            // corner scores two walls almost equally, and without this it would
            // alternate between them from one frame to the next.
            if (_removed != null && _removed != best)
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
            best?.SetHidden(true);
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
