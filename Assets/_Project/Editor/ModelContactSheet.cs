using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Renders every environment model straight down onto one sheet.
    ///
    /// The road pieces are named for what someone called them at export —
    /// "road tile", "road section", "modular road tile", "street intersection"
    /// — and those names do not say which is the straight, which is the
    /// crossroads and which is the T. Reading it off the texture atlas is
    /// guesswork, and guessing wrong lays a crossing across every metre of
    /// every street, which is exactly what happened.
    ///
    /// So: look at them. Each model is photographed from above at its own
    /// scale, labelled with its file name and its size in metres, and written
    /// to `Logs/model-sheet.png`.
    ///
    /// This runs in graphics mode, which dirties QualitySettings and
    /// GraphicsSettings as a side effect. Both are put back; check
    /// `git status -- ProjectSettings/` afterwards.
    /// </summary>
    internal static class ModelContactSheet
    {
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";
        private const string Output = "Logs/model-sheet.png";
        private const int Cell = 320;
        private const int Columns = 4;

        [MenuItem("Paws & Loot/Setup/Capture Model Sheet")]
        public static void Capture()
        {
            string[] paths = AssetDatabase
                .FindAssets("t:Model", new[] { EnvironmentDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx"))
                .Distinct()
                .OrderBy(path => path)
                .ToArray();

            int rows = Mathf.CeilToInt(paths.Length / (float)Columns);
            var sheet = new Texture2D(
                Columns * Cell,
                rows * Cell,
                TextureFormat.RGB24,
                false);
            var background = Enumerable
                .Repeat(new Color(0.12f, 0.12f, 0.14f), sheet.width * sheet.height)
                .ToArray();
            sheet.SetPixels(background);

            var notes = new List<string>();
            var stage = new GameObject("Contact Sheet Stage");
            var camera = new GameObject("Contact Sheet Camera")
                .AddComponent<Camera>();
            var sun = new GameObject("Contact Sheet Sun").AddComponent<Light>();

            try
            {
                camera.transform.SetParent(stage.transform);
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                sun.transform.SetParent(stage.transform);
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(55f, 20f, 0f);

                for (int index = 0; index < paths.Length; index++)
                {
                    string path = paths[index];
                    var asset =
                        AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null)
                    {
                        continue;
                    }

                    var subject =
                        (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    subject.transform.SetParent(stage.transform);
                    subject.transform.position = Vector3.zero;

                    Bounds bounds = Extent(subject);
                    notes.Add(
                        $"{Path.GetFileNameWithoutExtension(path)}: "
                        + $"{bounds.size.x:0.00} x {bounds.size.y:0.00} x "
                        + $"{bounds.size.z:0.00} m");

                    camera.orthographicSize =
                        Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.15f;
                    camera.transform.position = new Vector3(
                        bounds.center.x,
                        bounds.max.y + 20f,
                        bounds.center.z);
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 60f + bounds.size.y;

                    Blit(camera, sheet, index);
                    Object.DestroyImmediate(subject);
                }
            }
            finally
            {
                Object.DestroyImmediate(stage);
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllBytes(Output, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log(
                $"[ART] {paths.Length} models photographed to {Output}, "
                + $"left to right, top to bottom:\n  "
                + string.Join("\n  ", notes));
        }

        private static void Blit(Camera camera, Texture2D sheet, int index)
        {
            var target = new RenderTexture(Cell, Cell, 24)
            {
                antiAliasing = 4
            };
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;

                var frame = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0f, 0f, Cell, Cell), 0, 0);
                frame.Apply();

                int column = index % Columns;
                int row = index / Columns;

                // Top to bottom in reading order, and the image's rows run the
                // other way, so the row is flipped once here rather than
                // everywhere it is used.
                int rows = sheet.height / Cell;
                sheet.SetPixels(
                    column * Cell,
                    (rows - 1 - row) * Cell,
                    Cell,
                    Cell,
                    frame.GetPixels());
                sheet.Apply();
                Object.DestroyImmediate(frame);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static Bounds Extent(GameObject subject)
        {
            Renderer[] pieces =
                subject.GetComponentsInChildren<Renderer>(true);
            if (pieces.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }

            Bounds bounds = pieces[0].bounds;
            foreach (Renderer piece in pieces)
            {
                bounds.Encapsulate(piece.bounds);
            }

            return bounds;
        }
    }
}
