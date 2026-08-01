using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class AnimalCommandShortcutView : MonoBehaviour
    {
        [SerializeField] private TMP_Text modifierLabel;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text commandLabel;
        [SerializeField] private Image disabledOverlay;

        public void Configure(
            TMP_Text configuredModifierLabel,
            TMP_Text configuredKeyLabel,
            TMP_Text configuredCommandLabel,
            Image configuredDisabledOverlay)
        {
            modifierLabel = configuredModifierLabel;
            keyLabel = configuredKeyLabel;
            commandLabel = configuredCommandLabel;
            disabledOverlay = configuredDisabledOverlay;
        }

        public void Bind(AnimalCommandShortcutViewModel model)
        {
            if (modifierLabel != null) modifierLabel.text = model.Modifier;
            if (keyLabel != null) keyLabel.text = model.Key;
            if (commandLabel != null) commandLabel.text = model.Command;
            if (disabledOverlay != null) disabledOverlay.enabled = model.Disabled;
        }
    }
}
