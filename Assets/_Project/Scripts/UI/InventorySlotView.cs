using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text iconGlyph;
        [SerializeField] private Image icon;
        [SerializeField] private Image selectedFrame;
        [SerializeField] private Image disabledOverlay;

        public void Configure(
            TMP_Text configuredKeyLabel,
            TMP_Text configuredQuantityLabel,
            Image configuredIcon,
            Image configuredSelectedFrame,
            Image configuredDisabledOverlay,
            TMP_Text configuredIconGlyph = null)
        {
            keyLabel = configuredKeyLabel;
            quantityLabel = configuredQuantityLabel;
            iconGlyph = configuredIconGlyph;
            icon = configuredIcon;
            selectedFrame = configuredSelectedFrame;
            disabledOverlay = configuredDisabledOverlay;
        }

        public void Bind(InventorySlotViewModel model)
        {
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
            if (disabledOverlay != null) disabledOverlay.enabled = model.Disabled;
        }
    }
}
