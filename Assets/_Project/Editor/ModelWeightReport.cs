using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Lists every imported model by how many triangles it costs.
    ///
    /// The point is a decimation list somebody can work from, so it reports the
    /// source file each model came from rather than the imported name. Nobody
    /// opens `building_house_2f.fbx` in Blender; they open whatever it was
    /// called when it was exported.
    ///
    /// Also says whether the model is actually placed in the sandbox scene.
    /// A heavy model nothing uses costs nothing to ship, and decimating it
    /// would be an afternoon spent on a file that never reaches the player.
    /// </summary>
    internal static class ModelWeightReport
    {
        private const string ArtRoot = "Assets/_Project/Art";
        private const string ScenePath =
            "Assets/_Project/Sandbox/MapSandbox.unity";

        [MenuItem("PawliceAndPurrglar/Setup/Report Model Weights")]
        public static void Report()
        {
            HashSet<string> used = UsedInScene();
            var rows = new List<(long Triangles, string Stem, string Source,
                bool Used)>();

            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx"))
                .Distinct()
                .OrderBy(path => path))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    continue;
                }

                long triangles = 0;
                foreach (MeshFilter filter in
                    model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null)
                    {
                        triangles += filter.sharedMesh.triangles.Length / 3;
                    }
                }

                foreach (SkinnedMeshRenderer skin in
                    model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh != null)
                    {
                        triangles += skin.sharedMesh.triangles.Length / 3;
                    }
                }

                string stem = Path.GetFileNameWithoutExtension(path);
                rows.Add((triangles, stem, FindSource(stem), used.Contains(stem)));
            }

            var lines = new List<string>();
            long placed = 0;
            foreach (var row in rows.OrderByDescending(row => row.Triangles))
            {
                if (row.Used)
                {
                    placed += row.Triangles;
                }

                lines.Add(
                    $"{row.Triangles,9:N0}  {(row.Used ? "IN SCENE" : "unused  ")}"
                    + $"  {row.Stem,-28}  {row.Source}");
            }

            Debug.Log(
                $"[WEIGHT] {rows.Count} models, "
                + $"{placed:N0} triangles of them in the sandbox scene.\n"
                + string.Join("\n", lines));
        }

        /// <summary>
        /// Which imported models the sandbox scene actually places.
        ///
        /// Read from the scene file rather than by opening it, because the
        /// question is only which assets it references and the text says so
        /// directly.
        /// </summary>
        private static HashSet<string> UsedInScene()
        {
            var used = new HashSet<string>();
            if (!File.Exists(ScenePath))
            {
                return used;
            }

            string scene = File.ReadAllText(ScenePath);
            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { ArtRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct())
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid) && scene.Contains(guid))
                {
                    used.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            return used;
        }

        /// <summary>
        /// The ArtSource file an imported model came from, matched by texture.
        ///
        /// Import renames the model, so the name in the project says nothing
        /// about which Blender file to open. The baked texture folder keeps the
        /// original name, which is the one thread back to the source.
        /// </summary>
        private static string FindSource(string stem)
        {
            string fbm = $"{ArtRoot}/../Art/Environment/{stem}.fbm";
            foreach (string folder in new[]
            {
                $"{ArtRoot}/Environment/{stem}.fbm",
                $"{ArtRoot}/Buildings/{stem}.fbm",
                $"{ArtRoot}/Props/{stem}.fbm",
                $"{ArtRoot}/Characters/{stem}.fbm",
                fbm
            })
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                string texture = Directory
                    .GetFiles(folder)
                    .FirstOrDefault(file => !file.EndsWith(".meta"));
                if (texture == null)
                {
                    continue;
                }

                string original = Path.GetFileNameWithoutExtension(texture)
                    .Replace("_basecolor", string.Empty)
                    .Replace('+', ' ');
                return $"{original}.fbx";
            }

            return "(source unknown — no baked texture to trace)";
        }
    }
}
