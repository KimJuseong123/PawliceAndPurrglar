using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// CAT-004 police side. Shows where the cat raised a distraction.
    ///
    /// Visible to the police only, because a thief who could see their own
    /// decoy would gain nothing and the point is to mislead the other player.
    /// Read only: it never moves anything and never changes match state, so a
    /// distraction can cost the police attention but never the match directly.
    /// </summary>
    public sealed class DistractionAlertPresenter : MonoBehaviour
    {
        [SerializeField]
        private DistractionBoard board;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private Camera worldCamera;

        [SerializeField]
        private RectTransform marker;

        [SerializeField]
        private Text alertLabel;

        [SerializeField]
        private Text markerLabel;

        public bool IsMarkerVisible =>
            marker != null && marker.gameObject.activeSelf;
        public string AlertText =>
            alertLabel != null ? alertLabel.text : string.Empty;

        public void Configure(
            DistractionBoard configuredBoard,
            LocalPlayerRoleSelector configuredRoleSelector,
            Camera configuredCamera,
            RectTransform configuredMarker,
            Text configuredAlertLabel,
            Text configuredMarkerLabel)
        {
            board = configuredBoard;
            roleSelector = configuredRoleSelector;
            worldCamera = configuredCamera;
            marker = configuredMarker;
            alertLabel = configuredAlertLabel;
            markerLabel = configuredMarkerLabel;
            Refresh();
        }

        public void Refresh()
        {
            bool isPolice = roleSelector != null
                && roleSelector.ActiveRole == PlayerRole.Police;
            bool show = isPolice
                && board != null
                && board.IsActive;

            if (marker != null && marker.gameObject.activeSelf != show)
            {
                marker.gameObject.SetActive(show);
            }

            if (!show)
            {
                if (alertLabel != null)
                {
                    alertLabel.text = string.Empty;
                }

                return;
            }

            float remaining = board.RemainingSeconds(Time.time);
            if (alertLabel != null)
            {
                alertLabel.text =
                    $"수상한 소리! ({remaining:0.0}s)";
            }

            if (markerLabel != null)
            {
                markerLabel.text = "?";
            }

            PlaceMarker(board.ActivePosition);
        }

        /// <summary>
        /// Projects the signal onto the screen and clamps it to the edge when
        /// it is off view, so the police always get a direction to look.
        /// </summary>
        private void PlaceMarker(Vector3 worldPosition)
        {
            if (marker == null || worldCamera == null)
            {
                return;
            }

            var canvas = marker.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
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
