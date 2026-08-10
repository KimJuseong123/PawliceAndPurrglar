using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Measures how far apart the road pieces are in colour.
    ///
    /// Each piece was baked on its own, so each carries its own idea of what
    /// tarmac looks like. On the contact sheet they read as one set; laid end
    /// to end in a town they do not, and the crossings and junctions stand out
    /// as lighter patches. Whether that is worth re-baking is a judgement, and
    /// the judgement wants a number rather than an impression.
    ///
    /// The carriageway is sampled at the middle of each tile's top face, which
    /// is asphalt on every one of them.
    /// </summary>
    internal static class RoadColourProbe
    {
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";

        [MenuItem("PawliceAndPurrglar/Setup/Report Road Colours")]
        public static void Report()
        {
            var readings = new List<(string Name, Color Colour)>();

            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[] { EnvironmentDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("env_road"))
                .Distinct()
                .OrderBy(path => path))
            {
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                Mesh tile = FlatTileLibrary.TileFor(
                    stem,
                    EnvironmentDirectory,
                    out Material paint);
                if (tile == null || paint == null)
                {
                    continue;
                }

                var texture = paint.GetTexture(Shader.PropertyToID("_BaseMap"))
                    as Texture2D;
                if (texture == null)
                {
                    continue;
                }

                Vector2[] uv = tile.uv;
                Vector2 middle = (uv[0] + uv[1] + uv[2] + uv[3]) * 0.25f;
                readings.Add((stem, Sample(texture, middle)));
            }

            if (readings.Count == 0)
            {
                Debug.LogWarning("[ROAD] No road tiles to sample.");
                return;
            }

            Color mean = readings.Aggregate(
                Color.black,
                (running, entry) => running + entry.Colour) / readings.Count;

            string lines = string.Join(
                "\n  ",
                readings.Select(entry =>
                {
                    float drift = Mathf.Max(
                        Mathf.Abs(entry.Colour.r - mean.r),
                        Mathf.Abs(entry.Colour.g - mean.g),
                        Mathf.Abs(entry.Colour.b - mean.b)) * 255f;
                    return $"{entry.Name}: "
                        + $"({entry.Colour.r * 255f:0}, "
                        + $"{entry.Colour.g * 255f:0}, "
                        + $"{entry.Colour.b * 255f:0})  "
                        + $"off the average by {drift:0}";
                }));

            Debug.Log(
                "[ROAD] Carriageway colour, average "
                + $"({mean.r * 255f:0}, {mean.g * 255f:0}, {mean.b * 255f:0}):"
                + $"\n  {lines}");
        }

        /// <summary>
        /// Reads one texel, going through a readable copy because imported
        /// textures are not readable and asking anyway throws.
        /// </summary>
        private static Color Sample(Texture2D texture, Vector2 uv)
        {
            var target = new RenderTexture(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var readable = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                int x = Mathf.Clamp(
                    Mathf.RoundToInt(uv.x * (texture.width - 1)),
                    0,
                    texture.width - 1);
                int y = Mathf.Clamp(
                    Mathf.RoundToInt(uv.y * (texture.height - 1)),
                    0,
                    texture.height - 1);
                readable.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                readable.Apply();
                return readable.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readable);
            }
        }
    }
}
