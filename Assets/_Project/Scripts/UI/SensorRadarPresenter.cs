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
    /// lit standing in for how far away it is: one arc when it is right in front,
    /// one more per five metres. Reading a count is faster than judging a size.
    ///
    /// Deliberately the opposite way round to a phone's signal meter. These arcs
    /// are the distance the signal had to cross, not its strength — standing on
    /// top of the sensor needs no fan at all, and a trip across the map should
    /// look like a long way off.
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
        /// Metres per arc.
        ///
        /// Distance is read as a count, and the count grows with distance: one arc
        /// when the sensor is right in front, more the further away it is. That is
        /// the opposite of a phone's signal meter and it is the right way round
        /// here — the arcs are the distance the signal had to travel, not its
        /// strength. Right on top of it needs no fan at all.
        ///
        /// Five metres per arc puts a sensor two blocks away at three or four,
        /// and one at arm's length at one.
        /// </summary>
        [SerializeField, Min(1f)]
        private float metresPerBar = 5f;

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
        /// Finds the arcs, innermost first.
        ///
        /// Collected here rather than handed over by the scene builder. A list
        /// filled at edit time is not serialised, so in the built game it was
        /// empty: nothing was ever switched on or off and the count did nothing at
        /// all. That is the same trap that killed the lobby buttons and the leg
        /// animator, and the third time it has cost a playtest.
        /// </summary>
        private void CollectBars()
        {
            if (_bars.Count > 0 || root == null)
            {
                return;
            }

            _bars.AddRange(
                root.GetComponentsInChildren<SensorArcGraphic>(true));
            _bars.Sort((left, right) =>
                left.InnerRadius.CompareTo(right.InnerRadius));
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

            CollectBars();
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

            // A screen bearing, which is the world bearing turned by however far
            // the camera is facing.
            //
            // The camera used to never turn, so the two were the same and this read
            // the world directly. The interior view does turn, and a fixed world
            // bearing would point the arrows at a wall the moment the officer
            // looked somewhere else. Subtracting the camera's yaw is correct in
            // both cases: outdoors it is a constant and nothing changes.
            float cameraYaw = UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main.transform.eulerAngles.y
                : 0f;
            CurrentAngle = -Mathf.DeltaAngle(
                cameraYaw,
                Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg);
            root.localRotation =
                Quaternion.Euler(0f, 0f, CurrentAngle);

            // Distance as a count: one arc per five metres, at least one.
            LitBarCount = Mathf.Clamp(
                1 + Mathf.RoundToInt(distance / metresPerBar),
                1,
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
