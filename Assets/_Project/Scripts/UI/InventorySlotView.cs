using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class InventorySlotView : MonoBehaviour, IItemTooltipContentSource
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
        /// What the shared tooltip says while the cursor is on this cell.
        ///
        /// Stored from the last bind rather than worked out here. The cell knows
        /// what it is drawing and nothing about what a banana does, which is the
        /// only arrangement in which the same prop cannot be described two ways
        /// in two panels.
        /// </summary>
        private ItemTooltipContent tooltip;

        /// <summary>
        /// Refused for an empty cell, and for one holding something with no
        /// words yet. Not keyed on <see cref="HasContent"/>: the merchant's
        /// window draws the officer's own props as deliberately unusable cells,
        /// and "you cannot sell this" is exactly when a player wants to know what
        /// it is.
        /// </summary>
        public bool TryGetTooltipContent(out ItemTooltipContent content)
        {
            content = tooltip;
            return content.HasContent;
        }

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
            tooltip = model.Tooltip;
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
        }
    }
}
