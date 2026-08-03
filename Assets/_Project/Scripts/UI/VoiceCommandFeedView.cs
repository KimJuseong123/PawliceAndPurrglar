using TMPro;
using PawsAndLoot.Companions;
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
        [SerializeField] private GameObject interpretationIcon;
        [SerializeField, Min(0.5f)] private float displaySeconds = 4f;

        private string lastContentKey = string.Empty;
        private float hideAt;
        private bool manualMessageActive;

        public void Configure(
            TMP_Text configuredInputLabel,
            TMP_Text configuredCommandLabel,
            CanvasGroup configuredCanvasGroup,
            GameObject configuredInterpretationIcon = null)
        {
            inputLabel = configuredInputLabel;
            commandLabel = configuredCommandLabel;
            canvasGroup = configuredCanvasGroup;
            interpretationIcon = configuredInterpretationIcon;
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
            bool showInterpretationIcon = false;
            switch (input.State)
            {
                case VoiceCommandInputState.Starting:
                    first = "음성 입력 준비 중";
                    break;
                case VoiceCommandInputState.Recording:
                    first = "LISTENING...";
                    second = "5초 동안 듣고 있어요";
                    break;
                case VoiceCommandInputState.Encoding:
                case VoiceCommandInputState.Transcribing:
                case VoiceCommandInputState.Interpreting:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 처리 중"
                        : FormatRawTranscript(input.LastTranscript);
                    second = $"{GetAnimalLabel(input)} THINKING...";
                    break;
                case VoiceCommandInputState.Cooldown:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 완료"
                        : FormatRawTranscript(input.LastTranscript);
                    second = input.LastResult != null
                        ? FormatInterpretedCommand(input, input.LastResult)
                        : "명령 쿨타임";
                    showInterpretationIcon = input.LastResult != null;
                    break;
                case VoiceCommandInputState.Error:
                    first = string.IsNullOrWhiteSpace(input.LastTranscript)
                        ? "음성 명령 처리 실패"
                        : FormatRawTranscript(input.LastTranscript);
                    second = string.IsNullOrWhiteSpace(input.LastError)
                        ? "다시 시도해 주세요"
                        : input.LastError;
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(input.LastTranscript))
                    {
                        first = FormatRawTranscript(input.LastTranscript);
                        if (input.LastResult != null)
                        {
                            second = FormatInterpretedCommand(input, input.LastResult);
                            showInterpretationIcon = true;
                        }
                    }

                    break;
            }

            ShowContent(
                first,
                second,
                displaySeconds,
                false,
                showInterpretationIcon);
        }

        public void ShowMessage(
            string first,
            string second = "",
            float seconds = 4f)
        {
            ShowContent(first, second, seconds, true, false);
        }

        private void ShowContent(
            string first,
            string second,
            float seconds,
            bool manual,
            bool showInterpretationIcon)
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

            if (interpretationIcon != null)
            {
                interpretationIcon.SetActive(
                    showInterpretationIcon
                    && !string.IsNullOrWhiteSpace(second));
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
            if (interpretationIcon != null)
            {
                interpretationIcon.SetActive(false);
            }

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

        private static string FormatRawTranscript(string transcript)
        {
            return $"YOU SAID: \"{Shorten(transcript, 46)}\"";
        }

        private static string FormatInterpretedCommand(
            VoiceCommandInput input,
            VoiceCommandResult result)
        {
            CompanionKind kind = string.Equals(
                    input.PetId,
                    "dog",
                    System.StringComparison.OrdinalIgnoreCase)
                ? CompanionKind.Dog
                : CompanionKind.Cat;
            string animal = kind == CompanionKind.Dog ? "DOG" : "CAT";
            string interpreted = result.interpretedCommand ?? "NONE";
            string display = "NO COMMAND";
            if (VoiceCommandMapper.TryMap(interpreted, kind, out CompanionCommandId command))
            {
                display = CompanionCommandCatalog.GetDisplayName(command);
            }

            if (!string.IsNullOrWhiteSpace(result.failureMessage)
                && display == "NO COMMAND")
            {
                return $"{animal} HEARD: {display} ({Shorten(result.failureMessage, 24)})";
            }

            return $"{animal} HEARD: {display}";
        }

        private static string GetAnimalLabel(VoiceCommandInput input)
        {
            return string.Equals(
                    input.PetId,
                    "dog",
                    System.StringComparison.OrdinalIgnoreCase)
                ? "DOG"
                : "CAT";
        }

        private static string Shorten(string value, int maxLength)
        {
            string safe = (value ?? string.Empty).Trim();
            if (safe.Length <= maxLength)
            {
                return safe;
            }

            int take = Mathf.Max(1, maxLength - 3);
            return safe.Substring(0, take) + "...";
        }
    }
}
