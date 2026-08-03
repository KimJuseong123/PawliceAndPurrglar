using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class ContextInteractionPromptView : MonoBehaviour
    {
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Graphic holdProgress;

        private Image holdProgressImage;
        private RadialProgressGraphic holdProgressRadial;

        public void Configure(
            TMP_Text configuredKeyLabel,
            TMP_Text configuredActionLabel,
            Graphic configuredHoldProgress)
        {
            keyLabel = configuredKeyLabel;
            actionLabel = configuredActionLabel;
            holdProgress = configuredHoldProgress;
            CacheProgressGraphic();
        }

        private void Awake()
        {
            CacheProgressGraphic();
        }

        public void Bind(ContextInteractionPromptViewModel model)
        {
            gameObject.SetActive(model.Visible);
            if (!model.Visible) return;
            if (keyLabel != null) keyLabel.text = model.KeyLabel;
            if (actionLabel != null) actionLabel.text = model.ActionLabel;
            if (holdProgress != null)
            {
                holdProgress.enabled = model.Holding;
                if (holdProgressImage != null)
                {
                    holdProgressImage.fillAmount = model.HoldProgress01;
                }

                if (holdProgressRadial != null)
                {
                    holdProgressRadial.FillAmount = model.HoldProgress01;
                }
            }
        }

        private void CacheProgressGraphic()
        {
            holdProgressImage = holdProgress as Image;
            holdProgressRadial = holdProgress as RadialProgressGraphic;
        }
    }
}
