using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// A plan view of the sandbox town, so its shape can be checked without opening
    /// the editor.
    ///
    /// The same reason the main map has one: a road grid looks fine in a log while a
    /// building stands in the middle of a street. Two houses blocked an alley and the
    /// police station poked 3 m through a wall for a long time before anyone took a
    /// picture.
    /// </summary>
    internal static class MapSandboxOverview
    {
        private const float PixelsPerMeter = 9f;
        private const string OutputPath = "Logs/map-sandbox.png";

        [MenuItem("Paws & Loot/Sandbox/Capture Sandbox Overview")]
        public static void Capture()
        {
            Scene scene = EditorSceneManager.OpenScene(
                MapSandboxSetup.ScenePath,
                OpenSceneMode.Single);

            Bounds bounds = GroundBounds(scene);
            int width = Mathf.RoundToInt(bounds.size.x * PixelsPerMeter);
            int height = Mathf.RoundToInt(bounds.size.z * PixelsPerMeter);

            var cameraObject = new GameObject("Sandbox Overview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = bounds.size.z * 0.5f;
            camera.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, 140f, bounds.center.z),
                Quaternion.Euler(90f, 0f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 400f;

            // Rendering off-screen has been observed to leave QualitySettings
            // changed behind it, which would then be committed as an unrelated
            // project-wide change. Captured and restored rather than trusted.
            int previousAntiAliasing = QualitySettings.antiAliasing;
            bool previousTemperature = UnityEngine.Rendering
                .GraphicsSettings.lightsUseColorTemperature;

            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(
                width,
                height,
                TextureFormat.RGB24,
                false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            QualitySettings.antiAliasing = previousAntiAliasing;
            UnityEngine.Rendering.GraphicsSettings
                .lightsUseColorTemperature = previousTemperature;

            string full = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            Debug.Log(
                $"[SANDBOX-OVERVIEW] {width}x{height} px covering "
                + $"{bounds.size.x:0}x{bounds.size.z:0} m -> {full}");
        }

        private static Bounds GroundBounds(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in
                    root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "Ground")
                    {
                        continue;
                    }

                    var renderer = child.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        return renderer.bounds;
                    }
                }
            }

            return new Bounds(Vector3.zero, new Vector3(160f, 1f, 140f));
        }
    }
}
