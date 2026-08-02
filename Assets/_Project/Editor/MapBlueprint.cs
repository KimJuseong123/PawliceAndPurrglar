using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Draws the town plan straight from the layout numbers.
    ///
    /// The photographic version — put a camera above the scene and render —
    /// produced a flat colour three times running while the diagnostics said
    /// the roads were present, in frame and at the right height. Whatever the
    /// cause, it sits somewhere in camera setup and the render pipeline, and
    /// none of that has anything to do with the question being asked, which is
    /// "are the streets where the drawing puts them".
    ///
    /// So this draws the answer instead of photographing it. No camera, no
    /// lights, no pipeline: rectangles from the same arrays the scene is built
    /// from, written to a PNG. It cannot be dark, cannot be off frame, and
    /// cannot be looking the wrong way.
    ///
    /// What it does not do is prove the scene matches the numbers. The scene is
    /// generated from these arrays, and `ValidateLayoutIsClear` measures them,
    /// so the gap is small — but a blueprint is a drawing of the plan, not a
    /// photograph of the town.
    /// </summary>
    internal static class MapBlueprint
    {
        private const int PixelsPerMetre = 12;
        private const int GridEveryMetres = 10;

        private static readonly Color Ground = new(0.86f, 0.85f, 0.80f);
        private static readonly Color Road = new(0.34f, 0.36f, 0.40f);
        private static readonly Color Alley = new(0.52f, 0.54f, 0.58f);
        private static readonly Color Grid = new(0.76f, 0.75f, 0.70f);
        private static readonly Color Axis = new(0.62f, 0.60f, 0.55f);
        private static readonly Color Border = new(0.25f, 0.24f, 0.22f);

        /// <summary>
        /// A rectangle in world metres, with y meaning z.
        /// </summary>
        internal readonly struct Piece
        {
            public Piece(Rect area, Color colour)
            {
                Area = area;
                Colour = colour;
            }

            public Rect Area { get; }
            public Color Colour { get; }
        }

        /// <summary>
        /// A number written on the plan, at a place in the world.
        /// </summary>
        internal readonly struct Label
        {
            public Label(string text, Vector2 at)
            {
                Text = text;
                At = at;
            }

            public string Text { get; }
            public Vector2 At { get; }
        }

        /// <summary>
        /// The ground the roads leave behind, as connected pieces.
        ///
        /// Found by filling rather than by subtracting rectangles, because the
        /// streets do not form a grid: they stop, jog and dead-end, so what is
        /// left between them is not a set of rectangles and pretending it is
        /// would report blocks that are actually joined round a corner.
        ///
        /// One cell is one metre. Finer would be slower and would only find
        /// slivers nobody can stand in anyway.
        /// </summary>
        internal static Rect[] FindBlocks(
            float minX,
            float minZ,
            float width,
            float depth,
            Rect[] roads,
            float smallestArea,
            out int[] areas)
        {
            int cellsX = Mathf.RoundToInt(width);
            int cellsZ = Mathf.RoundToInt(depth);
            var open = new bool[cellsX, cellsZ];

            for (int x = 0; x < cellsX; x++)
            {
                for (int z = 0; z < cellsZ; z++)
                {
                    var cell = new Rect(
                        minX + x + 0.5f,
                        minZ + z + 0.5f,
                        0.01f,
                        0.01f);
                    bool onRoad = false;
                    foreach (Rect road in roads)
                    {
                        if (road.Overlaps(cell))
                        {
                            onRoad = true;
                            break;
                        }
                    }

                    open[x, z] = !onRoad;
                }
            }

            var found = new List<Rect>();
            var sizes = new List<int>();
            var stack = new Stack<Vector2Int>();

            for (int x = 0; x < cellsX; x++)
            {
                for (int z = 0; z < cellsZ; z++)
                {
                    if (!open[x, z])
                    {
                        continue;
                    }

                    int minCellX = x;
                    int maxCellX = x;
                    int minCellZ = z;
                    int maxCellZ = z;
                    int count = 0;

                    stack.Push(new Vector2Int(x, z));
                    open[x, z] = false;
                    while (stack.Count > 0)
                    {
                        Vector2Int at = stack.Pop();
                        count++;
                        minCellX = Mathf.Min(minCellX, at.x);
                        maxCellX = Mathf.Max(maxCellX, at.x);
                        minCellZ = Mathf.Min(minCellZ, at.y);
                        maxCellZ = Mathf.Max(maxCellZ, at.y);

                        foreach (Vector2Int step in Steps)
                        {
                            int nextX = at.x + step.x;
                            int nextZ = at.y + step.y;
                            if (nextX < 0
                                || nextZ < 0
                                || nextX >= cellsX
                                || nextZ >= cellsZ
                                || !open[nextX, nextZ])
                            {
                                continue;
                            }

                            open[nextX, nextZ] = false;
                            stack.Push(new Vector2Int(nextX, nextZ));
                        }
                    }

                    if (count < smallestArea)
                    {
                        continue;
                    }

                    found.Add(new Rect(
                        minX + minCellX,
                        minZ + minCellZ,
                        maxCellX - minCellX + 1,
                        maxCellZ - minCellZ + 1));
                    sizes.Add(count);
                }
            }

            areas = sizes.ToArray();
            return found.ToArray();
        }

        private static readonly Vector2Int[] Steps =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
        };

        /// <summary>
        /// Writes the plan. Bounds are the town's own, so the picture is always
        /// the whole town at a fixed scale rather than whatever fitted.
        /// </summary>
        internal static void Write(
            string outputPath,
            float minX,
            float minZ,
            float width,
            float depth,
            Piece[] pieces,
            Label[] labels = null)
        {
            int pixelWidth = Mathf.RoundToInt(width * PixelsPerMetre);
            int pixelHeight = Mathf.RoundToInt(depth * PixelsPerMetre);
            var image = new Texture2D(
                pixelWidth,
                pixelHeight,
                TextureFormat.RGB24,
                false);

            var fill = new Color[pixelWidth * pixelHeight];
            for (int index = 0; index < fill.Length; index++)
            {
                fill[index] = Ground;
            }

            image.SetPixels(fill);

            // Grid first so everything else sits on top of it.
            for (float metre = 0f; metre <= width; metre += GridEveryMetres)
            {
                DrawColumn(
                    image,
                    Mathf.RoundToInt(metre * PixelsPerMetre),
                    Mathf.Approximately(metre % 20f, 0f) ? Axis : Grid);
            }

            for (float metre = 0f; metre <= depth; metre += GridEveryMetres)
            {
                DrawRow(
                    image,
                    Mathf.RoundToInt(metre * PixelsPerMetre),
                    Mathf.Approximately(metre % 20f, 0f) ? Axis : Grid);
            }

            foreach (Piece piece in pieces)
            {
                FillRect(
                    image,
                    piece.Area,
                    minX,
                    minZ,
                    piece.Colour);
            }

            if (labels != null)
            {
                foreach (Label label in labels)
                {
                    DrawText(
                        image,
                        label.Text,
                        Mathf.RoundToInt((label.At.x - minX) * PixelsPerMetre),
                        Mathf.RoundToInt((label.At.y - minZ) * PixelsPerMetre));
                }
            }

            DrawBorder(image, Border);
            image.Apply();

            string full = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);

            Debug.Log(
                $"[BLUEPRINT] {pixelWidth}x{pixelHeight} px at "
                + $"{PixelsPerMetre} px/m covering {width:0}x{depth:0} m "
                + $"from ({minX:0}, {minZ:0}) -> {full}");
        }

        /// <summary>
        /// A tint per block, cycling through a few so neighbours differ. The
        /// point is telling one block from the next, not colour meaning
        /// anything.
        /// </summary>
        internal static Color BlockColour(int index)
        {
            Color[] tints =
            {
                new(0.80f, 0.84f, 0.74f),
                new(0.86f, 0.80f, 0.72f),
                new(0.76f, 0.82f, 0.84f),
                new(0.86f, 0.83f, 0.70f),
                new(0.79f, 0.77f, 0.84f)
            };
            return tints[index % tints.Length];
        }

        internal static Color BuildingColour(bool isPlaza)
        {
            return isPlaza
                ? new Color(0.62f, 0.72f, 0.55f)
                : new Color(0.42f, 0.40f, 0.38f);
        }

        internal static Color RoadColour(bool isAlley)
        {
            return isAlley ? Alley : Road;
        }

        /// <summary>
        /// World rectangle to pixels.
        ///
        /// The vertical flip is the whole reason this is one function: world z
        /// grows north and image rows grow downward, and getting that backwards
        /// produces a plan that is upside down but otherwise entirely
        /// convincing.
        /// </summary>
        private static void FillRect(
            Texture2D image,
            Rect area,
            float minX,
            float minZ,
            Color colour)
        {
            int left = Mathf.RoundToInt((area.xMin - minX) * PixelsPerMetre);
            int right = Mathf.RoundToInt((area.xMax - minX) * PixelsPerMetre);
            int bottom = Mathf.RoundToInt((area.yMin - minZ) * PixelsPerMetre);
            int top = Mathf.RoundToInt((area.yMax - minZ) * PixelsPerMetre);

            for (int x = Mathf.Max(0, left);
                 x < Mathf.Min(image.width, right);
                 x++)
            {
                for (int y = Mathf.Max(0, bottom);
                     y < Mathf.Min(image.height, top);
                     y++)
                {
                    image.SetPixel(x, y, colour);
                }
            }
        }

        private static void DrawColumn(Texture2D image, int x, Color colour)
        {
            if (x < 0 || x >= image.width)
            {
                return;
            }

            for (int y = 0; y < image.height; y++)
            {
                image.SetPixel(x, y, colour);
            }
        }

        private static void DrawRow(Texture2D image, int y, Color colour)
        {
            if (y < 0 || y >= image.height)
            {
                return;
            }

            for (int x = 0; x < image.width; x++)
            {
                image.SetPixel(x, y, colour);
            }
        }

        /// <summary>
        /// A 3x5 digit, drawn by hand.
        ///
        /// Unity's text does not render into a Texture2D without a camera and a
        /// canvas, which is the machinery this whole tool exists to avoid. Ten
        /// digits of five bytes is a smaller price than bringing the render
        /// pipeline back for the sake of a number.
        /// </summary>
        private static readonly string[] Digits =
        {
            "111101101101111", // 0
            "010110010010111", // 1
            "111001111100111", // 2
            "111001111001111", // 3
            "101101111001001", // 4
            "111100111001111", // 5
            "111100111101111", // 6
            "111001001001001", // 7
            "111101111101111", // 8
            "111101111001111"  // 9
        };

        private const int DigitScale = 3;
        private static readonly Color Ink = new(0.15f, 0.14f, 0.13f);
        private static readonly Color Halo = new(0.98f, 0.97f, 0.94f);

        /// <summary>
        /// Writes a number centred on a point, with a pale halo behind it so it
        /// stays readable over road and ground alike.
        /// </summary>
        private static void DrawText(
            Texture2D image,
            string text,
            int centreX,
            int centreY)
        {
            int glyphWidth = 3 * DigitScale;
            int glyphHeight = 5 * DigitScale;
            int gap = DigitScale;
            int totalWidth =
                text.Length * glyphWidth + (text.Length - 1) * gap;
            int left = centreX - totalWidth / 2;
            int bottom = centreY - glyphHeight / 2;

            for (int x = left - DigitScale;
                 x <= left + totalWidth + DigitScale;
                 x++)
            {
                for (int y = bottom - DigitScale;
                     y <= bottom + glyphHeight + DigitScale;
                     y++)
                {
                    if (x >= 0 && y >= 0
                        && x < image.width && y < image.height)
                    {
                        image.SetPixel(x, y, Halo);
                    }
                }
            }

            for (int index = 0; index < text.Length; index++)
            {
                if (!char.IsDigit(text[index]))
                {
                    continue;
                }

                string glyph = Digits[text[index] - '0'];
                int originX = left + index * (glyphWidth + gap);
                for (int row = 0; row < 5; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        if (glyph[row * 3 + column] != '1')
                        {
                            continue;
                        }

                        // Rows read top-down, image rows read bottom-up.
                        int blockX = originX + column * DigitScale;
                        int blockY =
                            bottom + (4 - row) * DigitScale;
                        for (int dx = 0; dx < DigitScale; dx++)
                        {
                            for (int dy = 0; dy < DigitScale; dy++)
                            {
                                int px = blockX + dx;
                                int py = blockY + dy;
                                if (px >= 0 && py >= 0
                                    && px < image.width
                                    && py < image.height)
                                {
                                    image.SetPixel(px, py, Ink);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void DrawBorder(Texture2D image, Color colour)
        {
            for (int x = 0; x < image.width; x++)
            {
                image.SetPixel(x, 0, colour);
                image.SetPixel(x, image.height - 1, colour);
            }

            for (int y = 0; y < image.height; y++)
            {
                image.SetPixel(0, y, colour);
                image.SetPixel(image.width - 1, y, colour);
            }
        }
    }
}
