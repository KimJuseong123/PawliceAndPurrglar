using System.IO;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Renders the Game scene straight down into a PNG.
    ///
    /// Layout problems are visual and the play camera cannot show them: it sits
    /// low behind one character, so a district can be lopsided or half empty
    /// without anything in a log or a test saying so. This is the plan view that
    /// makes road spacing and block fill checkable at a glance.
    /// </summary>
    public static class MapOverviewCapture
    {
        private const string OutputPath =
            "Logs/map-overview.png";

        private const string Map02OutputPath =
            "Logs/map02-overview.png";

        private const int PixelsPerMeter = 12;

        [MenuItem("PawliceAndPurrglar/Setup/Capture Map Overview")]
        public static void Capture()
        {
            CaptureScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OutputPath);
        }

        /// <summary>
        /// The same plan view of MAP-002.
        ///
        /// A town is re-cut by editing coordinates, and coordinates are exactly
        /// what nobody can check by reading. Overlap can be measured, but "this
        /// block is empty and that one is crammed" only shows up in a picture.
        /// </summary>
        [MenuItem("PawliceAndPurrglar/Setup/Capture MAP-002 Overview")]
        public static void CaptureMap02()
        {
            CaptureScene(Map02GreyboxSetup.ScenePath, Map02OutputPath);
        }

        private static void CaptureScene(
            string scenePath,
            string outputPath)
        {
            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);

            GreyboxMapDefinition map = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                map = root.GetComponentInChildren<
                    GreyboxMapDefinition>(true);
                if (map != null)
                {
                    break;
                }
            }

            if (map == null)
            {
                throw new System.InvalidOperationException(
                    "The Game scene has no GreyboxMapDefinition to frame.");
            }

            // The ground is centred on the map bounds, so the renderer is
            // framed from the ground itself rather than from the origin: the
            // village is not symmetric around zero any more.
            Bounds bounds = ResolveGroundBounds(scene, map);

            int width = Mathf.RoundToInt(
                bounds.size.x * PixelsPerMeter);
            int height = Mathf.RoundToInt(
                bounds.size.z * PixelsPerMeter);

            var cameraObject = new GameObject("Overview Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = bounds.size.z * 0.5f;
            camera.transform.position = new Vector3(
                bounds.center.x,
                80f,
                bounds.center.z);
            camera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 200f;

            // Rendering to an off-screen target has been observed to leave
            // QualitySettings.antiAliasing changed behind it, which would then
            // be committed as an unrelated project-wide change and quietly
            // degrade every build. Captured and restored rather than trusted.
            int previousAntiAliasing = QualitySettings.antiAliasing;
            bool previousColorTemperature = UnityEngine.Rendering
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
                .lightsUseColorTemperature = previousColorTemperature;

            string full = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            Debug.Log(
                $"[MAP-OVERVIEW] {width}x{height} px covering "
                + $"{bounds.size.x:0}x{bounds.size.z:0} m -> {full}");
        }

        private static Bounds ResolveGroundBounds(
            Scene scene,
            GreyboxMapDefinition map)
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

            // Falls back to the declared size, centred on the origin, which is
            // only correct for the pre-expansion map but is better than failing.
            return new Bounds(
                Vector3.zero,
                new Vector3(
                    map.MapWidthMeters,
                    1f,
                    map.MapDepthMeters));
        }
    }
}
