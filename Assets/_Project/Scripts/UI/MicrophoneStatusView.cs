using TMPro;
using PawliceAndPurrglar.Input;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    public sealed class MicrophoneStatusView : MonoBehaviour
    {
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private TMP_Text cooldownLabel;
        [SerializeField] private Image recordingRadial;
        [SerializeField] private Image disabledOverlay;

        public void Configure(
            TMP_Text configuredStateLabel,
            TMP_Text configuredKeyLabel,
            TMP_Text configuredCooldownLabel,
            Image configuredRecordingRadial,
            Image configuredDisabledOverlay)
        {
            stateLabel = configuredStateLabel;
            keyLabel = configuredKeyLabel;
            cooldownLabel = configuredCooldownLabel;
            recordingRadial = configuredRecordingRadial;
            disabledOverlay = configuredDisabledOverlay;
        }

        public void Bind(MicrophoneStatusViewModel model)
        {
            if (stateLabel != null) stateLabel.text = model.StateLabel;
            if (keyLabel != null) keyLabel.text = GameplayInputRouter.VoiceBindingLabel;
            if (cooldownLabel != null)
            {
                cooldownLabel.text = model.CooldownSeconds > 0f
                    ? Mathf.CeilToInt(model.CooldownSeconds).ToString()
                    : string.Empty;
            }

            if (recordingRadial != null)
            {
                recordingRadial.enabled = model.Recording
                    || model.CooldownSeconds > 0f;
                recordingRadial.fillAmount = model.Recording
                    ? model.RecordingProgress01
                    : model.CooldownProgress01;
            }

            if (disabledOverlay != null)
            {
                disabledOverlay.enabled = model.PermissionFailed || model.CooldownSeconds > 0f;
            }
        }
    }
}
