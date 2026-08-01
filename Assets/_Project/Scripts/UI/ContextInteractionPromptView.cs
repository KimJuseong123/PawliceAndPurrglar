using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class ContextInteractionPromptView : MonoBehaviour
    {
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Image holdProgress;

        public void Configure(
            TMP_Text configuredKeyLabel,
            TMP_Text configuredActionLabel,
            Image configuredHoldProgress)
        {
            keyLabel = configuredKeyLabel;
            actionLabel = configuredActionLabel;
            holdProgress = configuredHoldProgress;
        }

        public void Bind(ContextInteractionPromptViewModel model)
        {
            gameObject.SetActive(model.Visible);
            if (!model.Visible) return;
            if (keyLabel != null) keyLabel.text = model.Holding ? "[Hold E]" : model.KeyLabel;
            if (actionLabel != null) actionLabel.text = model.ActionLabel;
            if (holdProgress != null)
            {
                holdProgress.enabled = model.Holding;
                holdProgress.fillAmount = model.HoldProgress01;
            }
        }
    }
}
