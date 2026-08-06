using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Puts a rounded sprite on an <see cref="Image"/> at runtime.
    ///
    /// It has to happen at runtime. The HUD is assembled by an editor script and
    /// saved to <c>Assets/Resources/HudCanvas.prefab</c>, and a sprite generated
    /// in memory is not an asset — assigning it in the editor writes a reference
    /// to nothing, and the prefab loads with <c>sprite == null</c>. An
    /// <c>Image</c> with no sprite is not "no decoration", it is a **white
    /// rectangle**, so the failure is loud in the wrong place: the panel looks
    /// like a bug in the layout rather than in the asset pipeline. The same
    /// mistake in <c>ISSUE-050</c> cost a playtest.
    ///
    /// So the editor writes down *which* sprite it wants — a shape kind and two
    /// colours, all of them plain serialized values — and this asks for it on
    /// <c>OnEnable</c> on the machine that is going to draw it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class HudPanelSkin : MonoBehaviour
    {
        public enum Shape
        {
            Window = 0,
            Section = 1,
            Slot = 2,
            Solid = 3,
            Outline = 4
        }

        [SerializeField] private Shape shape = Shape.Window;
        [SerializeField] private Color fill = Color.clear;
        [SerializeField] private Color border = Color.clear;
        [SerializeField] private int cornerRadius;
        [SerializeField] private int borderWidth = 2;

        public void Configure(
            Shape configuredShape,
            Color configuredFill = default,
            Color configuredBorder = default,
            int configuredCornerRadius = 0,
            int configuredBorderWidth = 2)
        {
            shape = configuredShape;
            fill = configuredFill;
            border = configuredBorder;
            cornerRadius = configuredCornerRadius;
            borderWidth = configuredBorderWidth;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = Resolve();
            image.type = Image.Type.Sliced;

            // White, because the colours are baked into the sprite: a body colour
            // and an outline colour cannot both come out of one tint. Leaving the
            // authored tint on would multiply them twice.
            image.color = Color.white;
        }

        private Sprite Resolve()
        {
            int radius = cornerRadius;
            switch (shape)
            {
                case Shape.Section:
                    return radius > 0
                        ? HudSpriteLibrary.Panel(
                            fill.a > 0f ? fill : HudSpriteLibrary.SectionFill,
                            border.a > 0f ? border : HudSpriteLibrary.BorderSoft,
                            radius,
                            borderWidth)
                        : HudSpriteLibrary.Section();
                case Shape.Slot:
                    return radius > 0
                        ? HudSpriteLibrary.Panel(
                            fill.a > 0f ? fill : HudSpriteLibrary.SlotFill,
                            border.a > 0f ? border : HudSpriteLibrary.BorderSoft,
                            radius,
                            borderWidth)
                        : HudSpriteLibrary.Slot();
                case Shape.Solid:
                    return HudSpriteLibrary.Solid(
                        fill,
                        radius > 0 ? radius : 8);
                case Shape.Outline:
                    return HudSpriteLibrary.Outline(
                        border.a > 0f ? border : HudSpriteLibrary.Accent,
                        radius > 0 ? radius : 8,
                        borderWidth);
                default:
                    return radius > 0
                        ? HudSpriteLibrary.Panel(
                            fill.a > 0f ? fill : HudSpriteLibrary.PanelFill,
                            border.a > 0f ? border : HudSpriteLibrary.Border,
                            radius,
                            borderWidth)
                        : HudSpriteLibrary.Window();
            }
        }
    }
}
