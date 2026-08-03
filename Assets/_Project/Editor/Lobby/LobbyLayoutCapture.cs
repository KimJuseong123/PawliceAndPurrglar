using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Renders a screen to a PNG at each resolution that has to hold, without
    /// building a player.
    ///
    /// The same reason the map has an overhead capture: a layout can satisfy
    /// every assertion about sizes and anchors and still put a status line
    /// across somebody's feet. Nothing but a picture catches that, and waiting
    /// for a full Windows build to see one makes it tempting not to look.
    /// </summary>
    public static class LobbyLayoutCapture
    {
        private const string OutputFolder = "Logs";

        private static readonly Vector2Int[] Resolutions =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
            // Not a target resolution; included because a 4:3 window is where a
            // layout tuned on 16:9 usually collides.
            new(1440, 1080)
        };

        [MenuItem("Paws & Loot/UI/Capture Lobby Layout")]
        public static void CaptureLobby()
        {
            Capture(
                "Assets/_Project/Scenes/Bootstrap.unity",
                "LobbyCanvas",
                "lobby");
        }

        [MenuItem("Paws & Loot/UI/Capture Result Layout")]
        public static void CaptureResult()
        {
            Capture(
                "Assets/_Project/Scenes/Result.unity",
                "ResultCanvas",
                "result");
        }

        private static void Capture(
            string scenePath,
            string canvasName,
            string filePrefix)
        {
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);

            Canvas canvas = FindCanvas(scene, canvasName);
            var scaler = canvas.GetComponent<CanvasScaler>();
            RenderMode originalMode = canvas.renderMode;
            Camera originalCamera = canvas.worldCamera;

            var cameraObject = new GameObject("UI Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.949f, 0.898f, 0.855f, 1f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = new Vector3(0f, 0f, -500f);

            // Overlay canvases cannot be pointed at a render texture, so the
            // canvas is borrowed into camera space for the shot and handed back
            // afterwards.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;

            // Rendering to an off-screen target leaves these changed behind it,
            // which then commits as an unrelated project-wide change. The map
            // capture hit the same thing; captured and restored rather than
            // trusted.
            int previousAntiAliasing = QualitySettings.antiAliasing;
            bool previousColorTemperature = UnityEngine.Rendering
                .GraphicsSettings.lightsUseColorTemperature;

            Directory.CreateDirectory(Path.GetFullPath(OutputFolder));
            try
            {
                foreach (Vector2Int size in Resolutions)
                {
                    Shoot(canvas, scaler, camera, size, filePrefix);
                }
            }
            finally
            {
                QualitySettings.antiAliasing = previousAntiAliasing;
                UnityEngine.Rendering.GraphicsSettings
                    .lightsUseColorTemperature = previousColorTemperature;
                canvas.renderMode = originalMode;
                canvas.worldCamera = originalCamera;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                // Closed without saving: the borrowed render mode and the
                // capture camera must not end up in the scene.
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
        }

        private static void Shoot(
            Canvas canvas,
            CanvasScaler scaler,
            Camera camera,
            Vector2Int size,
            string filePrefix)
        {
            var texture = new RenderTexture(size.x, size.y, 24)
            {
                antiAliasing = 4
            };
            camera.targetTexture = texture;
            camera.orthographicSize = size.y * 0.5f;

            // The scaler reads the camera's pixel rect in camera space, so the
            // layout has to be rebuilt after the render texture changes size or
            // every shot comes out at the previous one's scale.
            scaler.enabled = false;
            scaler.enabled = true;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                (RectTransform)canvas.transform);
            foreach (TMP_Text label in
                     canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                label.ForceMeshUpdate();
            }

            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var shot = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            string path =
                $"{OutputFolder}/{filePrefix}-{size.x}x{size.y}.png";
            File.WriteAllBytes(Path.GetFullPath(path), shot.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(shot);

            camera.targetTexture = null;
            texture.Release();
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log($"Captured {filePrefix} layout at {size.x}x{size.y} -> {path}");
        }

        private static Canvas FindCanvas(Scene scene, string canvasName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != canvasName)
                {
                    continue;
                }

                var canvas = root.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return canvas;
                }
            }

            throw new System.InvalidOperationException(
                $"'{scene.name}' has no '{canvasName}'. Rebuild the screen "
                + "from the 'Paws & Loot/UI' menu first.");
        }
    }
}
