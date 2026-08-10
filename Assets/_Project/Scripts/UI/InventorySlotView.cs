using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class InventorySlotView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text itemNameLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text priceLabel;
        [SerializeField] private TMP_Text iconGlyph;
        [SerializeField] private Image icon;
        [SerializeField] private Image priceIcon;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private Image disabledOverlay;
        [SerializeField] private GameObject newBadge;

        /// <summary>
        /// Which cell of which grid this is, so a drag knows what it picked up.
        ///
        /// Written by the builder rather than read from the sibling index: the
        /// grid also holds the four prop quick slots at the front, so a cell's
        /// place on screen and its place in the bag are different numbers.
        /// </summary>
        public int CellIndex { get; private set; } = -1;

        /// <summary>
        /// Whether this cell is holding something, as of the last bind.
        ///
        /// The drag needs it. It used to decide by looking for an icon sprite, and
        /// only six of the thirty-odd loot definitions have artwork — so every
        /// other piece drew a letter instead and **could not be picked up at all**.
        /// The cell looked full, the cursor did nothing, and the feature read as
        /// broken rather than as unfinished art.
        /// </summary>
        public bool HasContent { get; private set; }

        /// <summary>
        /// What the hover window says about this cell, as of the last bind.
        /// </summary>
        public string TooltipTitle { get; private set; } = string.Empty;

        public string TooltipBody { get; private set; } = string.Empty;

        private InventoryTooltipView _tooltip;
        private bool _pointerInside;

        public void SetCellIndex(int index)
        {
            CellIndex = index;
        }

        public void Configure(
            TMP_Text configuredKeyLabel,
            TMP_Text configuredQuantityLabel,
            Image configuredIcon,
            Image configuredSelectedFrame,
            Image configuredDisabledOverlay,
            TMP_Text configuredIconGlyph = null,
            TMP_Text configuredItemNameLabel = null,
            TMP_Text configuredPriceLabel = null,
            Image configuredPriceIcon = null,
            GameObject configuredNewBadge = null)
        {
            newBadge = configuredNewBadge;
            keyLabel = configuredKeyLabel;
            itemNameLabel = configuredItemNameLabel;
            quantityLabel = configuredQuantityLabel;
            priceLabel = configuredPriceLabel;
            iconGlyph = configuredIconGlyph;
            icon = configuredIcon;
            priceIcon = configuredPriceIcon;
            selectedFrame = configuredSelectedFrame;
            disabledOverlay = configuredDisabledOverlay;
        }

        public void Bind(InventorySlotViewModel model)
        {
            HasContent = !model.Disabled;
            TooltipTitle = model.TooltipTitle;
            TooltipBody = model.TooltipBody;
            if (keyLabel != null) keyLabel.text = model.KeyLabel;
            if (itemNameLabel != null)
            {
                itemNameLabel.text = model.ItemName;
                itemNameLabel.enabled = !string.IsNullOrWhiteSpace(model.ItemName);
            }

            if (quantityLabel != null)
            {
                quantityLabel.text = model.Quantity > 0
                    ? model.Quantity.ToString()
                    : string.Empty;
            }

            if (icon != null)
            {
                icon.sprite = model.Icon;
                icon.enabled = model.Icon != null;
            }

            if (iconGlyph != null)
            {
                iconGlyph.text = model.IconGlyph;
                iconGlyph.enabled = !string.IsNullOrWhiteSpace(model.IconGlyph);
            }

            if (priceLabel != null)
            {
                priceLabel.text = model.HasPrice ? model.Price.ToString() : string.Empty;
                priceLabel.enabled = model.HasPrice;
            }

            if (priceIcon != null)
            {
                priceIcon.sprite = model.PriceIcon;
                priceIcon.enabled = model.HasPrice && model.PriceIcon != null;
            }

            if (selectedFrame != null) selectedFrame.enabled = model.Selected;
            if (disabledOverlay != null) disabledOverlay.enabled = model.Disabled;

            // Deactivated rather than made transparent. A badge kept alive at zero
            // alpha still answers "is the badge there?" with yes, which is how the
            // result screen ended up asserting four labels that nobody could read
            // (ISSUE-050).
            if (newBadge != null && newBadge.activeSelf != model.IsNew)
            {
                newBadge.SetActive(model.IsNew);
            }

            // The cursor may already be sitting on this cell. The bag rebinds
            // every frame the player has it open, so a cell that emptied while
            // being hovered — dragged away, sold, handed to the cat — would
            // otherwise keep describing what used to be in it.
            if (_pointerInside)
            {
                ShowTooltip();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            HideTooltip();
        }

        /// <summary>
        /// Closes the window when this cell goes away with the cursor still on
        /// it.
        ///
        /// <c>OnPointerExit</c> does not arrive in that case — the bag is closed
        /// with a key, and the panel is deactivated from under the pointer — so
        /// without this the window is left hanging over the game with the bag
        /// gone from behind it.
        /// </summary>
        private void OnDisable()
        {
            _pointerInside = false;
            HideTooltip();
        }

        private void ShowTooltip()
        {
            if (string.IsNullOrWhiteSpace(TooltipTitle))
            {
                HideTooltip();
                return;
            }

            if (_tooltip == null)
            {
                _tooltip = InventoryTooltipView.FindOrCreate(transform);
            }

            if (_tooltip != null)
            {
                _tooltip.Show(
                    TooltipTitle,
                    TooltipBody,
                    transform as RectTransform);
            }
        }

        /// <summary>
        /// Closes the window if it is still there.
        ///
        /// Written with an explicit <c>!=</c> rather than <c>?.</c> because the
        /// two do not agree about a destroyed object: Unity overloads the
        /// operator to answer null once the object is gone, and the null-
        /// conditional does not use the overload. This runs during scene
        /// teardown, where the window may already have been destroyed while this
        /// cell is still being switched off, and <c>?.</c> would go straight into
        /// a <c>MissingReferenceException</c> there.
        /// </summary>
        private void HideTooltip()
        {
            if (_tooltip != null)
            {
                _tooltip.Hide();
            }
        }
    }
}
