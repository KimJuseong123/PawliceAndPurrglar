using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Brings the four authored lobby pair sprites into the project.
    ///
    /// Two things about the delivered files have to be fixed before they can be
    /// shown side by side, and both are invisible in a file browser:
    ///
    /// One of the four arrived as 24-bit RGB with no alpha channel at all, so it
    /// would have drawn as a solid dark square over the lobby's ivory. Its
    /// background is a near-black neutral grey well below anything the artwork
    /// uses — even the thief's black hoodie sits twice as bright — so it is
    /// keyed out here rather than sent back.
    ///
    /// And the four frame their subjects at different scales inside their square
    /// canvases. Displayed at one fixed height the thief pair visibly shrank on
    /// being selected. Cropping every sprite to its own opaque bounds makes the
    /// characters, not the padding, what the layout sizes.
    /// </summary>
    public static class LobbyPairArtImporter
    {
        private const string SourceFolder = "ArtSource/Lobby";

        internal const string OutputFolder =
            "Assets/_Project/UI/Lobby/Sprites";

        internal static readonly string[] PairNames =
        {
            "police_dog_idle",
            "police_dog_selected",
            "thief_cat_idle",
            "thief_cat_selected"
        };

        /// <summary>
        /// The background is a band, not everything below a ceiling.
        ///
        /// Measured on the delivered file, the backdrop sits at 0.078–0.094 —
        /// but the artwork's own outlines and the thief's shoes go down to
        /// 0.012, *darker* than the backdrop. A plain "darker than this is
        /// background" rule therefore ate his legs while leaving the backdrop
        /// he stood on. Anything below the floor is artwork and is kept, which
        /// also turns those near-black outlines into a wall the fill cannot
        /// cross.
        /// </summary>
        private const float BackgroundValueFloor = 0.05f;

        /// <summary>
        /// Up to here, inside the band, a pixel is background outright.
        /// </summary>
        private const float HardBackgroundValue = 0.115f;

        /// <summary>
        /// Between the hard ceiling and this the pixel fades, which keeps the
        /// keyed silhouette from looking cut out. The hoodie starts near 0.18.
        /// </summary>
        private const float SoftBackgroundValue = 0.165f;

        /// <summary>
        /// A source counts as already transparent if anything in it is not fully
        /// opaque. Checking the alpha channel rather than the file's bit depth,
        /// because a 32-bit file can still be entirely opaque.
        /// </summary>
        private const byte OpaqueThreshold = 250;

        [MenuItem("PawliceAndPurrglar/UI/Import Lobby Pair Art")]
        public static void Import()
        {
            MockupCutter.EnsureFolder(OutputFolder);

            var written = new List<string>();
            var keyed = new List<string>();
            foreach (string name in PairNames)
            {
                string source = $"{SourceFolder}/{name}.png";
                string full = Path.GetFullPath(source);
                if (!File.Exists(full))
                {
                    throw new FileNotFoundException(
                        $"Lobby pair art is missing: {source}. Save the four "
                        + "authored pair PNGs there first.",
                        full);
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(full)))
                {
                    throw new InvalidDataException($"Could not read {source}.");
                }

                Color32[] pixels = texture.GetPixels32();
                if (!HasTransparency(pixels))
                {
                    KeyOutDarkBackground(pixels, texture.width, texture.height);
                    keyed.Add(name);
                }

                written.Add(
                    WriteCropped(name, pixels, texture.width, texture.height));
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.Refresh();
            foreach (string path in written)
            {
                ApplyImportSettings(path);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Imported {written.Count} lobby pair sprites into "
                + $"'{OutputFolder}'. Cropped every one to its opaque bounds so "
                + "the pairs match at a shared height."
                + (keyed.Count > 0
                    ? $" Keyed a background out of: {string.Join(", ", keyed)} "
                      + "(delivered without an alpha channel)."
                    : string.Empty));
        }

        private static bool HasTransparency(IReadOnlyList<Color32> pixels)
        {
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a < OpaqueThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Flood fills the near-black backdrop in from the border.
        ///
        /// A flood fill rather than a threshold over the whole image: the
        /// characters have their own dark recesses and outlines, and a global
        /// cut punches holes straight through them. Anything enclosed by the
        /// artwork is unreachable from the edge and survives.
        /// </summary>
        private static void KeyOutDarkBackground(
            Color32[] pixels,
            int width,
            int height)
        {
            var visited = new bool[pixels.Length];
            var queue = new Queue<int>();

            void Seed(int x, int y)
            {
                int index = y * width + x;
                if (visited[index])
                {
                    return;
                }

                visited[index] = true;
                float value = Value(pixels[index]);
                if (value >= SoftBackgroundValue
                    || value < BackgroundValueFloor)
                {
                    // Too bright to be the backdrop, or darker than it — either
                    // way it is artwork, and the fill stops here.
                    return;
                }

                Color32 pixel = pixels[index];
                if (value <= HardBackgroundValue)
                {
                    pixel.a = 0;
                }
                else
                {
                    float fade = (value - HardBackgroundValue)
                        / (SoftBackgroundValue - HardBackgroundValue);
                    pixel.a = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(fade) * 255f);
                }

                pixels[index] = pixel;
                queue.Enqueue(index);
            }

            for (int x = 0; x < width; x++)
            {
                Seed(x, 0);
                Seed(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                Seed(0, y);
                Seed(width - 1, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                if (x > 0)
                {
                    Seed(x - 1, y);
                }

                if (x < width - 1)
                {
                    Seed(x + 1, y);
                }

                if (y > 0)
                {
                    Seed(x, y - 1);
                }

                if (y < height - 1)
                {
                    Seed(x, y + 1);
                }
            }
        }

        private static float Value(Color32 pixel)
        {
            return Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) / 255f;
        }

        /// <summary>
        /// Writes the sprite trimmed to its opaque bounds, so a shared display
        /// height means a shared character height.
        /// </summary>
        private static string WriteCropped(
            string name,
            Color32[] pixels,
            int width,
            int height)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 8)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (minX > maxX || minY > maxY)
            {
                throw new InvalidDataException(
                    $"'{name}' is fully transparent after keying, so the "
                    + "background thresholds do not suit this file.");
            }

            const int Padding = 2;
            minX = Mathf.Max(0, minX - Padding);
            maxX = Mathf.Min(width - 1, maxX + Padding);
            minY = Mathf.Max(0, minY - Padding);
            maxY = Mathf.Min(height - 1, maxY + Padding);

            int cutWidth = maxX - minX + 1;
            int cutHeight = maxY - minY + 1;
            var cut = new Color32[cutWidth * cutHeight];
            for (int y = 0; y < cutHeight; y++)
            {
                for (int x = 0; x < cutWidth; x++)
                {
                    cut[y * cutWidth + x] =
                        pixels[(minY + y) * width + minX + x];
                }
            }

            var cropped = new Texture2D(
                cutWidth,
                cutHeight,
                TextureFormat.RGBA32,
                false);
            cropped.SetPixels32(cut);
            cropped.Apply();

            string path = $"{OutputFolder}/{name}.png";
            File.WriteAllBytes(Path.GetFullPath(path), cropped.EncodeToPNG());
            Object.DestroyImmediate(cropped);
            return path;
        }

        /// <summary>
        /// The settings the art brief asks for: full rect so nothing is trimmed
        /// off the silhouette, alpha treated as transparency, no mip maps and no
        /// compression, so eyes and outlines stay clean at UI scale.
        /// </summary>
        private static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException(
                    $"Imported sprite has no importer: {path}",
                    path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
