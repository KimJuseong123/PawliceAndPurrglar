using TMPro;
using PawsAndLoot.Integration.Voice;
using UnityEngine;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Central bottom feedback for voice and keyboard animal commands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandFeedView : MonoBehaviour
    {
        [SerializeField] private TMP_Text inputLabel;
        [SerializeField] private TMP_Text commandLabel;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Min(0.5f)] private float displaySeconds = 4f;

        private string lastContentKey = string.Empty;
        private float hideAt;
        private bool manualMessageActive;

        public void Configure(
            TMP_Text configuredInputLabel,
            TMP_Text configuredCommandLabel,
            CanvasGroup configuredCanvasGroup)
        {
            inputLabel = configuredInputLabel;
            commandLabel = configuredCommandLabel;
            canvasGroup = configuredCanvasGroup;
            Hide();
        }

        public void Bind(VoiceCommandInput input)
        {
            if (input == null)
            {
                return;
            }

            if (manualMessageActive && Time.unscaledTime < hideAt)
            {
                return;
            }

            string first = string.Empty;
            string second = string.Empty;
            switch (input.State)
            {
                case VoiceCommandInputState.Starting:
                    first = "음성 입력 준비 중";
                    break;
                case VoiceCommandInputState.Recording:
                    first = "음성 명령 녹음 중";
                    second = "5초 동안 듣고 있어요";
                    break;
                case VoiceCommandInputState.Encoding:
                case VoiceCommandInputState.Transcribing:
                case VoiceCommandInputState.Interpreting:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 처리 중"
                        : input.LastTranscript;
                    second = "명령을 해석하고 있어요";
                    break;
                case VoiceCommandInputState.Cooldown:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 완료"
                        : input.LastTranscript;
                    second = input.LastResult != null
                        && !string.IsNullOrWhiteSpace(
                            input.LastResult.interpretedCommand)
                            ? input.LastResult.interpretedCommand
                            : "명령 쿨타임";
                    break;
                case VoiceCommandInputState.Error:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 처리 실패"
                        : input.LastTranscript;
                    second = string.IsNullOrWhiteSpace(input.LastError)
                        ? "다시 시도해 주세요"
                        : input.LastError;
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(input.LastTranscript))
                    {
                        first = input.LastTranscript;
                    }

                    break;
            }

            ShowContent(first, second, displaySeconds, false);
        }

        public void ShowMessage(
            string first,
            string second = "",
            float seconds = 4f)
        {
            ShowContent(first, second, seconds, true);
        }

        private void ShowContent(
            string first,
            string second,
            float seconds,
            bool manual)
        {
            if (string.IsNullOrWhiteSpace(first)
                && string.IsNullOrWhiteSpace(second))
            {
                return;
            }

            string key = first + "\n" + second;
            if (manual || !string.Equals(lastContentKey, key))
            {
                lastContentKey = key;
                hideAt = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            }

            manualMessageActive = manual;
            if (inputLabel != null)
            {
                inputLabel.text = first;
            }

            if (commandLabel != null)
            {
                commandLabel.text = second;
                commandLabel.gameObject.SetActive(
                    !string.IsNullOrWhiteSpace(second));
            }

            SetVisible(true);
        }

        private void Update()
        {
            if (gameObject.activeSelf
                && hideAt > 0f
                && Time.unscaledTime >= hideAt)
            {
                Hide();
            }
        }

        private void Hide()
        {
            lastContentKey = string.Empty;
            hideAt = 0f;
            manualMessageActive = false;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
