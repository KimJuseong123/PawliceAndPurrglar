using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The little window that says what the cell under the cursor is.
    ///
    /// A bag cell is 70px and draws an icon and a count. The name and the
    /// effect will not fit in it — that was tried, and a cell saying three
    /// things says none of them, which is why the in-cell name label is left
    /// blank. So the name lives here, next to the cursor, and only while the
    /// player is asking for it.
    ///
    /// Made at runtime rather than baked into <c>HudCanvas.prefab</c>, the same
    /// way the raccoon's ledger is. Two reasons, both of them scars: the panel
    /// wants generated sprites, and a sprite generated in memory is not an
    /// asset — an editor script that assigns one writes a reference to nothing
    /// and the prefab loads with a **white rectangle** where the window should
    /// be (<c>ISSUE-050</c>). Building it on the machine that draws it also
    /// means the bag keeps working without anybody running a rebuild menu.
    ///
    /// Nothing here takes a raycast. A tooltip that appears under the cursor
    /// and can be hit steals the pointer from the cell that opened it, so the
    /// cell gets an exit, the tooltip closes, the cell gets an enter — a
    /// flicker at frame rate rather than a tooltip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryTooltipView : MonoBehaviour
    {
        /// <summary>
        /// Wide enough for an effect line to break twice rather than five
        /// times, and narrow enough to sit beside the bag without covering it.
        /// </summary>
        public const float Width = 300f;

        private const string ObjectName = "Item Tooltip";
        private const float SidePadding = 16f;
        private const float VerticalPadding = 13f;
        private const float TitleFontSize = 18f;
        private const float BodyFontSize = 15f;

        /// <summary>
        /// How much rect one line of text needs, as a multiple of its point
        /// size.
        ///
        /// TMP draws **nothing at all** when a rect is shorter than one line —
        /// not a clipped line, not an ellipsis, nothing (<c>ISSUE-047</c>). The
        /// height here is computed rather than guessed for exactly that reason,
        /// and this is the floor used everywhere else in the HUD.
        /// </summary>
        private const float LineHeightFactor = 1.45f;

        private const float TitleHeight = TitleFontSize * LineHeightFactor;
        private const float BodyMinimumHeight = BodyFontSize * LineHeightFactor;
        private const float TitleToBodyGap = 6f;

        /// <summary>Clearance between the hovered cell and this window.</summary>
        private const float SlotGap = 12f;

        /// <summary>Clearance between this window and the edge of the screen.</summary>
        private const float ScreenMargin = 12f;

        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;

        public string Title => titleLabel != null ? titleLabel.text : string.Empty;

        public string Body => bodyLabel != null ? bodyLabel.text : string.Empty;

        public bool IsShowing => gameObject.activeSelf;

        /// <summary>
        /// The one tooltip belonging to the canvas <paramref name="child"/> is
        /// under, made if it is not there yet.
        ///
        /// One per canvas, found by walking up from the cell rather than held in
        /// a static. A static survives a scene unload and hands the next match a
        /// destroyed object; the hierarchy cannot go stale, and it is the same
        /// reason <c>InventorySlotDragHandler</c> finds its controller this way
        /// instead of being handed a delegate.
        /// </summary>
        public static InventoryTooltipView FindOrCreate(Transform child)
        {
            if (child == null)
            {
                return null;
            }

            Canvas canvas = child.GetComponentInParent<Canvas>();
            Transform root = canvas != null
                ? canvas.rootCanvas.transform
                : child.root;
            if (root == null)
            {
                return null;
            }

            InventoryTooltipView existing =
                root.GetComponentInChildren<InventoryTooltipView>(true);
            return existing != null ? existing : Build(root);
        }

        public void Configure(TMP_Text configuredTitle, TMP_Text configuredBody)
        {
            titleLabel = configuredTitle;
            bodyLabel = configuredBody;
        }

        /// <summary>
        /// Puts the window beside <paramref name="target"/> with this text, or
        /// closes it when there is nothing to say.
        ///
        /// An empty title closes it rather than showing an empty frame. A
        /// tooltip over an empty cell would be the screen answering a question
        /// nobody asked.
        /// </summary>
        public void Show(string title, string body, RectTransform target)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                Hide();
                return;
            }

            string trimmedBody = string.IsNullOrWhiteSpace(body)
                ? string.Empty
                : body.Trim();
            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = trimmedBody;

                // Deactivated rather than emptied. An enabled label with no text
                // still owns its share of the height, so a loot cell — which has
                // a name and no effect — would open a window with a blank half.
                bodyLabel.enabled = trimmedBody.Length > 0;
            }

            gameObject.SetActive(true);

            // Above every panel, including the bag it is describing. Drawn by
            // sibling order, so it has to be asked for again each time: the bag
            // and the exchange screen are created after this window is.
            transform.SetAsLastSibling();
            Layout(trimmedBody);
            Place(target);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Grows the window to fit the text instead of clipping it.
        ///
        /// The body is measured rather than assumed: an effect line wraps to one
        /// row for the rock and three for the sensor light, and a fixed height
        /// would either cut the long one or leave the short one in a half-empty
        /// box.
        /// </summary>
        private void Layout(string body)
        {
            float inner = Width - (SidePadding * 2f);
            float bodyHeight = 0f;
            if (bodyLabel != null && body.Length > 0)
            {
                bodyHeight = Mathf.Max(
                    BodyMinimumHeight,
                    bodyLabel.GetPreferredValues(body, inner, 0f).y);
            }

            float height = (VerticalPadding * 2f) + TitleHeight
                + (bodyHeight > 0f ? TitleToBodyGap + bodyHeight : 0f);
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(Width, height);

            if (titleLabel != null)
            {
                HudRuntimeInstaller.Anchor(
                    titleLabel.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, -(VerticalPadding + (TitleHeight / 2f))),
                    new Vector2(-(SidePadding * 2f), TitleHeight));
            }

            if (bodyLabel != null)
            {
                HudRuntimeInstaller.Anchor(
                    bodyLabel.rectTransform,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(
                        0f,
                        -(VerticalPadding + TitleHeight + TitleToBodyGap
                            + (bodyHeight / 2f))),
                    new Vector2(-(SidePadding * 2f), bodyHeight));
            }
        }

        /// <summary>
        /// Beside the cell, and inside the screen.
        ///
        /// To the right by preference and flipped to the left when the right
        /// would run off the edge, because the bag sits against the left of the
        /// screen and the exchange screen sits beside it. The clamp is not
        /// decoration: the bottom row of a 25-cell grid is 700px down, and a
        /// window hung below it from there is under the screen.
        /// </summary>
        private void Place(RectTransform target)
        {
            if (transform.parent is not RectTransform canvasRect || target == null)
            {
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 topRight = canvasRect.InverseTransformPoint(corners[2]);

            Rect canvas = canvasRect.rect;
            float width = rect.sizeDelta.x;
            float height = rect.sizeDelta.y;
            float x = topRight.x + SlotGap;
            if (x + width > canvas.xMax - ScreenMargin)
            {
                x = bottomLeft.x - SlotGap - width;
            }

            x = Mathf.Clamp(
                x,
                canvas.xMin + ScreenMargin,
                Mathf.Max(canvas.xMin + ScreenMargin, canvas.xMax - ScreenMargin - width));
            float y = Mathf.Clamp(
                topRight.y,
                Mathf.Min(canvas.yMax - ScreenMargin, canvas.yMin + ScreenMargin + height),
                canvas.yMax - ScreenMargin);

            // Measured against the canvas rect and then written as an offset from
            // its centre, because that is where an anchor of 0.5 puts the origin
            // and a canvas whose pivot is not 0.5 would otherwise land the window
            // half a screen away.
            rect.anchoredPosition = new Vector2(x, y) - canvas.center;
        }

        private static InventoryTooltipView Build(Transform root)
        {
            GameObject panel = HudRuntimeInstaller.CreatePanel(
                root,
                ObjectName,
                new Vector2(Width, 80f));
            HudRuntimeInstaller.SkinWindow(panel);
            panel.GetComponent<Image>().raycastTarget = false;

            TMP_Text title = HudRuntimeInstaller.CreateText(
                panel.transform,
                "Title",
                string.Empty,
                TitleFontSize,
                TextAlignmentOptions.TopLeft);
            title.color = HudSpriteLibrary.Accent;

            TMP_Text body = HudRuntimeInstaller.CreateText(
                panel.transform,
                "Body",
                string.Empty,
                BodyFontSize,
                TextAlignmentOptions.TopLeft);
            body.color = new Color(0.86f, 0.93f, 0.98f, 0.94f);

            InventoryTooltipView view = panel.AddComponent<InventoryTooltipView>();
            view.Configure(title, body);
            panel.SetActive(false);
            return view;
        }
    }
}
