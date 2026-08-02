using TMPro;
using PawsAndLoot.Input;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class QuickSlotView : MonoBehaviour
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

        public void Bind(QuickSlotViewModel model)
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
