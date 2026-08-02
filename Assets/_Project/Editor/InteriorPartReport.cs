using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Counts what each interior model is made of.
    ///
    /// Which kind of collider a room should get is a question about its
    /// structure, not about its looks. If a room is one welded mesh then a
    /// mesh collider is the only thing that fits it; if the furniture is
    /// separate objects then a box each is cheaper and plays the same. Both
    /// answers are defensible and choosing between them by guessing is how a
    /// scene ends up carrying a million triangles of collision it did not need.
    /// </summary>
    internal static class InteriorPartReport
    {
        private const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";

        [MenuItem("Paws & Loot/Setup/Report Interior Parts")]
        public static void Report()
        {
            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { BuildingDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    Path.GetFileName(path).StartsWith("interior_"))
                .Distinct()
                .OrderBy(path => path))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    continue;
                }

                MeshFilter[] filters =
                    model.GetComponentsInChildren<MeshFilter>(true);
                long triangles = filters.Sum(filter =>
                    filter.sharedMesh == null
                        ? 0
                        : filter.sharedMesh.triangles.Length / 3);

                string biggest = filters
                    .OrderByDescending(filter =>
                        filter.sharedMesh == null
                            ? 0
                            : filter.sharedMesh.triangles.Length)
                    .Select(filter => filter.name)
                    .FirstOrDefault() ?? "-";

                Debug.Log(
                    $"[INTERIOR] {Path.GetFileNameWithoutExtension(path)}: "
                    + $"{filters.Length} part(s), {triangles:N0} tris, "
                    + $"largest '{biggest}'");
            }
        }
    }
}
