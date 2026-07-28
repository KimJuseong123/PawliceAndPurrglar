using System.Collections.Generic;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// A wifi-style pulse on the officer's screen, pointing at the sensor that
    /// tripped.
    ///
    /// A line of text at the bottom of the screen was not enough. It says
    /// something happened without saying where, and in the middle of a chase the
    /// officer is looking at their own character, not at a caption.
    ///
    /// Concentric arcs rotated toward the sensor, with <b>how many</b> of them
    /// lit standing in for how close it is: four at the far edge of the map, one
    /// more per band as it gets nearer. Reading a count is faster than judging a
    /// size, which is why signal meters have used bars rather than one growing
    /// blob for forty years.
    ///
    /// The first version scaled one shape built from plain rects. At a size big
    /// enough to notice, three rectangles read as three fat bars rather than a
    /// signal — hence real arcs, and hence a count instead of a scale.
    ///
    /// Red, because it is an alarm. The torch, the stun stars and the ground
    /// wedge are all yellow, and a fourth yellow thing on a night screen is just
    /// one more yellow thing.
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
        /// Distance at which only the base arcs light. Beyond the torch's own
        /// reach, so a distant trip still reads as distant.
        /// </summary>
        [SerializeField, Min(1f)]
        private float farDistance = 30f;

        /// <summary>
        /// Arcs lit at that far edge. Fewer than four does not read as a signal
        /// meter; the extras above it are what closeness adds.
        /// </summary>
        [SerializeField, Min(1)]
        private int farBarCount = 4;

        private readonly List<SensorArcGraphic> _bars = new();
        private FlashlightVisibility _visibility;
        private PlayerRoleIdentity _officer;
        private float _pulse;

        public bool IsShowing { get; private set; }
        public float CurrentAngle { get; private set; }

        /// <summary>
        /// How many arcs are lit. Exposed so a test can assert that closer means
        /// more, which is the entire reading.
        /// </summary>
        public int LitBarCount { get; private set; }

        public void Configure(
            RectTransform configuredRoot,
            LocalPlayerRoleSelector configuredRoleSelector)
        {
            root = configuredRoot;
            roleSelector = configuredRoleSelector;
        }

        /// <summary>
        /// Registers the arcs the scene builder created, innermost first.
        /// </summary>
        public void AddBar(SensorArcGraphic bar)
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
                && visibility != null
                && visibility.IsRevealed;

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

            // The remembered source, not a search of the world. The sensor that
            // fired is removed shortly afterwards, so looking for one still
            // flashing found nothing and the officer got no direction at all —
            // which is what "the sensor does not detect" actually was.
            Vector3 delta =
                visibility.RevealSource - officer.transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;

            // Taken from the world rather than the camera, because the camera
            // never turns and a world bearing is therefore also a screen bearing.
            CurrentAngle =
                -Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            root.localRotation =
                Quaternion.Euler(0f, 0f, CurrentAngle);

            // Closeness as a count.
            float closeness =
                1f - Mathf.Clamp01(distance / farDistance);
            LitBarCount = Mathf.Clamp(
                farBarCount + Mathf.FloorToInt(
                    closeness * (_bars.Count - farBarCount + 1)),
                farBarCount,
                _bars.Count);

            // A wave travelling outward from the middle, one arc at a time, so it
            // reads as a signal arriving rather than a badge sitting there.
            _pulse += deltaTime * 1.9f;
            float head = Mathf.Repeat(_pulse, 1f) * LitBarCount;
            for (int index = 0; index < _bars.Count; index++)
            {
                if (_bars[index] == null)
                {
                    continue;
                }

                bool lit = index < LitBarCount;
                if (_bars[index].enabled != lit)
                {
                    _bars[index].enabled = lit;
                }

                if (!lit)
                {
                    continue;
                }

                // Brightest as the wave passes, never fully dark, so the shape
                // stays readable between pulses.
                float behind = Mathf.Repeat(head - index, LitBarCount);
                float glow = Mathf.Clamp01(1f - behind * 0.8f);
                Color color = _bars[index].color;
                color.a = Mathf.Lerp(0.35f, 1f, glow);
                _bars[index].color = color;
            }
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }
    }
}
