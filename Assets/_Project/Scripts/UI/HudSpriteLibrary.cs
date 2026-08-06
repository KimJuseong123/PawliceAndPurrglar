using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Rounded panel sprites, drawn in code and reused by every HUD panel.
    ///
    /// Drawn rather than imported for one reason: the HUD is assembled by
    /// <see cref="HudRuntimeInstaller"/> and baked into
    /// <c>Assets/Resources/HudCanvas.prefab</c>, and a sprite it references has to
    /// exist wherever that prefab is instantiated. A generated texture is always
    /// there and cannot go missing the way a Resources path can — and the lobby's
    /// sprite factory is an editor script that bakes PNGs for the lobby's paper
    /// palette, which is the wrong colour scheme and the wrong lifetime for this.
    ///
    /// Nine-sliced, so one 64px texture serves a 24px badge and a 900px window.
    /// The border is part of the corner slice, which is what keeps a stretched
    /// panel's outline the same weight as a small one's.
    /// </summary>
    public static class HudSpriteLibrary
    {
        /// <summary>Panel body: near-black navy, like the mockup's windows.</summary>
        public static readonly Color PanelFill = new(0.043f, 0.075f, 0.114f, 0.94f);

        /// <summary>Section body, one step lighter so it reads inside a panel.</summary>
        public static readonly Color SectionFill = new(0.055f, 0.098f, 0.149f, 0.96f);

        /// <summary>An empty cell. Darker than its panel, not lighter.</summary>
        public static readonly Color SlotFill = new(0.031f, 0.063f, 0.098f, 0.92f);

        public static readonly Color Border = new(0.16f, 0.60f, 0.75f, 0.85f);
        public static readonly Color BorderSoft = new(0.14f, 0.38f, 0.50f, 0.60f);
        public static readonly Color Accent = new(0.25f, 0.85f, 1f, 1f);
        public static readonly Color Gold = new(1f, 0.80f, 0.20f, 1f);
        public static readonly Color SellGreen = new(0.13f, 0.72f, 0.38f, 1f);
        public static readonly Color CancelRed = new(0.78f, 0.18f, 0.20f, 1f);
        public static readonly Color HeaderTeal = new(0.06f, 0.30f, 0.40f, 0.95f);

        private const int Resolution = 64;
        private static readonly Dictionary<int, Sprite> Cache = new();

        /// <summary>
        /// A rounded rectangle with a one-pixel-equivalent outline.
        /// </summary>
        /// <param name="fill">Body colour.</param>
        /// <param name="border">Outline colour. Transparent for no outline.</param>
        /// <param name="cornerRadius">Corner radius in sprite pixels.</param>
        /// <param name="borderWidth">Outline thickness in sprite pixels.</param>
        public static Sprite Panel(
            Color fill,
            Color border,
            int cornerRadius = 12,
            int borderWidth = 2)
        {
            int key = HashCode(fill, border, cornerRadius, borderWidth);
            if (Cache.TryGetValue(key, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Sprite created = Build(fill, border, cornerRadius, borderWidth);
            Cache[key] = created;
            return created;
        }

        public static Sprite Window()
        {
            return Panel(PanelFill, Border, 14, 2);
        }

        public static Sprite Section()
        {
            return Panel(SectionFill, BorderSoft, 10, 2);
        }

        public static Sprite Slot()
        {
            return Panel(SlotFill, BorderSoft, 8, 2);
        }

        /// <summary>A filled pill with no outline, for buttons and badges.</summary>
        public static Sprite Solid(Color fill, int cornerRadius = 8)
        {
            return Panel(fill, new Color(0f, 0f, 0f, 0f), cornerRadius, 0);
        }

        /// <summary>An outline with nothing inside, for selection frames.</summary>
        public static Sprite Outline(Color border, int cornerRadius = 8, int borderWidth = 3)
        {
            return Panel(new Color(0f, 0f, 0f, 0f), border, cornerRadius, borderWidth);
        }

        private static Sprite Build(
            Color fill,
            Color border,
            int cornerRadius,
            int borderWidth)
        {
            int radius = Mathf.Clamp(cornerRadius, 0, Resolution / 2);
            int outline = Mathf.Clamp(borderWidth, 0, radius <= 0 ? 8 : radius);
            var texture = new Texture2D(
                Resolution,
                Resolution,
                TextureFormat.RGBA32,
                false)
            {
                name = $"HudPanel_{radius}_{outline}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[Resolution * Resolution];
            float half = Resolution * 0.5f;
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    // Sampled at the pixel centre. Sampling the corner puts the
                    // outline half a pixel off on one side of the sprite and not
                    // the other, which shows up as a panel with one thick edge.
                    float distance = RoundedDistance(
                        new Vector2(x + 0.5f - half, y + 0.5f - half),
                        half,
                        radius);
                    float inside = Mathf.Clamp01(0.5f - distance);
                    float onOutline = outline <= 0
                        ? 0f
                        : Mathf.Clamp01(distance + outline + 0.5f)
                            * inside;
                    Color colour = Color.Lerp(fill, border, onOutline * border.a);
                    colour.a = Mathf.Max(fill.a * inside, border.a * onOutline);
                    pixels[(y * Resolution) + x] = colour;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);

            // The slice has to clear the corner, or a stretched middle drags the
            // curve out into a diagonal. One pixel of margin covers the
            // anti-aliased edge.
            float slice = Mathf.Max(radius + 1, outline + 1);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, Resolution, Resolution),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(slice, slice, slice, slice));
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// Signed distance to a rounded box: negative inside, zero on the edge.
        /// </summary>
        private static float RoundedDistance(
            Vector2 point,
            float half,
            float radius)
        {
            float inner = half - radius;
            var corner = new Vector2(
                Mathf.Abs(point.x) - inner,
                Mathf.Abs(point.y) - inner);
            float outside = new Vector2(
                Mathf.Max(corner.x, 0f),
                Mathf.Max(corner.y, 0f)).magnitude;
            return outside
                + Mathf.Min(Mathf.Max(corner.x, corner.y), 0f)
                - radius;
        }

        private static int HashCode(
            Color fill,
            Color border,
            int cornerRadius,
            int borderWidth)
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + fill.GetHashCode();
                hash = (hash * 31) + border.GetHashCode();
                hash = (hash * 31) + cornerRadius;
                hash = (hash * 31) + borderWidth;
                return hash;
            }
        }
    }
}
