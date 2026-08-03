using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// The pieces the lobby and the result screen are both built from.
    ///
    /// Shared so the two screens cannot drift apart: same plate, same font,
    /// same minimum line box, same rule about never letting a layout group
    /// force-expand a child.
    /// </summary>
    internal static class UiBuildKit
    {
        internal const string FontAssetPath =
            "Assets/Resources/PawsAndLootDefaultFont.asset";

        internal const string ChromeFolder =
            "Assets/_Project/UI/Lobby/Elements";

        internal static readonly Color Ink = Hex("38271F");
        internal static readonly Color PoliceBlue = Hex("3566BE");
        internal static readonly Color ThiefRed = Hex("E63C35");
        internal static readonly Color StartAmber = Hex("F5B53B");
        internal static readonly Color MicPurple = Hex("8A4BA0");
        internal static readonly Color PanelCream = Hex("FFF9F2");
        internal static readonly Color Muted = Hex("A99B8F");
        internal static readonly Color QuitRed = Hex("D94A42");

        /// <summary>
        /// Minimum rect height for a font size. TMP needs more than the point
        /// size for a line, and a rect merely equal to it either clips or, if
        /// anything ever sets Ellipsis, blanks entirely.
        /// </summary>
        internal static float LineBox(float fontSize)
        {
            return Mathf.Ceil(fontSize * 1.45f);
        }

        internal static RectTransform Node(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        internal static T NewGraphic<T>(string name, Transform parent)
            where T : Graphic
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(T));
            child.transform.SetParent(parent, false);
            return child.GetComponent<T>();
        }

        internal static void Stretch(
            RectTransform rect,
            float left = 0f,
            float bottom = 0f,
            float right = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        internal static void Anchor(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        /// <summary>
        /// Every child of a layout group states its own minimum and preferred
        /// size. Without this the group is free to shrink a button below the
        /// height its caption needs.
        /// </summary>
        internal static void Fixed(
            RectTransform rect,
            float width,
            float height,
            float flexibleWidth = 0f)
        {
            LayoutElement element =
                rect.GetComponent<LayoutElement>()
                ?? rect.gameObject.AddComponent<LayoutElement>();
            // A floor rather than the full width: a control may give up a
            // little to a crowded row, but not enough to crop its caption.
            element.minWidth = width * 0.85f;
            element.minHeight = height;
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = 0f;
        }

        /// <summary>
        /// Configures a layout group the way every group in these screens is
        /// configured. Force-expand is what turns a button into a flat bar and
        /// squeezes a field until its caption overlaps its own text.
        /// </summary>
        internal static T Group<T>(
            RectTransform host,
            TextAnchor alignment,
            float spacing)
            where T : HorizontalOrVerticalLayoutGroup
        {
            T group = host.gameObject.AddComponent<T>();
            group.childAlignment = alignment;
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childScaleWidth = false;
            group.childScaleHeight = false;
            return group;
        }

        internal static TMP_Text Text(
            string name,
            Transform parent,
            float size,
            TextAlignmentOptions alignment)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<TMP_Text>();

            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                throw new FileNotFoundException(
                    "The UI font asset is missing. Run "
                    + "'Paws & Loot/UI/Create Role-Aware HUD Prefabs' first, "
                    + "which generates it from DNFBitBitv2.ttf.",
                    FontAssetPath);
            }

            text.font = font;
            text.fontSharedMaterial = font.material;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            // Overflow, never Ellipsis. Under Ellipsis TMP draws nothing at all
            // when the rect is shorter than one line, which is how a set of
            // captions disappeared with no warning of any kind.
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static void Sliced(
            Image image,
            string spriteName,
            float pixelsPerUnitMultiplier)
        {
            image.sprite = Sprite(ChromeFolder, spriteName);
            image.type = Image.Type.Sliced;
            // Raised for short controls: a 26px border on a 36px row would
            // leave no middle at all and Unity would shrink the corners to fit,
            // rounding the plate differently on every row height.
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        }

        /// <summary>
        /// A button: dark plate behind, tinted face in front, caption and marks
        /// on top. Two layers because tinting a single plate drags its outline
        /// towards the button colour and the disabled tint washes it out.
        /// </summary>
        internal static Button Plate(
            string name,
            Transform parent,
            Color fillColor,
            Color textColor,
            string caption,
            float fontSize,
            float pixelsPerUnitMultiplier,
            string markSprite,
            float height)
        {
            RectTransform root = Node(name, parent);
            var button = root.gameObject.AddComponent<Button>();

            Image outline = NewGraphic<Image>("Outline", root);
            Stretch((RectTransform)outline.transform);
            Sliced(outline, "ui_plate_outline", pixelsPerUnitMultiplier);
            outline.color = Color.white;
            // The hit target. The fill sits on top but does not take raycasts,
            // so the whole plate reacts including its bevel.
            outline.raycastTarget = true;

            float inset = Mathf.Max(3f, height * 0.05f);
            Image fill = NewGraphic<Image>("Fill", root);
            Stretch((RectTransform)fill.transform, inset, inset, inset, inset);
            Sliced(fill, "ui_plate_fill", pixelsPerUnitMultiplier);
            fill.color = fillColor;
            fill.raycastTarget = false;
            button.targetGraphic = fill;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var marks = new List<Graphic>();
            TMP_Text label = Text(
                "Label",
                root,
                fontSize,
                TextAlignmentOptions.Center);
            label.text = caption;
            label.color = textColor;
            // No vertical inset: on short rows the caption's line box is taller
            // than what an inset rect would leave, and TMP would trim it.
            Stretch((RectTransform)label.transform, 12f, 0f, 12f, 0f);
            marks.Add(label);

            if (!string.IsNullOrEmpty(markSprite))
            {
                float markSize = height * 0.30f;
                marks.Add(Mark(root, markSprite, textColor, markSize, true));
                marks.Add(Mark(root, markSprite, textColor, markSize, false));
            }

            var motion = root.gameObject.AddComponent<LobbyButtonMotion>();
            motion.Configure(root, fill, fillColor, marks.ToArray());
            return button;
        }

        internal static Image Mark(
            RectTransform root,
            string spriteName,
            Color tint,
            float size,
            bool left)
        {
            Image mark = NewGraphic<Image>(left ? "MarkLeft" : "MarkRight", root);
            float edge = left ? 0f : 1f;
            Anchor(
                (RectTransform)mark.transform,
                new Vector2(edge, 0.5f),
                new Vector2(edge, 0.5f),
                new Vector2(edge, 0.5f),
                new Vector2(left ? 30f : -30f, 0f),
                new Vector2(size, size));
            mark.sprite = Sprite(ChromeFolder, spriteName);
            mark.color = tint;
            mark.preserveAspect = true;
            mark.raycastTarget = false;
            return mark;
        }

        internal static Sprite Sprite(string folder, string name)
        {
            string path = $"{folder}/{name}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new FileNotFoundException(
                    $"UI sprite is missing: {path}. Run the matching "
                    + "'Extract ... Art' and 'Generate Lobby Chrome Sprites' "
                    + "commands first.",
                    path);
            }

            return sprite;
        }

        internal static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString($"#{value}", out Color color))
            {
                throw new ArgumentException(
                    $"Not a colour: {value}",
                    nameof(value));
            }

            return color;
        }
    }
}
