using TMPro;
using PawliceAndPurrglar.Input;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class QuickSlotView : MonoBehaviour, IItemTooltipContentSource
    {
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text iconGlyph;
        [SerializeField] private Image icon;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private Image disabledOverlay;
        [SerializeField] private Image cooldownOverlay;

        public void Configure(
            TMP_Text configuredKeyLabel,
            TMP_Text configuredQuantityLabel,
            Image configuredIcon,
            Image configuredSelectedFrame,
            Image configuredDisabledOverlay,
            Image configuredCooldownOverlay,
            TMP_Text configuredIconGlyph = null)
        {
            keyLabel = configuredKeyLabel;
            quantityLabel = configuredQuantityLabel;
            iconGlyph = configuredIconGlyph;
            icon = configuredIcon;
            selectedFrame = configuredSelectedFrame;
            disabledOverlay = configuredDisabledOverlay;
            cooldownOverlay = configuredCooldownOverlay;
        }

        /// <summary>
        /// What the shared tooltip says while the cursor is on this slot, from
        /// the last bind. Same arrangement as the bag's cells: the slot carries
        /// finished words and decides nothing about the prop itself.
        /// </summary>
        private ItemTooltipContent tooltip;

        public bool TryGetTooltipContent(out ItemTooltipContent content)
        {
            content = tooltip;
            return content.HasContent;
        }

        public void Bind(QuickSlotViewModel model)
        {
            tooltip = model.Tooltip;
            if (keyLabel != null) keyLabel.text = model.KeyLabel;
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

            if (selectedFrame != null) selectedFrame.enabled = model.Selected;
            if (disabledOverlay != null)
            {
                disabledOverlay.enabled = model.Disabled && !model.Selected;
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.enabled = model.Cooldown01 > 0f;
                cooldownOverlay.fillAmount = model.Cooldown01;
            }
        }
    }
}
