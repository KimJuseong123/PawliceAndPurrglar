using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Reports how heavy each road model is and how its top face is unwrapped.
    ///
    /// Two questions, because the answer to the first decides whether the
    /// second matters. A tile of a few hundred triangles can simply be placed;
    /// a tile of a hundred thousand has to be cut down to its top face, and
    /// cutting depends on the top face being one tidy island in the atlas. When
    /// it is not, the cut spans the gap between islands and the road comes out
    /// as dark smears.
    /// </summary>
    internal static class RoadMeshProbe
    {
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";

        [MenuItem("PawliceAndPurrglar/Setup/Report Road Meshes")]
        public static void Report()
        {
            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { EnvironmentDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("env_road"))
                .Distinct()
                .OrderBy(path => path))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    continue;
                }

                long triangles = 0;
                int meshes = 0;
                float minU = float.MaxValue;
                float maxU = float.MinValue;
                float minV = float.MaxValue;
                float maxV = float.MinValue;
                int upward = 0;

                foreach (MeshFilter filter in
                    model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    meshes++;
                    triangles += mesh.triangles.Length / 3;

                    Vector3[] normals = mesh.normals;
                    Vector2[] uv = mesh.uv;
                    for (int index = 0;
                        index < normals.Length && index < uv.Length;
                        index++)
                    {
                        if (normals[index].y < 0.9f)
                        {
                            continue;
                        }

                        upward++;
                        minU = Mathf.Min(minU, uv[index].x);
                        maxU = Mathf.Max(maxU, uv[index].x);
                        minV = Mathf.Min(minV, uv[index].y);
                        maxV = Mathf.Max(maxV, uv[index].y);
                    }
                }

                string span = upward == 0
                    ? "no upward faces"
                    : $"u {minU:0.00}..{maxU:0.00}  v {minV:0.00}..{maxV:0.00}";

                Debug.Log(
                    $"[ROAD] {System.IO.Path.GetFileNameWithoutExtension(path)}: "
                    + $"{triangles:N0} tris in {meshes} mesh(es), "
                    + $"{upward} upward verts, {span}");
            }
        }
    }
}
