using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Cuts a model's top face out as a two-triangle tile.
    ///
    /// The road and grass models are scans: a hundred thousand triangles for a
    /// square of ground, and a town needs hundreds of them. Seen from the only
    /// angle this game is played at — above — none of that geometry shows.
    /// The kerbs, the lane markings and the junction shapes are painted into
    /// the texture, so a flat quad wearing the same corner of the same atlas
    /// looks the same and costs two triangles.
    ///
    /// Which corner of the atlas is worked out by fitting the model's own
    /// texture coordinates against its own footprint, not by assuming they run
    /// the same way. They do not: the pieces were unwrapped independently, so
    /// one tile's atlas island is turned a quarter turn against its neighbour's
    /// and a guess puts the centre line across the road instead of along it.
    /// </summary>
    internal static class FlatTileLibrary
    {
        private const string GeneratedDirectory =
            "Assets/_Project/Art/Generated";
        private const string MaterialDirectory =
            "Assets/_Project/Materials/Models";

        /// <summary>
        /// A one-metre square lying on y = 0, wearing the model's top face.
        ///
        /// Saved as an asset. A mesh built in memory is gone the moment the
        /// scene is saved, which is the same trap that made the roads invisible
        /// when their material was built that way.
        /// </summary>
        /// <param name="inset">
        /// How far in from the edge of the face to sample, as a fraction of its
        /// width. Zero takes the whole thing, which is right for a road tile
        /// whose kerbs are part of the picture. The grass tile needs some,
        /// because it has a bevelled rim in a different colour and repeating
        /// that across a lawn draws a tan grid over the town.
        /// </param>
        public static Mesh TileFor(
            string stem,
            string directory,
            out Material paint,
            float inset = 0f)
        {
            paint = AssetDatabase.LoadAssetAtPath<Material>(
                $"{MaterialDirectory}/{stem}.mat");
            // The inset is part of the name, so changing it makes a new tile
            // rather than quietly handing back the one cut with the old value.
            string path = inset > 0f
                ? $"{GeneratedDirectory}/{stem}_face_{inset:0.00}.asset"
                : $"{GeneratedDirectory}/{stem}_face.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null && paint != null)
            {
                return cached;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{directory}/{stem}.fbx");
            if (model == null)
            {
                Debug.LogWarning($"[ART] '{stem}.fbx' not found.");
                return null;
            }

            if (!Surface(model, out Rect footprint, out Fit u, out Fit v))
            {
                Debug.LogWarning(
                    $"[ART] '{stem}' has no flat top face to cut a tile from.");
                return null;
            }

            if (inset > 0f)
            {
                float back = Mathf.Min(footprint.width, footprint.height) * inset;
                footprint = Rect.MinMaxRect(
                    footprint.xMin + back,
                    footprint.yMin + back,
                    footprint.xMax - back,
                    footprint.yMax - back);
            }

            var quad = new Mesh
            {
                name = $"{stem}_face",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, -0.5f)
                },
                uv = new[]
                {
                    Coordinate(u, v, footprint.xMin, footprint.yMin),
                    Coordinate(u, v, footprint.xMin, footprint.yMax),
                    Coordinate(u, v, footprint.xMax, footprint.yMax),
                    Coordinate(u, v, footprint.xMax, footprint.yMin)
                },
                normals = new[]
                {
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            quad.RecalculateTangents();
            quad.RecalculateBounds();

            Directory.CreateDirectory(GeneratedDirectory);
            AssetDatabase.CreateAsset(quad, path);
            AssetDatabase.SaveAssets();
            return quad;
        }

        /// <summary>
        /// How one texture coordinate varies across the model's footprint.
        /// </summary>
        private readonly struct Fit
        {
            public Fit(float constant, float perX, float perZ)
            {
                Constant = constant;
                PerX = perX;
                PerZ = perZ;
            }

            public float Constant { get; }
            public float PerX { get; }
            public float PerZ { get; }
        }

        private static Vector2 Coordinate(Fit u, Fit v, float x, float z)
        {
            return new Vector2(
                u.Constant + u.PerX * x + u.PerZ * z,
                v.Constant + v.PerX * x + v.PerZ * z);
        }

        /// <summary>
        /// Finds the model's largest flat upward face and how its texture is
        /// laid across it.
        ///
        /// "Largest" is by area rather than by count, and heights are bucketed
        /// first, because the grass tile has tufts standing proud of it whose
        /// tops also point up. Taking every upward face together stretched the
        /// atlas across the gap between the lawn and the tufts, and the lawn
        /// came out streaked white.
        /// </summary>
        private static bool Surface(
            GameObject model,
            out Rect footprint,
            out Fit u,
            out Fit v)
        {
            footprint = default;
            u = default;
            v = default;

            var levels = new System.Collections.Generic.Dictionary<int, float>();
            var samples =
                new System.Collections.Generic.List<(Vector3 At, Vector2 Uv)>();
            float lowest = float.PositiveInfinity;
            float highest = float.NegativeInfinity;

            foreach (MeshFilter filter in
                model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || mesh.uv.Length == 0)
                {
                    continue;
                }

                foreach (Vector3 vertex in mesh.vertices)
                {
                    lowest = Mathf.Min(lowest, vertex.y);
                    highest = Mathf.Max(highest, vertex.y);
                }
            }

            float span = Mathf.Max(0.0001f, highest - lowest);

            // Two passes: the first weighs each height band by how much flat
            // upward area sits in it, the second keeps only the winner.
            for (int pass = 0; pass < 2; pass++)
            {
                int best = -1;
                if (pass == 1)
                {
                    float most = 0f;
                    foreach (var level in levels)
                    {
                        if (level.Value > most)
                        {
                            most = level.Value;
                            best = level.Key;
                        }
                    }

                    if (best < 0)
                    {
                        return false;
                    }
                }

                foreach (MeshFilter filter in
                    model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || mesh.uv.Length == 0)
                    {
                        continue;
                    }

                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    Vector2[] coordinates = mesh.uv;
                    int[] indices = mesh.triangles;

                    for (int corner = 0; corner + 2 < indices.Length; corner += 3)
                    {
                        int a = indices[corner];
                        int b = indices[corner + 1];
                        int c = indices[corner + 2];
                        if (a >= normals.Length
                            || b >= normals.Length
                            || c >= normals.Length
                            || a >= coordinates.Length
                            || b >= coordinates.Length
                            || c >= coordinates.Length)
                        {
                            continue;
                        }

                        if (normals[a].y < 0.9f
                            || normals[b].y < 0.9f
                            || normals[c].y < 0.9f)
                        {
                            continue;
                        }

                        float height =
                            (vertices[a].y + vertices[b].y + vertices[c].y) / 3f;
                        int band = Mathf.RoundToInt(
                            (height - lowest) / span * 50f);

                        if (pass == 0)
                        {
                            float area = Vector3.Cross(
                                vertices[b] - vertices[a],
                                vertices[c] - vertices[a]).magnitude * 0.5f;
                            levels.TryGetValue(band, out float running);
                            levels[band] = running + area;
                            continue;
                        }

                        if (band != best)
                        {
                            continue;
                        }

                        samples.Add((vertices[a], coordinates[a]));
                        samples.Add((vertices[b], coordinates[b]));
                        samples.Add((vertices[c], coordinates[c]));
                    }
                }
            }

            if (samples.Count < 3)
            {
                return false;
            }

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;
            foreach ((Vector3 at, Vector2 _) in samples)
            {
                minX = Mathf.Min(minX, at.x);
                maxX = Mathf.Max(maxX, at.x);
                minZ = Mathf.Min(minZ, at.z);
                maxZ = Mathf.Max(maxZ, at.z);
            }

            footprint = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return Solve(samples, 0, out u) && Solve(samples, 1, out v);
        }

        /// <summary>
        /// Least squares fit of one texture coordinate against x and z.
        ///
        /// Three unknowns, so three normal equations, solved by elimination.
        /// A fit rather than a corner-to-corner reading because the islands are
        /// not perfectly axis-aligned and one bad vertex should not decide the
        /// whole tile's orientation.
        /// </summary>
        private static bool Solve(
            System.Collections.Generic.List<(Vector3 At, Vector2 Uv)> samples,
            int channel,
            out Fit fit)
        {
            fit = default;

            double n = samples.Count;
            double sx = 0, sz = 0, sxx = 0, szz = 0, sxz = 0;
            double sw = 0, swx = 0, swz = 0;

            foreach ((Vector3 at, Vector2 uv) in samples)
            {
                double x = at.x;
                double z = at.z;
                double w = channel == 0 ? uv.x : uv.y;
                sx += x;
                sz += z;
                sxx += x * x;
                szz += z * z;
                sxz += x * z;
                sw += w;
                swx += w * x;
                swz += w * z;
            }

            // Centre the data so the constant term drops out of the two by two
            // system, which keeps the numbers well conditioned.
            double mx = sx / n;
            double mz = sz / n;
            double mw = sw / n;
            double cxx = sxx - n * mx * mx;
            double czz = szz - n * mz * mz;
            double cxz = sxz - n * mx * mz;
            double cwx = swx - n * mw * mx;
            double cwz = swz - n * mw * mz;

            double determinant = cxx * czz - cxz * cxz;
            if (System.Math.Abs(determinant) < 1e-12)
            {
                return false;
            }

            double perX = (cwx * czz - cwz * cxz) / determinant;
            double perZ = (cwz * cxx - cwx * cxz) / determinant;
            fit = new Fit(
                (float)(mw - perX * mx - perZ * mz),
                (float)perX,
                (float)perZ);
            return true;
        }
    }
}
