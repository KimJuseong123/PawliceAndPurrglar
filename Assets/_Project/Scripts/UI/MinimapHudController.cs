using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Minimal top-right minimap. It follows the local player and reuses the
    /// existing distraction board as the alert-direction source.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapHudController : MonoBehaviour
    {
        private const int TextureSize = 256;

        [SerializeField] private Camera minimapCamera;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform playerMarker;
        [SerializeField] private RectTransform alertMarker;
        [SerializeField, Min(1f)] private float orthographicSize = 18f;
        [SerializeField, Min(1f)] private float cameraHeight = 28f;

        private RenderTexture renderTexture;
        private ToolCarrier targetCarrier;
        private DistractionBoard distractionBoard;

        public Camera MinimapCamera => minimapCamera;

        public void Configure(
            Camera configuredCamera,
            RawImage configuredMapImage,
            RectTransform configuredPlayerMarker,
            RectTransform configuredAlertMarker)
        {
            if (configuredCamera != null)
            {
                minimapCamera = configuredCamera;
            }
            mapImage = configuredMapImage;
            playerMarker = configuredPlayerMarker;
            alertMarker = configuredAlertMarker;
        }

        private void Awake()
        {
            EnsureCamera();
        }

        private void OnDestroy()
        {
            if (renderTexture == null)
            {
                return;
            }

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        private void Update()
        {
            EnsureCamera();
            ResolveSources();
            if (minimapCamera == null || targetCarrier == null)
            {
                return;
            }

            Transform target = targetCarrier.transform;
            minimapCamera.transform.position =
                target.position + Vector3.up * cameraHeight;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (playerMarker != null)
            {
                playerMarker.anchoredPosition = Vector2.zero;
                playerMarker.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    -target.eulerAngles.y);
            }

            BindAlert(target.position);
        }

        /// <summary>
        /// Whether this machine can draw a minimap at all.
        ///
        /// A headless run has no graphics device, so `RenderTexture.Create`
        /// quietly fails and the camera is left pointing at a texture that does
        /// not exist. URP then walks into its depth-normal prepass with no
        /// surface to attach and takes the whole process down — the editor
        /// segfaults, the results file is never written, and the failure looks
        /// like the test framework rather than a minimap.
        ///
        /// That is the entire reason Play Mode could only be run with a window
        /// open. Asked here rather than guarded at the call sites, so nothing
        /// downstream has to remember.
        /// </summary>
        private static bool CanRender =>
            SystemInfo.graphicsDeviceType
                != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private void EnsureCamera()
        {
            if (!CanRender)
            {
                return;
            }

            if (minimapCamera == null)
            {
                GameObject cameraObject = new("MinimapCamera");
                cameraObject.transform.SetParent(transform, false);
                minimapCamera = cameraObject.AddComponent<Camera>();
            }

            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = Mathf.Max(1f, orthographicSize);
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.055f, 0.075f, 0.095f, 1f);
            minimapCamera.cullingMask = 1 << 0;
            minimapCamera.allowHDR = false;
            minimapCamera.allowMSAA = false;
            minimapCamera.enabled = true;

            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(
                    TextureSize,
                    TextureSize,
                    16,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Essential HUD Minimap RenderTexture",
                    filterMode = FilterMode.Bilinear,
                    antiAliasing = 1
                };
                renderTexture.Create();
                renderTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            minimapCamera.targetTexture = renderTexture;
            if (mapImage != null)
            {
                mapImage.texture = renderTexture;
            }
        }

        private void ResolveSources()
        {
            PlayerRole role = ResolveRole();
            if (targetCarrier == null || targetCarrier.Role != role)
            {
                targetCarrier = null;
                foreach (ToolCarrier candidate in
                    FindObjectsByType<ToolCarrier>(FindObjectsSortMode.None))
                {
                    if (candidate.Role == role)
                    {
                        targetCarrier = candidate;
                        break;
                    }
                }
            }

            distractionBoard ??= FindFirstObjectByType<DistractionBoard>();
        }

        private static PlayerRole ResolveRole()
        {
            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            return selector != null ? selector.ActiveRole : PlayerRole.Police;
        }

        private void BindAlert(Vector3 playerPosition)
        {
            if (alertMarker == null)
            {
                return;
            }

            bool visible = distractionBoard != null
                && distractionBoard.IsActive
                && ResolveRole() == PlayerRole.Police;
            alertMarker.gameObject.SetActive(visible);
            if (!visible || minimapCamera == null)
            {
                return;
            }

            Vector3 viewport = minimapCamera.WorldToViewportPoint(
                distractionBoard.ActivePosition);
            Vector2 direction = new(
                viewport.x - 0.5f,
                viewport.y - 0.5f);
            bool inside = viewport.z > 0f
                && Mathf.Abs(direction.x) <= 0.48f
                && Mathf.Abs(direction.y) <= 0.48f;
            if (!inside)
            {
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector2.up;
                }

                direction = direction.normalized * 0.43f;
            }

            RectTransform parent = alertMarker.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            alertMarker.anchoredPosition = new Vector2(
                direction.x * parent.rect.width,
                direction.y * parent.rect.height);
            alertMarker.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
