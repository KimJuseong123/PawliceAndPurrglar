using System;
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
        /// Writes the plan. Bounds are the town's own, so the picture is always
        /// the whole town at a fixed scale rather than whatever fitted.
        /// </summary>
        internal static void Write(
            string outputPath,
            float minX,
            float minZ,
            float width,
            float depth,
            Piece[] pieces)
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
