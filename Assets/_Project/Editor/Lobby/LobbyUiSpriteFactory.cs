using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// Draws the lobby's chrome — button plates, panels, fields, the selection
    /// glow and the two button marks — straight into PNGs.
    ///
    /// Drawn rather than cut out of the mockup because the mockup's buttons have
    /// their captions painted into them. A nine-sliced plate stretches its
    /// middle, so a painted caption would stretch with it. Drawing also lets the
    /// colours match the specified palette exactly and keeps the corners crisp
    /// at any button size.
    ///
    /// Plates come in two layers: an opaque outline behind and a white fill in
    /// front. Tinting a single-layer plate would drag the dark outline towards
    /// the button colour, and the disabled tint would wash it out entirely.
    /// </summary>
    public static class LobbyUiSpriteFactory
    {
        private const string OutputFolder = LobbyArtExtractor.OutputFolder;

        /// <summary>
        /// Plate side in pixels. Only the corners survive nine-slicing, so this
        /// is really "how much corner detail", not a button size.
        /// </summary>
        private const int PlateSize = 96;

        private const int PlateBorder = 26;

        /// <summary>
        /// A 45-degree corner cut rather than a radius: the reference art's
        /// buttons are chamfered, and the flat diagonal reads as moulded
        /// plastic where a radius reads as a web widget.
        /// </summary>
        private const float PlateChamfer = 18f;

        private static readonly Color Outline = FromHex("38271F");
        private static readonly Color PanelFill = FromHex("FFF9F2");
        private static readonly Color PanelOutline = FromHex("8A7462");
        private static readonly Color FieldFill = FromHex("FBF3E8");
        private static readonly Color FieldOutline = FromHex("D8C8B6");

        [MenuItem("PawliceAndPurrglar/UI/Generate Lobby Chrome Sprites")]
        public static void Generate()
        {
            LobbyArtExtractor.EnsureFolder(OutputFolder);

            var borders = new Dictionary<string, Vector4>();

            WritePlateOutline(borders);
            WritePlateFill(borders);
            WritePanel(borders);
            WriteField(borders);
            WriteGlow();
            WritePawMark();
            WriteMicrophoneMark();
            WriteRefreshMark();
            WriteHomeMark();
            WriteExitMark();

            AssetDatabase.Refresh();
            foreach (KeyValuePair<string, Vector4> entry in borders)
            {
                ApplySliced(entry.Key, entry.Value);
            }

            string[] plain =
            {
                "ui_glow",
                "icon_paw",
                "icon_mic",
                "icon_refresh",
                "icon_home",
                "icon_exit"
            };
            foreach (string simple in plain)
            {
                LobbyArtExtractor.ApplySpriteImportSettings(
                    $"{OutputFolder}/{simple}.png");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Generated {borders.Count + plain.Length} UI chrome sprites "
                + $"into '{OutputFolder}'.");
        }

        /// <summary>
        /// The dark plate that sits behind every button. Solid, so the fill in
        /// front can be inset by a few pixels to leave an even outline.
        /// </summary>
        private static void WritePlateOutline(
            IDictionary<string, Vector4> borders)
        {
            var pixels = new Color[PlateSize * PlateSize];
            for (int y = 0; y < PlateSize; y++)
            {
                for (int x = 0; x < PlateSize; x++)
                {
                    float coverage = ChamferCoverage(
                        x,
                        y,
                        PlateSize,
                        PlateSize,
                        PlateChamfer);
                    Color color = Outline;
                    color.a = coverage;
                    pixels[y * PlateSize + x] = color;
                }
            }

            string path = Write("ui_plate_outline", PlateSize, PlateSize, pixels);
            borders[path] = new Vector4(
                PlateBorder,
                PlateBorder,
                PlateBorder,
                PlateBorder);
        }

        /// <summary>
        /// The tintable face. White, with a lighter band along the top and a
        /// darker one along the bottom so a flat tint still reads as a moulded
        /// button rather than a rectangle of colour.
        /// </summary>
        private static void WritePlateFill(IDictionary<string, Vector4> borders)
        {
            var pixels = new Color[PlateSize * PlateSize];
            // Kept below the border so nine-slicing never stretches the bevel
            // into a gradient across the whole button.
            const float BevelHeight = 14f;
            const float HighlightHeight = 8f;

            for (int y = 0; y < PlateSize; y++)
            {
                for (int x = 0; x < PlateSize; x++)
                {
                    float coverage = ChamferCoverage(
                        x,
                        y,
                        PlateSize,
                        PlateSize,
                        PlateChamfer - 2f);

                    float shade = 0.94f;
                    if (y < BevelHeight)
                    {
                        float t = y / BevelHeight;
                        shade = Mathf.Lerp(0.68f, 0.94f, t * t);
                    }
                    else if (y > PlateSize - 1 - HighlightHeight)
                    {
                        float t = (PlateSize - 1 - y) / HighlightHeight;
                        shade = Mathf.Lerp(1f, 0.94f, t);
                    }

                    var color = new Color(shade, shade, shade, coverage);
                    pixels[y * PlateSize + x] = color;
                }
            }

            string path = Write("ui_plate_fill", PlateSize, PlateSize, pixels);
            borders[path] = new Vector4(
                PlateBorder,
                PlateBorder,
                PlateBorder,
                PlateBorder);
        }

        /// <summary>
        /// The address panel: rounded rather than chamfered, matching the
        /// reference, and not tinted, so its outline is baked in.
        /// </summary>
        private static void WritePanel(IDictionary<string, Vector4> borders)
        {
            const int Size = 96;
            const float Radius = 26f;
            const float Thickness = 5f;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float outer = RoundedCoverage(x, y, Size, Size, Radius, 0f);
                    float inner = RoundedCoverage(
                        x,
                        y,
                        Size,
                        Size,
                        Radius - Thickness,
                        Thickness);
                    Color color = Color.Lerp(PanelOutline, PanelFill, inner);
                    color.a = outer;
                    pixels[y * Size + x] = color;
                }
            }

            string path = Write("ui_panel", Size, Size, pixels);
            borders[path] = new Vector4(32f, 32f, 32f, 32f);
        }

        /// <summary>
        /// An input field: the same shape as the panel but lighter and with a
        /// thin edge, so a field inside a panel still reads as recessed.
        /// </summary>
        private static void WriteField(IDictionary<string, Vector4> borders)
        {
            const int Size = 64;
            const float Radius = 18f;
            const float Thickness = 2f;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float outer = RoundedCoverage(x, y, Size, Size, Radius, 0f);
                    float inner = RoundedCoverage(
                        x,
                        y,
                        Size,
                        Size,
                        Radius - Thickness,
                        Thickness);
                    Color color = Color.Lerp(FieldOutline, FieldFill, inner);
                    color.a = outer;
                    pixels[y * Size + x] = color;
                }
            }

            string path = Write("ui_field", Size, Size, pixels);
            borders[path] = new Vector4(22f, 22f, 22f, 22f);
        }

        /// <summary>
        /// The halo behind the selected team. White so it can be tinted to the
        /// team colour, with a squared falloff that stays soft when stretched
        /// over a character group.
        /// </summary>
        private static void WriteGlow()
        {
            const int Size = 256;
            var pixels = new Color[Size * Size];
            float centre = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            Write("ui_glow", Size, Size, pixels);
        }

        /// <summary>
        /// The paw mark used on the buttons and as the header decoration. White,
        /// so one sprite serves both the ivory decoration and the on-button
        /// marks.
        /// </summary>
        private static void WritePawMark()
        {
            const int Size = 128;
            var pixels = new Color[Size * Size];
            // Normalised, y measured downwards to match how the shape reads.
            var toes = new[]
            {
                new Vector4(0.20f, 0.34f, 0.115f, 0.145f),
                new Vector4(0.395f, 0.20f, 0.125f, 0.155f),
                new Vector4(0.615f, 0.20f, 0.125f, 0.155f),
                new Vector4(0.805f, 0.34f, 0.115f, 0.145f)
            };

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = 1f - (y + 0.5f) / Size;

                    float alpha = EllipseCoverage(
                        u,
                        v,
                        0.5f,
                        0.70f,
                        0.30f,
                        0.245f,
                        Size);
                    foreach (Vector4 toe in toes)
                    {
                        alpha = Mathf.Max(
                            alpha,
                            EllipseCoverage(
                                u,
                                v,
                                toe.x,
                                toe.y,
                                toe.z,
                                toe.w,
                                Size));
                    }

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Write("icon_paw", Size, Size, pixels);
        }

        /// <summary>
        /// The microphone mark for the check button. White for the same reason
        /// as the paw.
        /// </summary>
        private static void WriteMicrophoneMark()
        {
            const int Size = 128;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = 1f - (y + 0.5f) / Size;

                    // Capsule head.
                    float alpha = CapsuleCoverage(
                        u,
                        v,
                        0.5f,
                        0.20f,
                        0.5f,
                        0.46f,
                        0.115f,
                        Size);

                    // Cradle: the lower half of a ring under the head.
                    if (v >= 0.46f)
                    {
                        alpha = Mathf.Max(
                            alpha,
                            RingCoverage(u, v, 0.5f, 0.46f, 0.245f, 0.045f, Size));
                    }

                    // Stem and base.
                    alpha = Mathf.Max(
                        alpha,
                        CapsuleCoverage(
                            u,
                            v,
                            0.5f,
                            0.705f,
                            0.5f,
                            0.80f,
                            0.035f,
                            Size));
                    alpha = Mathf.Max(
                        alpha,
                        CapsuleCoverage(
                            u,
                            v,
                            0.355f,
                            0.845f,
                            0.645f,
                            0.845f,
                            0.038f,
                            Size));

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Write("icon_mic", Size, Size, pixels);
        }

        /// <summary>
        /// The rematch mark: a circular arrow. Drawn rather than cut out
        /// because the mockup's copy sits on a saturated button, where no
        /// colour key separates it from the plate underneath.
        /// </summary>
        private static void WriteRefreshMark()
        {
            const int Size = 128;
            const float Radius = 0.30f;
            const float Thickness = 0.075f;
            // Open on the right, where the arrowhead goes.
            const float StartDegrees = 34f;
            const float SweepDegrees = 296f;

            float head = StartDegrees * Mathf.Deg2Rad;
            var anchor = new Vector2(
                0.5f + Radius * Mathf.Cos(head),
                0.5f + Radius * Mathf.Sin(head));
            var tangent = new Vector2(Mathf.Sin(head), -Mathf.Cos(head));
            var radial = new Vector2(Mathf.Cos(head), Mathf.Sin(head));

            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = 1f - (y + 0.5f) / Size;

                    float alpha = ArcCoverage(
                        u,
                        v,
                        0.5f,
                        0.5f,
                        Radius,
                        Thickness,
                        StartDegrees,
                        SweepDegrees,
                        Size);
                    alpha = Mathf.Max(
                        alpha,
                        // Generous, because at the size a button leaves for it
                        // a small head disappears and the mark reads as a "C".
                        TriangleCoverage(
                            u,
                            v,
                            anchor + tangent * 0.22f,
                            anchor - radial * 0.17f,
                            anchor + radial * 0.17f,
                            Size));

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Write("icon_refresh", Size, Size, pixels);
        }

        /// <summary>The lobby mark: a house.</summary>
        private static void WriteHomeMark()
        {
            const int Size = 128;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = 1f - (y + 0.5f) / Size;

                    // v runs 0 at the top to 1 at the bottom here, so the ridge
                    // is the small value. Drawn the other way round the house
                    // came out as a downward arrow, which is a shape a button
                    // this size reads as something else entirely.
                    float alpha = TriangleCoverage(
                        u,
                        v,
                        new Vector2(0.5f, 0.10f),
                        new Vector2(0.04f, 0.48f),
                        new Vector2(0.96f, 0.48f),
                        Size);
                    alpha = Mathf.Max(
                        alpha,
                        BoxCoverage(u, v, 0.19f, 0.44f, 0.81f, 0.90f, Size));
                    // Doorway punched back out, so the shape still reads as a
                    // house at the size a button caption leaves for it.
                    alpha = Mathf.Min(
                        alpha,
                        1f - BoxCoverage(u, v, 0.40f, 0.62f, 0.60f, 0.92f, Size));

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Write("icon_home", Size, Size, pixels);
        }

        /// <summary>The quit mark: a doorway with an arrow leaving it.</summary>
        private static void WriteExitMark()
        {
            const int Size = 128;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;
                    float v = 1f - (y + 0.5f) / Size;

                    float frame = BoxCoverage(u, v, 0.10f, 0.10f, 0.56f, 0.90f, Size);
                    float hollow = BoxCoverage(u, v, 0.20f, 0.20f, 0.60f, 0.80f, Size);
                    float alpha = Mathf.Min(frame, 1f - hollow);

                    alpha = Mathf.Max(
                        alpha,
                        BoxCoverage(u, v, 0.46f, 0.455f, 0.78f, 0.545f, Size));
                    alpha = Mathf.Max(
                        alpha,
                        TriangleCoverage(
                            u,
                            v,
                            new Vector2(0.94f, 0.50f),
                            new Vector2(0.74f, 0.34f),
                            new Vector2(0.74f, 0.66f),
                            Size));

                    pixels[y * Size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Write("icon_exit", Size, Size, pixels);
        }

        private static float BoxCoverage(
            float u,
            float v,
            float minX,
            float minY,
            float maxX,
            float maxY,
            int size)
        {
            float distance = Mathf.Min(
                Mathf.Min(u - minX, maxX - u),
                Mathf.Min(v - minY, maxY - v));
            return Mathf.Clamp01(distance * size + 0.5f);
        }

        /// <summary>
        /// Coverage of a triangle, antialiased on all three edges by treating
        /// the distance to the nearest edge as a one-pixel ramp.
        /// </summary>
        private static float TriangleCoverage(
            float u,
            float v,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            int size)
        {
            var point = new Vector2(u, v);
            float distance = Mathf.Min(
                Mathf.Min(
                    SegmentDistance(point, a, b),
                    SegmentDistance(point, b, c)),
                SegmentDistance(point, c, a));

            float first = Cross(b - a, point - a);
            float second = Cross(c - b, point - b);
            float third = Cross(a - c, point - c);
            bool inside = first >= 0f && second >= 0f && third >= 0f
                || first <= 0f && second <= 0f && third <= 0f;

            return Mathf.Clamp01(
                (inside ? distance : -distance) * size + 0.5f);
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 line = b - a;
            float lengthSquared = line.sqrMagnitude;
            float t = lengthSquared <= 0f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - a, line) / lengthSquared);
            return Vector2.Distance(point, a + line * t);
        }

        private static float Cross(Vector2 left, Vector2 right)
        {
            return left.x * right.y - left.y * right.x;
        }

        /// <summary>
        /// A ring limited to a span of angles. The angular ends are cut without
        /// antialiasing, which is invisible here because both are covered by
        /// the arrowhead or fall in the open gap.
        /// </summary>
        private static float ArcCoverage(
            float u,
            float v,
            float centreX,
            float centreY,
            float radius,
            float thickness,
            float startDegrees,
            float sweepDegrees,
            int size)
        {
            float degrees = Mathf.Atan2(v - centreY, u - centreX) * Mathf.Rad2Deg;
            float offset = Mathf.Repeat(degrees - startDegrees, 360f);
            if (offset > sweepDegrees)
            {
                return 0f;
            }

            return RingCoverage(u, v, centreX, centreY, radius, thickness, size);
        }

        /// <summary>
        /// Coverage of a rectangle whose four corners are cut at 45 degrees.
        /// Antialiased by treating the shape's signed distance as a one-pixel
        /// ramp, which keeps the diagonals from stair-stepping.
        /// </summary>
        private static float ChamferCoverage(
            int x,
            int y,
            int width,
            int height,
            float chamfer)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float dx = Mathf.Min(px, width - px);
            float dy = Mathf.Min(py, height - py);
            float edge = Mathf.Min(dx, dy);
            // The corner constraint is measured along the diagonal, so it has to
            // be scaled before it can be compared with the edge distance.
            float corner = (dx + dy - chamfer) * 0.70710678f;
            float distance = Mathf.Min(edge, corner);
            return Mathf.Clamp01(distance + 0.5f);
        }

        private static float RoundedCoverage(
            int x,
            int y,
            int width,
            int height,
            float radius,
            float inset)
        {
            float px = x + 0.5f - width * 0.5f;
            float py = y + 0.5f - height * 0.5f;
            float halfWidth = width * 0.5f - inset;
            float halfHeight = height * 0.5f - inset;
            radius = Mathf.Max(0.5f, radius);

            float qx = Mathf.Max(Mathf.Abs(px) - (halfWidth - radius), 0f);
            float qy = Mathf.Max(Mathf.Abs(py) - (halfHeight - radius), 0f);
            float outside = Mathf.Sqrt(qx * qx + qy * qy) - radius;
            float inside = Mathf.Min(
                halfWidth - Mathf.Abs(px),
                halfHeight - Mathf.Abs(py));
            float distance = qx > 0f || qy > 0f ? -outside : inside;
            return Mathf.Clamp01(distance + 0.5f);
        }

        private static float EllipseCoverage(
            float u,
            float v,
            float centreX,
            float centreY,
            float radiusX,
            float radiusY,
            int size)
        {
            float dx = (u - centreX) / radiusX;
            float dy = (v - centreY) / radiusY;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            // Converted back to pixels so the one-pixel ramp is uniform.
            float pixels = (1f - distance) * Mathf.Min(radiusX, radiusY) * size;
            return Mathf.Clamp01(pixels + 0.5f);
        }

        private static float CapsuleCoverage(
            float u,
            float v,
            float ax,
            float ay,
            float bx,
            float by,
            float radius,
            int size)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared <= 0f
                ? 0f
                : Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / lengthSquared);
            float cx = ax + dx * t;
            float cy = ay + dy * t;
            float distance = Mathf.Sqrt(
                (u - cx) * (u - cx) + (v - cy) * (v - cy));
            return Mathf.Clamp01((radius - distance) * size + 0.5f);
        }

        private static float RingCoverage(
            float u,
            float v,
            float centreX,
            float centreY,
            float radius,
            float thickness,
            int size)
        {
            float distance = Mathf.Sqrt(
                (u - centreX) * (u - centreX) + (v - centreY) * (v - centreY));
            float offset = Mathf.Abs(distance - radius);
            return Mathf.Clamp01((thickness - offset) * size + 0.5f);
        }

        private static string Write(
            string name,
            int width,
            int height,
            Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            string path = $"{OutputFolder}/{name}.png";
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        /// <summary>
        /// Nine-slice borders have to exceed the corner detail, or stretching a
        /// plate drags its chamfer across the whole button.
        /// </summary>
        private static void ApplySliced(string path, Vector4 border)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException(
                    $"Generated sprite is missing its importer: {path}",
                    path);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteBorder = border;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Color FromHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString($"#{hex}", out Color color))
            {
                throw new ArgumentException($"Not a colour: {hex}", nameof(hex));
            }

            return color;
        }
    }
}
