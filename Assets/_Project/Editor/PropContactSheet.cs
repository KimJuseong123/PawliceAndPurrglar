using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Photographs the generated prop prefabs at the size the match loads them.
    ///
    /// Photographs the prefabs, not the source FBX files, because the prefab is
    /// where the measured scale and the ground offset are baked and the prefab is
    /// what a running match loads. This project has already learnt that a model
    /// can photograph perfectly from a tool that instantiates it fresh while the
    /// thing actually saved is untextured — the tool and the game were looking at
    /// two different objects.
    ///
    /// Shot from a shallow angle rather than overhead. The texture is on the
    /// sides, so a top-down sheet shows a clean footprint over a torn unwrap. A
    /// one-metre rule is drawn behind each prop, because "is it the right size"
    /// is the question a picture answers worst without one.
    /// </summary>
    internal static class PropContactSheet
    {
        private const string PrefabFolder = "Assets/_Project/Resources/Props";
        private const string Output = "Logs/prop-sheet.png";
        private const int Cell = 360;
        private const int Columns = 4;

        private static readonly Color Background =
            new(0.16f, 0.17f, 0.20f);

        [MenuItem("PawliceAndPurrglar/Setup/Capture Prop Sheet")]
        public static void Capture()
        {
            string[] paths = AssetDatabase
                .FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .ToArray();

            if (paths.Length == 0)
            {
                Debug.LogError(
                    $"[PROPS] No prop prefabs under {PrefabFolder}. Run "
                    + "'Sync Throwable Props To Resources' first.");
                return;
            }

            int rows = Mathf.CeilToInt(paths.Length / (float)Columns);
            var sheet = new Texture2D(
                Columns * Cell, rows * Cell, TextureFormat.RGB24, false);
            // Filled, so a row that does not divide by four ends in
            // background rather than in whatever the allocation held.
            sheet.SetPixels(Enumerable
                .Repeat(Background, sheet.width * sheet.height)
                .ToArray());

            var stage = new GameObject("Prop Stage");
            var camera = new GameObject("Prop Camera").AddComponent<Camera>();
            var sun = new GameObject("Prop Sun").AddComponent<Light>();

            try
            {
                camera.transform.SetParent(stage.transform);
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 40f;

                sun.transform.SetParent(stage.transform);
                sun.type = LightType.Directional;
                sun.intensity = 1.3f;
                sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

                // A metre of ground and a metre-tall post, so the prop is
                // measured against something rather than admired on its own.
                GameObject ground = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                ground.transform.SetParent(stage.transform);
                ground.transform.localScale = new Vector3(1f, 0.02f, 1f);
                ground.transform.position = new Vector3(0f, -0.01f, 0f);

                GameObject rule = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                rule.transform.SetParent(stage.transform);
                rule.transform.localScale = new Vector3(0.02f, 1f, 0.02f);
                rule.transform.position = new Vector3(-0.42f, 0.5f, -0.42f);

                // Thrown away, and the reason the first prop on this sheet came
                // out grey while every other prop came out textured.
                //
                // In batch mode the first render of a session happens before the
                // textures it needs are resident, so whatever sorts first is
                // photographed untextured. It was the banana, which sent an
                // afternoon into the importer, the material, the UVs and the
                // normals — all of which were correct. The sheet was wrong, not
                // the model.
                //
                // A diagnostic that is slightly not what it says it is costs more
                // than no diagnostic at all.
                Warm(camera);

                for (int index = 0; index < paths.Length; index++)
                {
                    var asset =
                        AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]);
                    if (asset == null)
                    {
                        continue;
                    }

                    var subject =
                        (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    subject.transform.SetParent(stage.transform);
                    subject.transform.position = Vector3.zero;
                    subject.transform.rotation = Quaternion.identity;

                    // Fixed frame across every cell. Fitting each prop to its
                    // own bounds would make them all the same size on the
                    // sheet, which is the one thing this has to show.
                    // Wide enough for the tallest prop in the set. The
                    // frame used to cut the rubber chicken off at the
                    // shoulders and the sheet read as a headless model.
                    camera.orthographicSize = 0.58f;
                    camera.transform.position =
                        new Vector3(0.9f, 0.72f, -0.9f);
                    camera.transform.rotation =
                        Quaternion.Euler(24f, -45f, 0f);

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
                $"[PROPS] {paths.Length} prop prefabs photographed to {Output}, "
                + $"{Columns} to a row, alphabetical. The post is one metre and "
                + "the slab is one metre square:\n  "
                + string.Join(
                    "\n  ",
                    paths.Select(Path.GetFileNameWithoutExtension)));
        }

        /// <summary>
        /// Renders one frame nobody looks at, so the first frame anybody does
        /// look at has its textures.
        /// </summary>
        private static void Warm(Camera camera)
        {
            var target = new RenderTexture(Cell, Cell, 24);
            try
            {
                camera.targetTexture = target;
                camera.Render();
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
            }
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
