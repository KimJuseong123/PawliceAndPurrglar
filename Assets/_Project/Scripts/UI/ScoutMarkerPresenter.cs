using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// Shows the thief where the cat's scout found loot, and where it saw police.
    ///
    /// The command already searched and already reported what it found — the
    /// resolver has been producing "보물과 경찰" since CAT-003 — but never said
    /// where, so the useful half of the answer was thrown away. This draws it.
    ///
    /// Marks fade after a few seconds. Scouting is meant to be a snapshot the
    /// thief acts on, not a permanent overlay that turns the map into a
    /// solved puzzle.
    ///
    /// Read only, and on the thief's screen only. Clamped to the screen edge
    /// when off-camera, the same way the police's distraction alert is, so the
    /// two directional cues behave identically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoutMarkerPresenter : MonoBehaviour
    {
        [SerializeField]
        private CompanionCommandResolver resolver;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private RectTransform lootMarker;

        [SerializeField]
        private RectTransform policeMarker;

        [SerializeField]
        private Text reportLabel;

        [SerializeField, Min(0.5f)]
        private float showSeconds = 6f;

        public bool IsShowing { get; private set; }

        public void Configure(
            CompanionCommandResolver configuredResolver,
            LocalPlayerRoleSelector configuredRoleSelector,
            Camera configuredCamera,
            RectTransform configuredLootMarker,
            RectTransform configuredPoliceMarker,
            Text configuredReportLabel)
        {
            resolver = configuredResolver;
            roleSelector = configuredRoleSelector;
            worldCamera = configuredCamera;
            lootMarker = configuredLootMarker;
            policeMarker = configuredPoliceMarker;
            reportLabel = configuredReportLabel;
        }

        /// <summary>
        /// Finds the cat's resolver at runtime. Only the cat scouts, and the
        /// companions are built elsewhere in the scene builder.
        /// </summary>
        private void ResolveResolver()
        {
            if (resolver != null)
            {
                return;
            }

            foreach (CompanionAgent agent in
                FindObjectsByType<CompanionAgent>(
                    FindObjectsSortMode.None))
            {
                if (agent.CompanionKind != CompanionKind.Cat)
                {
                    continue;
                }

                resolver = agent.GetComponent<
                    CompanionCommandResolver>();
                return;
            }
        }

        public void Refresh()
        {
            ResolveResolver();
            bool isThief = roleSelector != null
                && ResolveLocalRole() == PlayerRole.Thief;
            bool fresh = resolver != null
                && resolver.LastScoutAtSeconds >= 0f
                && Time.time - resolver.LastScoutAtSeconds
                   <= showSeconds;

            IsShowing = isThief && fresh;
            if (!IsShowing)
            {
                SetActive(lootMarker, false);
                SetActive(policeMarker, false);
                if (reportLabel != null)
                {
                    reportLabel.text = string.Empty;
                }

                return;
            }

            if (reportLabel != null)
            {
                reportLabel.text =
                    string.IsNullOrEmpty(resolver.LastScoutReport)
                        ? "고양이가 아무것도 못 찾았다"
                        : $"고양이가 찾았다: {resolver.LastScoutReport}";
            }

            Vector3? loot = resolver.LastScoutLootPosition;
            SetActive(lootMarker, loot.HasValue);
            if (loot.HasValue)
            {
                PlaceMarker(lootMarker, loot.Value);
            }

            Vector3? police = resolver.LastScoutPolicePosition;
            SetActive(policeMarker, police.HasValue);
            if (police.HasValue)
            {
                PlaceMarker(policeMarker, police.Value);
            }
        }

        private PlayerRole ResolveLocalRole()
        {
            return LocalPlayerRoleSelector.OverriddenRole
                ?? roleSelector.ActiveRole;
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect != null && rect.gameObject.activeSelf != active)
            {
                rect.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Same projection and edge clamp as the police's distraction marker, so
        /// an off-screen cue points the same way for both roles.
        /// </summary>
        private void PlaceMarker(
            RectTransform marker,
            Vector3 worldPosition)
        {
            if (marker == null || worldCamera == null)
            {
                return;
            }

            var canvas = marker.GetComponentInParent<Canvas>();
            if (canvas == null
                || canvas.transform is not RectTransform canvasRect)
            {
                return;
            }

            Vector3 viewport = worldCamera.WorldToViewportPoint(
                worldPosition + Vector3.up * 0.8f);
            bool behind = viewport.z < 0f;
            Vector2 clamped = new(
                Mathf.Clamp01(behind ? 1f - viewport.x : viewport.x),
                Mathf.Clamp01(behind ? 0f : viewport.y));

            Vector2 size = canvasRect.rect.size;
            marker.anchoredPosition = new Vector2(
                (clamped.x - 0.5f) * size.x,
                (clamped.y - 0.5f) * size.y);
        }

        private void Update()
        {
            Refresh();
        }
    }
}
