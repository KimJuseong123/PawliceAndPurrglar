using System.Collections.Generic;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// A wifi-style pulse on the officer's screen, pointing at the sensor that
    /// tripped and sized by how far away it is.
    ///
    /// A line of text at the bottom of the screen was not enough. It says
    /// something happened without saying where, and in the middle of a chase the
    /// officer is looking at their own character, not at a caption. This puts the
    /// answer in the direction they have to turn.
    ///
    /// Three arcs stacked like signal bars, rotated to face the sensor from the
    /// officer's position, and scaled by distance — close is big and loud, far is
    /// small. That is the whole reading: which way, and how far.
    ///
    /// Drawn as a ring of UI images rather than a world object so it cannot be
    /// hidden behind a building, which is exactly the case the caption failed at.
    ///
    /// Read only, and on the officer's screen alone. It reports a reveal the
    /// visibility rule already decided.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensorRadarPresenter : MonoBehaviour
    {
        [SerializeField]
        private RectTransform root;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        /// <summary>
        /// How far away a sensor has to be to draw at its smallest. Beyond the
        /// torch's own reach, so a distant trip still reads as distant rather
        /// than clamping to the same size as one underfoot.
        /// </summary>
        [SerializeField, Min(1f)]
        private float farDistance = 30f;

        [SerializeField, Min(0.1f)]
        private float nearScale = 2.1f;

        [SerializeField, Min(0.1f)]
        private float farScale = 0.95f;

        private readonly List<Image> _bars = new();
        private FlashlightVisibility _visibility;
        private PlayerRoleIdentity _officer;
        private float _pulse;

        public bool IsShowing { get; private set; }
        public float CurrentScale { get; private set; }
        public float CurrentAngle { get; private set; }

        public void Configure(
            RectTransform configuredRoot,
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            root = configuredRoot;
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// Registers the arcs the scene builder created, outermost last.
        /// </summary>
        public void AddBar(Image bar)
        {
            if (bar != null)
            {
                _bars.Add(bar);
            }
        }

        private bool ViewerIsPolice()
        {
            PlayerRole? assigned = LocalPlayerRoleSelector.OverriddenRole;
            if (assigned.HasValue)
            {
                return assigned.Value == PlayerRole.Police;
            }

            return roleSelector != null
                && roleSelector.ActiveRole == PlayerRole.Police;
        }

        private FlashlightVisibility ResolveVisibility()
        {
            if (_visibility == null)
            {
                _visibility =
                    FindFirstObjectByType<FlashlightVisibility>();
            }

            return _visibility;
        }

        private PlayerRoleIdentity ResolveOfficer()
        {
            if (_officer != null)
            {
                return _officer;
            }

            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role == PlayerRole.Police)
                {
                    _officer = candidate;
                    break;
                }
            }

            return _officer;
        }

        public void Refresh(float deltaTime)
        {
            if (root == null)
            {
                return;
            }

            FlashlightVisibility visibility = ResolveVisibility();
            PlayerRoleIdentity officer = ResolveOfficer();
            bool showing = ViewerIsPolice()
                && officer != null
                && visibility?.IsRevealed == true;

            if (showing != IsShowing)
            {
                IsShowing = showing;
                root.gameObject.SetActive(showing);
            }

            if (!showing)
            {
                _pulse = 0f;
                return;
            }

            // The remembered source, not a search of the world. The sensor
            // that fired is removed shortly afterwards, so looking for one still
            // flashing found nothing and the officer got no direction at all —
            // which is what "the sensor does not detect" actually was.
            Vector3 delta =
                visibility.RevealSource - officer.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            // Screen-space direction from the officer to the sensor. Taken from
            // the world rather than the camera so it stays right regardless of
            // where the camera happens to be looking; the camera never turns.
            CurrentAngle =
                -Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            root.localRotation =
                Quaternion.Euler(0f, 0f, CurrentAngle);

            CurrentScale = Mathf.Lerp(
                nearScale,
                farScale,
                Mathf.Clamp01(distance / farDistance));
            root.localScale = Vector3.one * CurrentScale;

            // Arcs light in turn, outward, so it reads as a signal arriving
            // rather than a static badge.
            _pulse += deltaTime * 2.2f;
            for (int index = 0; index < _bars.Count; index++)
            {
                if (_bars[index] == null)
                {
                    continue;
                }

                float phase = Mathf.Repeat(
                    _pulse - index * 0.22f,
                    1f);
                Color color = _bars[index].color;
                // Fully opaque at the leading edge. A faint pulse in the middle
                // of a night chase is not something anybody notices.
                color.a = Mathf.Lerp(0.25f, 1f, 1f - phase);
                _bars[index].color = color;
            }
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }
    }
}
