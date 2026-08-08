using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Works out which way round each road piece is.
    ///
    /// A modular road only works if the code knows that the T-junction's stem
    /// points north and the corner joins west to south. Reading that off a
    /// render is a judgement call, and a judgement call about rotation is the
    /// kind that produces a road grid where every junction is turned a quarter
    /// turn and nothing lines up.
    ///
    /// It is measurable instead. The carriageway is sunk below the kerb on
    /// every one of these pieces, so an edge the road runs out of is low and an
    /// edge the kerb closes off is high. This samples the top of the geometry
    /// in a strip along each of the four edges and reports which are open.
    /// </summary>
    internal static class RoadPieceProbe
    {
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";

        [MenuItem("Pawlice and Purrglar/Setup/Report Road Pieces")]
        public static void Report()
        {
            var lines = new List<string>();

            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { EnvironmentDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("env_road"))
                .Distinct()
                .OrderBy(path => path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    continue;
                }

                var subject = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                try
                {
                    lines.Add(Describe(asset.name, subject));
                }
                finally
                {
                    Object.DestroyImmediate(subject);
                }
            }

            Debug.Log("[ROAD] Edge heights, low means the road runs out:\n  "
                + string.Join("\n  ", lines));
        }

        private static string Describe(string name, GameObject subject)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter filter in
                subject.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Transform space = filter.transform;
                points.AddRange(
                    mesh.vertices.Select(space.TransformPoint));
            }

            if (points.Count == 0)
            {
                return $"{name}: no geometry";
            }

            float minX = points.Min(point => point.x);
            float maxX = points.Max(point => point.x);
            float minZ = points.Min(point => point.z);
            float maxZ = points.Max(point => point.z);
            float minY = points.Min(point => point.y);
            float maxY = points.Max(point => point.y);
            float width = maxX - minX;
            float depth = maxZ - minZ;

            // A narrow band across the middle of each edge: the kerb runs the
            // whole way along an edge it closes, and an edge the road leaves
            // through is open in the middle whatever the corners do.
            float band = 0.15f;
            float north = EdgeTop(
                points,
                point => point.z > maxZ - depth * band
                    && Mathf.Abs(point.x - (minX + maxX) * 0.5f)
                        < width * 0.15f);
            float south = EdgeTop(
                points,
                point => point.z < minZ + depth * band
                    && Mathf.Abs(point.x - (minX + maxX) * 0.5f)
                        < width * 0.15f);
            float east = EdgeTop(
                points,
                point => point.x > maxX - width * band
                    && Mathf.Abs(point.z - (minZ + maxZ) * 0.5f)
                        < depth * 0.15f);
            float west = EdgeTop(
                points,
                point => point.x < minX + width * band
                    && Mathf.Abs(point.z - (minZ + maxZ) * 0.5f)
                        < depth * 0.15f);

            // Halfway between the carriageway and the top of the kerb.
            float threshold = minY + (maxY - minY) * 0.5f;
            string open = string.Join(
                "",
                new[]
                {
                    north < threshold ? "N" : "-",
                    east < threshold ? "E" : "-",
                    south < threshold ? "S" : "-",
                    west < threshold ? "W" : "-"
                });

            return $"{name}: open {open}  "
                + $"(N {north:0.000} E {east:0.000} S {south:0.000} "
                + $"W {west:0.000}, kerb {maxY:0.000}, road {minY:0.000})";
        }

        private static float EdgeTop(
            List<Vector3> points,
            System.Func<Vector3, bool> inside)
        {
            float top = float.NegativeInfinity;
            foreach (Vector3 point in points)
            {
                if (inside(point) && point.y > top)
                {
                    top = point.y;
                }
            }

            return float.IsNegativeInfinity(top) ? 0f : top;
        }
    }
}
