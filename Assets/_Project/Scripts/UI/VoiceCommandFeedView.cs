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
                    // Push-to-talk, so the instruction is to keep holding rather
                    // than to wait out a fixed five seconds.
                    second = "V를 누른 채로 말해 주세요 (최대 "
                        + Mathf.RoundToInt(input.MaximumRecordingSeconds)
                        + "초)";
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
                    second = DescribeError(input.LastError);
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

        /// <summary>
        /// Turns a failure code into the sentence that says what to do about it.
        ///
        /// The panel used to print the raw code, so a build told the player
        /// `VOICE_COMMAND_REQUEST_FAILED:Cannot connect to destination host` and
        /// a missing local server, a muted microphone, and a denied permission
        /// all read as "voice is broken". Each of those is fixed somewhere else.
        ///
        /// Codes may carry a `:detail` suffix, so only the part before the first
        /// colon is matched.
        /// </summary>
        public static string DescribeError(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "다시 시도해 주세요";
            }

            int separator = error.IndexOf(':');
            string code = separator > 0 ? error.Substring(0, separator) : error;
            return code switch
            {
                "MIC_DEVICE_MISSING" =>
                    "마이크를 찾지 못했습니다",
                "MIC_PERMISSION_DENIED" or "NotAllowedError" =>
                    "마이크 권한이 거부되었습니다",
                "MIC_DEVICE_BUSY" or "MIC_START_FAILED" =>
                    "마이크가 다른 프로그램에 잡혀 있습니다",
                "MIC_HELD_BY_ANOTHER_CAPTURE" =>
                    "다른 음성 입력이 마이크를 쓰고 있습니다",
                "MIC_RETURNED_ONLY_ZEROS" =>
                    "마이크가 무음만 보냅니다. Windows 마이크 권한을 확인하세요",
                "VOICE_AUDIO_SILENT" =>
                    "소리가 너무 작습니다. 마이크 볼륨을 올려 주세요",
                "VOICE_AUDIO_TOO_SHORT" =>
                    "너무 짧습니다. V를 누른 채로 말해 주세요",
                "VOICE_AUDIO_EMPTY" or "MIC_AUDIO_EMPTY"
                    or "MIC_AUDIO_READ_FAILED" or "MIC_NOT_RECORDING" =>
                    "녹음된 소리가 없습니다",
                "GATEWAY_NOT_READY" or "LOCAL_AI_NOT_READY"
                    or "GATEWAY_URL_MISSING" or "LOCAL_AI_MANAGER_MISSING" =>
                    "로컬 음성 서버가 실행되지 않았습니다",
                "VOICE_COMMAND_REQUEST_FAILED" or "VOICE_UPLOAD_FAILED" =>
                    "음성 서버에 연결하지 못했습니다",
                "MIC_REQUIRES_HTTPS" =>
                    "브라우저는 https에서만 마이크를 허용합니다",
                "MICROPHONE_UNSUPPORTED" or "MEDIA_RECORDER_UNSUPPORTED"
                    or "MIME_UNSUPPORTED" =>
                    "이 브라우저는 음성 녹음을 지원하지 않습니다",
                "VOICE_CAPABILITY_MISSING" =>
                    "음성 세션이 준비되지 않았습니다",
                "VOICE_CAPTURE_NO_RESPONSE" or "RECORDER_ERROR"
                    or "RECORDER_ALREADY_RUNNING" =>
                    "녹음이 응답하지 않았습니다. 다시 시도해 주세요",
                "VOICE_INPUT_DISABLED" =>
                    "음성 입력이 꺼져 있습니다",
                "VOICE_RESULT_TIMEOUT" =>
                    "결과가 오지 않았습니다. 다시 시도해 주세요",
                "VOICE_HOST_LINK_MISSING" =>
                    "방장에게 명령을 보내지 못했습니다",
                _ => error
            };
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
