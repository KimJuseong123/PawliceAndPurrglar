using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The one hover tooltip, shared by every item slot on the canvas.
    ///
    /// One instance rather than one per slot. Thirty cells with thirty hidden
    /// panels is thirty places for the same layout bug, thirty rects the
    /// CanvasScaler has to lay out, and — the part that actually bites — thirty
    /// objects the pointer can enter, which is how a tooltip ends up stealing
    /// the exit event that was supposed to take it down.
    ///
    /// The panel is a child that is switched off while hidden, and this
    /// component sits on an always-active holder above it. That split matters:
    /// the hover delay is counted here, so if the timer lived on the panel it
    /// would only run while the panel was already visible.
    ///
    /// Nothing in here can be raycast — no graphic on the holder, raycasts off
    /// on every child, and a <see cref="CanvasGroup"/> that neither blocks nor
    /// interacts. A tooltip that can be hit is a tooltip that sends the cursor a
    /// pointer-exit for the slot it is describing, and then it flickers at
    /// exactly the rate the mouse moves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemTooltipView : MonoBehaviour
    {
        /// <summary>
        /// How long the cursor has to rest on a slot before the panel appears.
        ///
        /// Without it, dragging the cursor across a row of cells flashes a
        /// different tooltip per cell. A fifth of a second is under the time it
        /// takes to decide to look at something and over the time it takes to
        /// pass over it.
        /// </summary>
        public const float HoverDelaySeconds = 0.20f;

        /// <summary>Fade in only, and short. This is a panel read mid-chase.</summary>
        public const float FadeSeconds = 0.10f;

        /// <summary>
        /// How long after a tooltip goes down a new slot may claim it without
        /// waiting again.
        ///
        /// Moving from one cell to the next raises exit-then-enter in the same
        /// frame, so without this the brief says two contradictory things: the
        /// tooltip must hide on exit, and it must swap instantly between
        /// neighbours. This makes both true — a hop to another cell swaps, and a
        /// cursor that leaves for open space is simply gone.
        /// </summary>
        public const float SwapGraceSeconds = 0.12f;

        /// <summary>
        /// Fixed width. Height follows the text.
        ///
        /// A tooltip that also grew sideways would be a different shape per item
        /// and would have to be re-aimed for every one of them.
        /// </summary>
        public const float PanelWidth = 320f;

        /// <summary>
        /// Where the panel's top-left corner sits relative to the cursor.
        ///
        /// Down and to the right, and far enough that the arrow never covers the
        /// first character. Flipped rather than shrunk when it would leave the
        /// canvas.
        /// </summary>
        public static readonly Vector2 CursorOffset = new(26f, -16f);

        [SerializeField] private RectTransform panel;
        [SerializeField] private CanvasGroup fade;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text usageLabel;

        private Canvas hostCanvas;
        private RectTransform hostRect;
        private IItemTooltipRequester pending;
        private IItemTooltipRequester shown;
        private ItemTooltipContent current;
        private float pendingSeconds;
        private float fadeSeconds;
        private float graceSeconds;
        private Vector2 lastPointer;
        private bool dragging;

        public bool IsShowing => shown != null;

        /// <summary>Which requester the panel is currently describing.</summary>
        public IItemTooltipRequester ShownRequester => shown;

        public void Configure(
            RectTransform configuredPanel,
            CanvasGroup configuredFade,
            Image configuredIcon,
            TMP_Text configuredName,
            TMP_Text configuredCategory,
            TMP_Text configuredDescription,
            TMP_Text configuredUsage)
        {
            panel = configuredPanel;
            fade = configuredFade;
            icon = configuredIcon;
            nameLabel = configuredName;
            categoryLabel = configuredCategory;
            descriptionLabel = configuredDescription;
            usageLabel = configuredUsage;
            HideImmediately();
        }

        /// <summary>
        /// The tooltip belonging to whatever canvas <paramref name="context"/> is
        /// on, or null if that canvas has none.
        ///
        /// Found by walking up to the root canvas rather than through a static
        /// instance. A static would leak between Play Mode tests the way
        /// <c>LocalPlayerRoleSelector</c>'s role did in <c>ISSUE-054</c>, and it
        /// would tie every slot to whichever HUD happened to wake up last.
        /// </summary>
        public static ItemTooltipView Find(Component context)
        {
            if (context == null)
            {
                return null;
            }

            Canvas canvas = context.GetComponentInParent<Canvas>();
            Transform root = canvas != null
                ? canvas.rootCanvas.transform
                : context.transform.root;
            return root == null
                ? null
                : root.GetComponentInChildren<ItemTooltipView>(true);
        }

        /// <summary>
        /// Asks for the tooltip on behalf of <paramref name="requester"/>.
        ///
        /// Shows at once if another slot was just showing one, otherwise starts
        /// the hover delay. Never shows while a drag is in progress.
        /// </summary>
        public void RequestShow(
            IItemTooltipRequester requester,
            Vector2 pointerPosition)
        {
            lastPointer = pointerPosition;
            if (requester == null || dragging)
            {
                return;
            }

            if (ReferenceEquals(shown, requester))
            {
                return;
            }

            if (shown != null || graceSeconds > 0f)
            {
                ShowNow(requester);
                return;
            }

            if (ReferenceEquals(pending, requester))
            {
                return;
            }

            pending = requester;
            pendingSeconds = 0f;
        }

        /// <summary>
        /// Withdraws <paramref name="requester"/>'s claim. Ignored if some other
        /// slot has since taken the tooltip over.
        /// </summary>
        public void CancelShow(IItemTooltipRequester requester)
        {
            if (requester != null
                && !ReferenceEquals(pending, requester)
                && !ReferenceEquals(shown, requester))
            {
                return;
            }

            bool wasShowing = shown != null;
            HideImmediately();
            graceSeconds = wasShowing ? SwapGraceSeconds : 0f;
        }

        public void HideImmediately()
        {
            pending = null;
            shown = null;
            pendingSeconds = 0f;
            fadeSeconds = 0f;
            graceSeconds = 0f;
            if (fade != null)
            {
                fade.alpha = 0f;
            }

            if (panel != null && panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Suppresses the tooltip while a slot is being dragged.
        ///
        /// Needed because the event system keeps sending enter and exit through a
        /// drag: without this, dragging a banana across the bag pops a tooltip
        /// for every cell it passes over, describing items the player is not
        /// looking at while their own item is under the cursor.
        /// </summary>
        public void SetDragging(bool value)
        {
            dragging = value;
            if (value)
            {
                HideImmediately();
            }
        }

        /// <summary>
        /// Where the panel's top-left corner goes, in the canvas's own space,
        /// with the origin at the canvas centre.
        ///
        /// Flips to the other side of the cursor before it would leave the
        /// canvas, then clamps. Both, in that order: flipping alone fails for a
        /// panel taller than the space either side of the cursor, and clamping
        /// alone slides the panel under the cursor instead of moving it away.
        ///
        /// Static and pure so the corners can be tested without a screen.
        /// </summary>
        public static Vector2 ResolvePanelPosition(
            Vector2 cursor,
            Vector2 panelSize,
            Vector2 canvasSize,
            Vector2 offset)
        {
            float left = canvasSize.x * -0.5f;
            float right = canvasSize.x * 0.5f;
            float bottom = canvasSize.y * -0.5f;
            float top = canvasSize.y * 0.5f;

            float x = cursor.x + offset.x;
            if (x + panelSize.x > right)
            {
                x = cursor.x - offset.x - panelSize.x;
            }

            float y = cursor.y + offset.y;
            if (y - panelSize.y < bottom)
            {
                y = cursor.y - offset.y + panelSize.y;
            }

            return new Vector2(
                Mathf.Clamp(x, left, Mathf.Max(left, right - panelSize.x)),
                Mathf.Clamp(y, Mathf.Min(top, bottom + panelSize.y), top));
        }

        private void Awake()
        {
            HideImmediately();
        }

        private void OnEnable()
        {
            HideImmediately();
        }

        private void OnDisable()
        {
            HideImmediately();
        }

        private void Update()
        {
            float step = Time.unscaledDeltaTime;
            if (graceSeconds > 0f)
            {
                graceSeconds = Mathf.Max(0f, graceSeconds - step);
            }

            if (pending != null && !pending.IsTooltipRequesterAlive)
            {
                pending = null;
                pendingSeconds = 0f;
            }

            if (shown == null)
            {
                if (pending == null)
                {
                    return;
                }

                pendingSeconds += step;
                if (pendingSeconds >= HoverDelaySeconds)
                {
                    ShowNow(pending);
                }

                return;
            }

            // The panel closing under the cursor sends no pointer-exit, and
            // neither does a slot emptying while it is being described. Both are
            // caught by asking rather than by waiting to be told.
            ItemTooltipContent live = default;
            bool stillWorthShowing = shown.IsTooltipRequesterAlive
                && shown.TryGetTooltipContent(out live)
                && live.HasContent;
            if (!stillWorthShowing)
            {
                HideImmediately();
                return;
            }

            // Re-read while the tooltip is up, so a count that changes under the
            // cursor is not a stale panel — but only write to TMP when the words
            // differ. Assigning the same four strings every frame would dirty
            // four text meshes for nothing.
            if (!live.SaysTheSameAs(current))
            {
                ApplyContent(live);
            }

            if (fade != null && fade.alpha < 1f)
            {
                fadeSeconds += step;
                fade.alpha = FadeSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(fadeSeconds / FadeSeconds);
            }
        }

        private void LateUpdate()
        {
            if (shown != null)
            {
                UpdatePanelPosition();
            }
        }

        private void ShowNow(IItemTooltipRequester requester)
        {
            if (requester == null
                || !requester.IsTooltipRequesterAlive
                || !requester.TryGetTooltipContent(out ItemTooltipContent content)
                || !content.HasContent)
            {
                HideImmediately();
                return;
            }

            pending = null;
            pendingSeconds = 0f;
            graceSeconds = 0f;
            shown = requester;
            fadeSeconds = 0f;
            if (fade != null)
            {
                fade.alpha = 0f;
            }

            if (panel != null && !panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(true);
            }

            // Moved to the end of the canvas as it appears, not once at build
            // time. Sibling order is draw order, and the merchant's window is
            // added to the same canvas at runtime — a tooltip that only claimed
            // the top on the frame the HUD was assembled would be drawn behind
            // any panel created after it.
            if (transform.parent != null
                && transform.GetSiblingIndex() != transform.parent.childCount - 1)
            {
                transform.SetAsLastSibling();
            }

            ApplyContent(content);
            UpdatePanelPosition();
        }

        private void ApplyContent(in ItemTooltipContent content)
        {
            current = content;
            if (icon != null)
            {
                icon.sprite = content.Icon;
                icon.enabled = content.Icon != null;
            }

            if (nameLabel != null)
            {
                nameLabel.text = content.ItemName;
            }

            if (categoryLabel != null)
            {
                categoryLabel.text = content.Category;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = content.Description;
            }

            // Deactivated rather than blanked, so the vertical layout closes the
            // gap. A row kept alive with an empty string leaves a band of panel
            // under the description that reads as a rendering fault.
            if (usageLabel != null)
            {
                bool hasHint = !string.IsNullOrWhiteSpace(content.UsageHint);
                usageLabel.text = content.UsageHint;
                if (usageLabel.gameObject.activeSelf != hasHint)
                {
                    usageLabel.gameObject.SetActive(hasHint);
                }
            }

            // Forced now rather than left for the end of frame. The height is
            // what decides whether the panel flips above the cursor, and a
            // height one frame out of date puts the first frame of a tall
            // tooltip off the bottom of the screen.
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            }
        }

        private void UpdatePanelPosition()
        {
            if (panel == null || !ResolveCanvas())
            {
                return;
            }

            Vector2 pointer = ReadPointer();
            Camera eventCamera = hostCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : hostCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    hostRect,
                    pointer,
                    eventCamera,
                    out Vector2 local))
            {
                return;
            }

            panel.anchoredPosition = ResolvePanelPosition(
                local,
                panel.rect.size,
                hostRect.rect.size,
                CursorOffset);
        }

        private bool ResolveCanvas()
        {
            if (hostCanvas == null)
            {
                hostCanvas = GetComponentInParent<Canvas>();
                hostCanvas = hostCanvas != null ? hostCanvas.rootCanvas : null;
                hostRect = hostCanvas != null
                    ? hostCanvas.GetComponent<RectTransform>()
                    : null;
            }

            return hostCanvas != null && hostRect != null;
        }

        /// <summary>
        /// Where the cursor is now.
        ///
        /// Read from the Input System, which is what the rest of the project
        /// uses, and falls back to the position the pointer event arrived with —
        /// a machine with no mouse device still gets the tooltip where the slot
        /// was clicked from.
        /// </summary>
        private Vector2 ReadPointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return lastPointer;
            }

            lastPointer = mouse.position.ReadValue();
            return lastPointer;
        }
    }
}
