using PawsAndLoot.Integration.Voice;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// Displays capture and processing state without exposing internal model
    /// confidence. The server/Host events remain the source of gameplay truth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandHudPresenter : MonoBehaviour
    {
        [SerializeField] private VoiceCommandInput input;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Text transcriptLabel;
        [SerializeField] private Slider captureGauge;
        [SerializeField] private Button microphoneButton;
        private bool inputSubscribed;
        private bool buttonSubscribed;

        public void Configure(
            VoiceCommandInput configuredInput,
            Text configuredStateLabel,
            Text configuredTranscriptLabel,
            Slider configuredCaptureGauge,
            Button configuredMicrophoneButton)
        {
            input = configuredInput;
            stateLabel = configuredStateLabel;
            transcriptLabel = configuredTranscriptLabel;
            captureGauge = configuredCaptureGauge;
            microphoneButton = configuredMicrophoneButton;
            BindButton();
            Refresh();
        }

        private void OnEnable()
        {
            BindInput();
            BindButton();
        }

        private void OnDisable()
        {
            if (inputSubscribed && input != null)
            {
                input.StateChanged -= HandleStateChanged;
                inputSubscribed = false;
            }
            if (buttonSubscribed && microphoneButton != null)
            {
                microphoneButton.onClick.RemoveListener(StartListening);
                buttonSubscribed = false;
            }
        }

        private void Update()
        {
            BindInput();
            Refresh();
        }

        public void StartListening()
        {
            BindInput();
            if (input == null) return;
            if (input.State == VoiceCommandInputState.Recording
                || input.CooldownRemainingSeconds > 0f)
            {
                return;
            }

            input.StartListening();
        }

        public void StopListening()
        {
            input?.StopListening();
        }

        private void HandleStateChanged(VoiceCommandInputState state)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (input == null) return;
            if (stateLabel != null)
            {
                stateLabel.text = input.State switch
                {
                    VoiceCommandInputState.Recording => "듣는 중",
                    VoiceCommandInputState.Encoding => "WAV 변환 중",
                    VoiceCommandInputState.Transcribing => "음성 인식 중",
                    VoiceCommandInputState.Interpreting => "동물이 생각 중",
                    VoiceCommandInputState.Executing => "행동 중",
                    VoiceCommandInputState.Cooldown => "명령 완료",
                    VoiceCommandInputState.Error => "다시 말해 주세요",
                    _ => "음성 명령"
                };
            }

            if (transcriptLabel != null && !string.IsNullOrEmpty(input.LastTranscript))
            {
                transcriptLabel.text = "플레이어: \"" + input.LastTranscript + "\"";
            }

            if (captureGauge != null)
            {
                captureGauge.value = input.State == VoiceCommandInputState.Recording
                    ? Mathf.Clamp01(input.ListeningElapsedSeconds / 5f)
                    : 0f;
            }
        }

        private void BindInput()
        {
            if (input == null)
            {
                PlayerRole role = LocalPlayerRoleSelector.OverriddenRole
                    ?? PlayerRole.Police;
                foreach (NetworkPlayerLink link in
                    FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
                {
                    if (link.Role != role) continue;
                    input = link.GetComponent<VoiceCommandInput>();
                    break;
                }
            }

            if (!inputSubscribed && input != null)
            {
                input.StateChanged += HandleStateChanged;
                inputSubscribed = true;
            }
        }

        private void BindButton()
        {
            if (!buttonSubscribed && microphoneButton != null)
            {
                microphoneButton.onClick.AddListener(StartListening);
                buttonSubscribed = true;
            }
        }
    }
}
