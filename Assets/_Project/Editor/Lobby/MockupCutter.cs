using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Cuts individual elements out of an authored full-screen mockup.
    ///
    /// Shared by the lobby and the result screen. Both were built as one
    /// painted image with controls pinned over it at fixed offsets, which only
    /// lines up at the mockup's own aspect; splitting the picture into sprites
    /// is what lets each screen be anchored instead of painted.
    /// </summary>
    internal static class MockupCutter
    {
        /// <summary>
        /// Above this the pixel is background outright. The mockups' ivory sits
        /// near 0.94 and the cast shadows bottom out around 0.78, so one floor
        /// removes both and leaves the artwork, whose darkest lit surfaces are
        /// well below it.
        /// </summary>
        private const float HardBackgroundValue = 0.62f;

        /// <summary>
        /// Between this and <see cref="HardBackgroundValue"/> the pixel fades
        /// out rather than cutting, which keeps silhouettes from looking
        /// stamped.
        /// </summary>
        private const float SoftBackgroundValue = 0.50f;

        /// <summary>
        /// The ivory is a warm grey and a white paw or whisker is not. Without
        /// this the flood fill walks straight through the cat's paws, because
        /// on brightness and saturation alone they read as background.
        /// </summary>
        private const int MinimumWarmth = 6;

        private const float MaximumBackgroundSaturation = 0.26f;

        /// <summary>
        /// Anything darker than this on the outer rows is the mockup's own
        /// painted frame, which belongs to the PNG rather than the screen.
        /// </summary>
        private const float FrameValueCeiling = 0.16f;

        /// <summary>
        /// Fraction of a search window trimmed off each side to get the core
        /// that <see cref="BlobFilter.CoreOverlap"/> tests against.
        /// </summary>
        private const float CoreInset = 0.2f;

        /// <summary>
        /// How to tell an element apart from whatever its search window also
        /// caught. Every window borders another element, and a window loose
        /// enough not to clip its own subject bites into its neighbour.
        /// </summary>
        internal enum BlobFilter
        {
            /// <summary>
            /// Keep only blobs reaching the middle of the window. Right for a
            /// single subject that fills its window, because intruders enter
            /// from an edge and stop well short of the centre.
            /// </summary>
            CoreOverlap,

            /// <summary>
            /// Keep only blobs clear of the window border. Right for lettering,
            /// which is many separate blobs — none of which reaches the middle
            /// — inside a window with margin all round.
            /// </summary>
            DropEdgeTouching,

            /// <summary>
            /// Take the window as it stands, fully opaque. Right for a piece of
            /// illustration that carries its own background, where keying would
            /// punch holes in the artwork.
            /// </summary>
            None
        }

        internal readonly struct Element
        {
            internal Element(
                string name,
                int x,
                int y,
                int width,
                int height,
                BlobFilter filter)
            {
                Name = name;
                Left = x;
                Top = y;
                Width = width;
                Height = height;
                Filter = filter;
            }

            internal string Name { get; }

            /// <summary>Left edge in source pixels.</summary>
            internal int Left { get; }

            /// <summary>Top edge in source pixels, measured downwards.</summary>
            internal int Top { get; }

            internal int Width { get; }

            internal int Height { get; }

            internal BlobFilter Filter { get; }
        }

        /// <summary>
        /// Cuts every element out of one mockup and writes it as a sprite.
        /// Returns the sampled backdrop colour, which is what a screen built
        /// from these should fill itself with so the faint halo left around
        /// each keyed silhouette lands on the colour it was cut from.
        /// </summary>
        internal static Color32 Cut(
            string mockupPath,
            string outputFolder,
            IReadOnlyList<Element> elements)
        {
            Texture2D source = LoadReadable(mockupPath);
            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;

            RectInt content = MeasurePaintedFrame(pixels, width, height);
            byte[] alpha = BuildAlpha(pixels, width, height, content);

            EnsureFolder(outputFolder);
            var written = new List<string>();
            foreach (Element element in elements)
            {
                written.Add(
                    WriteElement(
                        element,
                        outputFolder,
                        pixels,
                        alpha,
                        width,
                        height));
            }

            AssetDatabase.Refresh();
            foreach (string path in written)
            {
                ApplySpriteImportSettings(path);
            }

            AssetDatabase.SaveAssets();
            Color32 backdrop = SampleBackdrop(pixels, width, height, content);
            Debug.Log(
                $"Cut {written.Count} sprites from '{mockupPath}' into "
                + $"'{outputFolder}'. Painted frame trimmed to "
                + $"{content.width}x{content.height}. Backdrop sample "
                + $"#{backdrop.r:X2}{backdrop.g:X2}{backdrop.b:X2}.");
            return backdrop;
        }

        /// <summary>
        /// The importer keeps mockups non-readable for the build, so the
        /// readable flag is turned on only for the duration of the cut.
        /// </summary>
        private static Texture2D LoadReadable(string mockupPath)
        {
            var importer =
                AssetImporter.GetAtPath(mockupPath) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException(
                    $"Mockup is missing: {mockupPath}",
                    mockupPath);
            }

            if (!importer.isReadable
                || importer.crunchedCompression
                || importer.textureCompression
                != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.crunchedCompression = false;
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(mockupPath);
            if (texture == null)
            {
                throw new FileNotFoundException(
                    $"Mockup could not be loaded: {mockupPath}",
                    mockupPath);
            }

            return texture;
        }

        /// <summary>
        /// Finds the picture inside the mockup's painted black border. Measured
        /// rather than assumed, because the flood fill has to start on a
        /// background pixel and starting on the frame would stop immediately.
        /// </summary>
        private static RectInt MeasurePaintedFrame(
            Color32[] pixels,
            int width,
            int height)
        {
            int left = 0;
            while (left < width / 4 && ColumnIsFrame(pixels, width, height, left))
            {
                left++;
            }

            int right = width - 1;
            while (right > width * 3 / 4
                   && ColumnIsFrame(pixels, width, height, right))
            {
                right--;
            }

            int bottom = 0;
            while (bottom < height / 4 && RowIsFrame(pixels, width, bottom))
            {
                bottom++;
            }

            int top = height - 1;
            while (top > height * 3 / 4 && RowIsFrame(pixels, width, top))
            {
                top--;
            }

            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        private static bool ColumnIsFrame(
            Color32[] pixels,
            int width,
            int height,
            int x)
        {
            int dark = 0;
            for (int y = 0; y < height; y++)
            {
                if (Value(pixels[y * width + x]) <= FrameValueCeiling)
                {
                    dark++;
                }
            }

            return dark > height * 0.9f;
        }

        private static bool RowIsFrame(Color32[] pixels, int width, int y)
        {
            int dark = 0;
            for (int x = 0; x < width; x++)
            {
                if (Value(pixels[y * width + x]) <= FrameValueCeiling)
                {
                    dark++;
                }
            }

            return dark > width * 0.9f;
        }

        /// <summary>
        /// Flood fills the backdrop inwards from the picture's border.
        ///
        /// A flood fill rather than a global colour replace, because the pixel
        /// lettering is filled with a cream barely distinguishable from the
        /// backdrop. Its dark outline stops the fill, so enclosed cream
        /// survives while the surrounding ivory does not.
        /// </summary>
        private static byte[] BuildAlpha(
            Color32[] pixels,
            int width,
            int height,
            RectInt content)
        {
            var alpha = new byte[pixels.Length];
            for (int index = 0; index < alpha.Length; index++)
            {
                alpha[index] = 255;
            }

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
                if (!TryClassifyBackdrop(pixels[index], out byte value))
                {
                    return;
                }

                alpha[index] = value;
                queue.Enqueue(index);
            }

            int maxX = content.xMax - 1;
            int maxY = content.yMax - 1;
            for (int x = content.xMin; x <= maxX; x++)
            {
                Seed(x, content.yMin);
                Seed(x, maxY);
            }

            for (int y = content.yMin; y <= maxY; y++)
            {
                Seed(content.xMin, y);
                Seed(maxX, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                if (x > content.xMin)
                {
                    Seed(x - 1, y);
                }

                if (x < maxX)
                {
                    Seed(x + 1, y);
                }

                if (y > content.yMin)
                {
                    Seed(x, y - 1);
                }

                if (y < maxY)
                {
                    Seed(x, y + 1);
                }
            }

            return alpha;
        }

        /// <summary>
        /// Decides whether a pixel is backdrop and, if so, how transparent it
        /// becomes. Fully transparent above the hard floor; fading below it so
        /// silhouettes keep a soft edge instead of a cut one.
        /// </summary>
        private static bool TryClassifyBackdrop(Color32 pixel, out byte alpha)
        {
            alpha = 255;
            float value = Value(pixel);
            if (value < SoftBackgroundValue)
            {
                return false;
            }

            if (Saturation(pixel) > MaximumBackgroundSaturation)
            {
                return false;
            }

            if (pixel.r - pixel.b < MinimumWarmth)
            {
                return false;
            }

            if (value >= HardBackgroundValue)
            {
                alpha = 0;
                return true;
            }

            float fade = (value - SoftBackgroundValue)
                / (HardBackgroundValue - SoftBackgroundValue);
            alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - fade) * 255f);
            return true;
        }

        private static float Value(Color32 pixel)
        {
            return Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) / 255f;
        }

        private static float Saturation(Color32 pixel)
        {
            int max = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            if (max == 0)
            {
                return 0f;
            }

            int min = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            return (max - min) / (float)max;
        }

        private static Color32 SampleBackdrop(
            Color32[] pixels,
            int width,
            int height,
            RectInt content)
        {
            int x = Mathf.Clamp(content.xMin + content.width / 2, 0, width - 1);
            int y = Mathf.Clamp(
                height - (content.yMin + (int)(content.height * 0.62f)),
                0,
                height - 1);
            return pixels[y * width + x];
        }

        /// <summary>
        /// Writes one element. Keyed elements are trimmed to the pixels that
        /// survived the fill rather than to the search window, so a loose
        /// window costs nothing; unkeyed ones are taken exactly as framed.
        /// </summary>
        private static string WriteElement(
            Element element,
            string outputFolder,
            Color32[] pixels,
            byte[] alpha,
            int width,
            int height)
        {
            int left = Mathf.Clamp(element.Left, 0, width - 1);
            int right = Mathf.Clamp(element.Left + element.Width - 1, 0, width - 1);
            // Tables are written top-down to match how a mockup reads; texture
            // rows run bottom-up.
            int bottom = Mathf.Clamp(
                height - (element.Top + element.Height),
                0,
                height - 1);
            int top = Mathf.Clamp(height - 1 - element.Top, 0, height - 1);

            int minX = left;
            int maxX = right;
            int minY = bottom;
            int maxY = top;
            bool[] keep = null;
            int windowWidth = right - left + 1;

            if (element.Filter != BlobFilter.None)
            {
                keep = SelectBlobs(
                    element,
                    alpha,
                    width,
                    left,
                    right,
                    bottom,
                    top);

                minX = int.MaxValue;
                maxX = int.MinValue;
                minY = int.MaxValue;
                maxY = int.MinValue;
                for (int y = bottom; y <= top; y++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        if (!keep[(y - bottom) * windowWidth + (x - left)])
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
                    throw new System.InvalidOperationException(
                        $"Element '{element.Name}' came out empty. Its search "
                        + "window holds no opaque pixels, so either the window "
                        + "or the backdrop thresholds are wrong.");
                }

                const int Padding = 2;
                minX = Mathf.Max(left, minX - Padding);
                maxX = Mathf.Min(right, maxX + Padding);
                minY = Mathf.Max(bottom, minY - Padding);
                maxY = Mathf.Min(top, maxY + Padding);
            }

            int cutWidth = maxX - minX + 1;
            int cutHeight = maxY - minY + 1;
            var cut = new Color32[cutWidth * cutHeight];
            for (int y = 0; y < cutHeight; y++)
            {
                for (int x = 0; x < cutWidth; x++)
                {
                    int sourceX = minX + x;
                    int sourceY = minY + y;
                    int sourceIndex = sourceY * width + sourceX;
                    Color32 pixel = pixels[sourceIndex];
                    if (keep != null)
                    {
                        // Discarded blobs are erased rather than merely
                        // excluded from the bounding box: a neighbour's sliver
                        // can sit well inside the crop even after the box has
                        // been tightened.
                        pixel.a = keep[
                            (sourceY - bottom) * windowWidth
                            + (sourceX - left)]
                            ? alpha[sourceIndex]
                            : (byte)0;
                    }
                    else
                    {
                        pixel.a = 255;
                    }

                    cut[y * cutWidth + x] = pixel;
                }
            }

            var texture = new Texture2D(
                cutWidth,
                cutHeight,
                TextureFormat.RGBA32,
                false);
            texture.SetPixels32(cut);
            texture.Apply();

            string path = $"{outputFolder}/{element.Name}.png";
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return path;
        }

        /// <summary>
        /// Labels the opaque blobs inside a search window and returns which of
        /// them belong to the element.
        ///
        /// Without this every window kept a bite of its neighbour — the lobby
        /// logo caught the corner of the thief's hat, the dog caught the
        /// officer's sleeve. Tightening the windows instead would have clipped
        /// the subjects.
        /// </summary>
        private static bool[] SelectBlobs(
            Element element,
            byte[] alpha,
            int width,
            int left,
            int right,
            int bottom,
            int top)
        {
            int windowWidth = right - left + 1;
            int windowHeight = top - bottom + 1;
            var keep = new bool[windowWidth * windowHeight];
            var labelled = new bool[windowWidth * windowHeight];
            var blob = new List<int>();
            var queue = new Queue<int>();

            int coreLeft = left + Mathf.RoundToInt(windowWidth * CoreInset);
            int coreRight = right - Mathf.RoundToInt(windowWidth * CoreInset);
            int coreBottom = bottom + Mathf.RoundToInt(windowHeight * CoreInset);
            int coreTop = top - Mathf.RoundToInt(windowHeight * CoreInset);

            bool Opaque(int x, int y)
            {
                return alpha[y * width + x] > 8;
            }

            for (int startY = bottom; startY <= top; startY++)
            {
                for (int startX = left; startX <= right; startX++)
                {
                    int startLocal = (startY - bottom) * windowWidth
                        + (startX - left);
                    if (labelled[startLocal] || !Opaque(startX, startY))
                    {
                        continue;
                    }

                    blob.Clear();
                    queue.Clear();
                    labelled[startLocal] = true;
                    queue.Enqueue(startLocal);
                    bool touchesCore = false;
                    bool touchesEdge = false;

                    while (queue.Count > 0)
                    {
                        int local = queue.Dequeue();
                        blob.Add(local);
                        int x = left + local % windowWidth;
                        int y = bottom + local / windowWidth;

                        touchesCore |= x >= coreLeft
                            && x <= coreRight
                            && y >= coreBottom
                            && y <= coreTop;
                        touchesEdge |= x == left
                            || x == right
                            || y == bottom
                            || y == top;

                        for (int direction = 0; direction < 4; direction++)
                        {
                            int nextX = x
                                + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                            int nextY = y
                                + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                            if (nextX < left
                                || nextX > right
                                || nextY < bottom
                                || nextY > top)
                            {
                                continue;
                            }

                            int nextLocal = (nextY - bottom) * windowWidth
                                + (nextX - left);
                            if (labelled[nextLocal] || !Opaque(nextX, nextY))
                            {
                                continue;
                            }

                            labelled[nextLocal] = true;
                            queue.Enqueue(nextLocal);
                        }
                    }

                    bool wanted = element.Filter == BlobFilter.CoreOverlap
                        ? touchesCore
                        : !touchesEdge;
                    if (!wanted)
                    {
                        continue;
                    }

                    foreach (int local in blob)
                    {
                        keep[local] = true;
                    }
                }
            }

            return keep;
        }

        internal static void ApplySpriteImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetFullPath(folder));
            AssetDatabase.Refresh();
        }
    }
}
