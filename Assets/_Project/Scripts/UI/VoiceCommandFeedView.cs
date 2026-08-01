using TMPro;
using PawsAndLoot.Integration.Voice;
using UnityEngine;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Small, production-safe voice feedback feed. It never invents a
    /// transcript: when STT has not returned text it reports capture only.
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
                Hide();
                return;
            }

            string first = string.Empty;
            string second = string.Empty;
            switch (input.State)
            {
                case VoiceCommandInputState.PermissionRequested:
                    first = "마이크 권한 확인 중...";
                    break;
                case VoiceCommandInputState.Recording:
                    first = "듣고 있어요...";
                    break;
                case VoiceCommandInputState.Uploading:
                case VoiceCommandInputState.Transcribing:
                case VoiceCommandInputState.Interpreting:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 파일이 입력되었습니다"
                        : input.LastTranscript;
                    second = "명령이 들어왔습니다!";
                    break;
                case VoiceCommandInputState.Completed:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 파일이 입력되었습니다"
                        : input.LastTranscript;
                    second = input.LastResult != null
                        && !string.IsNullOrWhiteSpace(input.LastResult.interpretedCommand)
                        ? input.LastResult.interpretedCommand
                        : "명령이 들어왔습니다!";
                    break;
                case VoiceCommandInputState.Failed:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 입력 처리 실패"
                        : input.LastTranscript;
                    second = string.IsNullOrWhiteSpace(input.LastError)
                        ? "음성 명령을 처리할 수 없습니다"
                        : input.LastError;
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(input.LastTranscript))
                    {
                        first = input.LastTranscript;
                    }
                    break;
            }

            if (string.IsNullOrWhiteSpace(first)
                && string.IsNullOrWhiteSpace(second))
            {
                Hide();
                return;
            }

            string key = first + "\n" + second;
            if (!string.Equals(lastContentKey, key))
            {
                lastContentKey = key;
                hideAt = Time.unscaledTime + Mathf.Max(0.5f, displaySeconds);
            }

            if (inputLabel != null)
            {
                inputLabel.text = first;
            }

            if (commandLabel != null)
            {
                commandLabel.text = second;
                commandLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(second));
            }

            SetVisible(true);
        }

        private void Update()
        {
            if (gameObject.activeSelf && hideAt > 0f
                && Time.unscaledTime >= hideAt)
            {
                Hide();
            }
        }

        private void Hide()
        {
            lastContentKey = string.Empty;
            hideAt = 0f;
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
