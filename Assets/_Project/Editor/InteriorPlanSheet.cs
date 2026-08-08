using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Photographs each interior from directly above, square on, at a known
    /// orientation.
    ///
    /// Which wall a room's door is in has been settled three times from
    /// gameplay screenshots and been wrong three times, because a screenshot is
    /// taken through an orbiting camera at an unknown yaw and there is no way
    /// to tell north from east in one. A plan view has one orientation and it
    /// is written on it.
    ///
    /// The rooms are laid out unrotated, so what is up in this picture is +Z
    /// for the code that places the doorway. That is the whole point: the
    /// picture and the constant are in the same coordinates.
    /// </summary>
    internal static class InteriorPlanSheet
    {
        private const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";
        private const string Output = "Logs/interior-plans.png";
        private const int Cell = 420;
        private const int Columns = 3;

        [MenuItem("PawliceAndPurrglar/Setup/Capture Interior Plans")]
        public static void Capture()
        {
            string[] paths = AssetDatabase
                .FindAssets("t:Model", new[] { BuildingDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    // The rooms a player can walk into, and only those. The
                    // jail is reached by being arrested rather than by a door,
                    // and the station and the spare house are not enterable at
                    // all, so putting them on a sheet meant for marking spawn
                    // points would only invite marks nothing reads.
                    return name.StartsWith("interior_")
                        && !name.EndsWith("_col")
                        && name != "interior_jail"
                        && name != "interior_police"
                        && name != "interior_house03";
                })
                .Distinct()
                .OrderBy(path => path)
                .ToArray();

            int rows = Mathf.CeilToInt(paths.Length / (float)Columns);
            var sheet = new Texture2D(
                Columns * Cell,
                rows * Cell,
                TextureFormat.RGB24,
                false);
            sheet.SetPixels(Enumerable
                .Repeat(new Color(0.10f, 0.10f, 0.12f), sheet.width * sheet.height)
                .ToArray());

            var stage = new GameObject("Plan Stage");
            var camera = new GameObject("Plan Camera").AddComponent<Camera>();
            var sun = new GameObject("Plan Sun").AddComponent<Light>();

            try
            {
                camera.transform.SetParent(stage.transform);
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.10f, 0.12f);

                // Straight down, and not turned. Up in the picture is +Z.
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                sun.transform.SetParent(stage.transform);
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(60f, 20f, 0f);

                for (int index = 0; index < paths.Length; index++)
                {
                    var asset = AssetDatabase
                        .LoadAssetAtPath<GameObject>(paths[index]);
                    if (asset == null)
                    {
                        continue;
                    }

                    var subject =
                        (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    subject.transform.SetParent(stage.transform);
                    subject.transform.position = Vector3.zero;
                    subject.transform.rotation = Quaternion.identity;

                    if (!TryGetBounds(subject, out Bounds bounds))
                    {
                        Object.DestroyImmediate(subject);
                        continue;
                    }

                    camera.orthographicSize =
                        Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.12f;
                    camera.transform.position = new Vector3(
                        bounds.center.x,
                        bounds.max.y + 20f,
                        bounds.center.z);
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 80f + bounds.size.y;

                    Debug.Log(
                        $"[PLAN] {Path.GetFileNameWithoutExtension(paths[index])}"
                        + $" cell {index}: {bounds.size.x:0.00} x "
                        + $"{bounds.size.z:0.00} m, view half-width "
                        + $"{camera.orthographicSize:0.00} m");
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
                $"[PLAN] {paths.Length} interiors drawn to {Output}, three to "
                + "a row, alphabetical. Up is +Z (forward), right is +X "
                + "(right), down is -Z (back), left is -X (left):\n  "
                + string.Join(
                    "\n  ",
                    paths.Select(Path.GetFileNameWithoutExtension)));
        }

        private static bool TryGetBounds(GameObject subject, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer part in
                subject.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = part.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(part.bounds);
                }
            }

            return any;
        }

        private static void Blit(Camera camera, Texture2D sheet, int index)
        {
            var target = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;

                var frame =
                    new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                frame.ReadPixels(new Rect(0f, 0f, Cell, Cell), 0, 0);
                frame.Apply();

                int column = index % Columns;
                int row = index / Columns;
                sheet.SetPixels(
                    column * Cell,
                    sheet.height - (row + 1) * Cell,
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
    }
}
