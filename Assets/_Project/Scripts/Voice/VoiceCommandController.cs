using System;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PawsAndLoot.Voice
{
    public enum VoiceCommandStatus
    {
        Disabled = 0,
        Ready,
        Listening,
        Processing,
        Executed,
        Rejected,
        Error
    }

    public readonly struct VoiceCommandFeedback
    {
        public VoiceCommandFeedback(
            VoiceCommandStatus status,
            string transcript,
            CompanionCommandId commandId,
            string message)
        {
            Status = status;
            Transcript = transcript;
            CommandId = commandId;
            Message = message;
        }

        public VoiceCommandStatus Status { get; }
        public string Transcript { get; }
        public CompanionCommandId CommandId { get; }
        public string Message { get; }
    }

    public sealed class VoiceCommandController : MonoBehaviour
    {
        private const int RequestedFrequency = 16000;

        [SerializeField]
        private VoiceConfig config;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private VoiceCommandGatewayClient gatewayClient;

        private IVoiceCommandGateway _gateway;
        private AudioClip _recording;
        private float _recordingStartedAt;
        private bool _requestInFlight;
        private string _activeRequestId;
        private PlayerRole _requestRole;

        public event Action<VoiceCommandFeedback> FeedbackChanged;

        public VoiceCommandStatus Status { get; private set; }
        public VoiceCommandFeedback LastFeedback { get; private set; }

        public void Configure(
            VoiceConfig configuredConfig,
            MatchRuntimeState configuredMatchRuntime,
            LocalPlayerRoleSelector configuredRoleSelector,
            CompanionCommandDispatcher configuredDispatcher,
            VoiceCommandGatewayClient configuredGateway)
        {
            config = configuredConfig;
            matchRuntime = configuredMatchRuntime;
            roleSelector = configuredRoleSelector;
            dispatcher = configuredDispatcher;
            gatewayClient = configuredGateway;
            _gateway = gatewayClient;
            SetReadyState();
        }

        public void SetGatewayForTests(IVoiceCommandGateway gateway)
        {
            _gateway = gateway;
        }

        public void SubmitWavForTests(
            byte[] wavBytes,
            PlayerRole role,
            string requestId)
        {
            BeginGatewayRequest(wavBytes, role, requestId);
        }

        private void Awake()
        {
            if (config == null && GameConfigService.IsInitialized)
            {
                config = GameConfigService.Current.Voice;
            }

            _gateway = gatewayClient;
            SetReadyState();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null
                || config == null
                || !config.VoiceInputEnabled)
            {
                return;
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                StartRecording();
            }

            if (_recording != null
                && (keyboard.vKey.wasReleasedThisFrame
                    || Time.unscaledTime - _recordingStartedAt
                    >= config.MaximumUtteranceSeconds))
            {
                StopRecordingAndSend();
            }
        }

        private void StartRecording()
        {
            if (_recording != null
                || _requestInFlight
                || matchRuntime == null
                || !matchRuntime.IsGameplayActive
                || roleSelector == null
                || !roleSelector.IsGameplayInputEnabled)
            {
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Publish(
                    VoiceCommandStatus.Error,
                    string.Empty,
                    CompanionCommandId.None,
                    "마이크를 찾을 수 없습니다. 숫자키를 사용하세요.");
                return;
            }

            try
            {
                _recording = Microphone.Start(
                    null,
                    false,
                    Mathf.CeilToInt(
                        config.MaximumUtteranceSeconds) + 1,
                    RequestedFrequency);
            }
            catch (Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Voice,
                    exception,
                    "Could not start microphone capture.",
                    this);
                _recording = null;
            }

            if (_recording == null)
            {
                Publish(
                    VoiceCommandStatus.Error,
                    string.Empty,
                    CompanionCommandId.None,
                    "마이크 녹음을 시작하지 못했습니다.");
                return;
            }

            _recordingStartedAt = Time.unscaledTime;
            Publish(
                VoiceCommandStatus.Listening,
                string.Empty,
                CompanionCommandId.None,
                "듣는 중... V 키를 놓아 명령을 보냅니다.");
        }

        private void StopRecordingAndSend()
        {
            AudioClip clip = _recording;
            int frames = Mathf.Max(0, Microphone.GetPosition(null));
            Microphone.End(null);
            _recording = null;

            float duration = clip.frequency <= 0
                ? 0f
                : (float)frames / clip.frequency;
            if (duration < config.MinimumUtteranceSeconds)
            {
                Destroy(clip);
                Publish(
                    VoiceCommandStatus.Rejected,
                    string.Empty,
                    CompanionCommandId.None,
                    "발화가 너무 짧습니다.");
                return;
            }

            byte[] wavBytes;
            try
            {
                wavBytes = WavEncoder.Encode(clip, frames);
            }
            catch (Exception exception)
            {
                Destroy(clip);
                GameLogger.Exception(
                    GameLogCategory.Voice,
                    exception,
                    "Could not encode microphone audio.",
                    this);
                Publish(
                    VoiceCommandStatus.Error,
                    string.Empty,
                    CompanionCommandId.None,
                    "음성을 변환하지 못했습니다.");
                return;
            }

            Destroy(clip);
            BeginGatewayRequest(
                wavBytes,
                roleSelector.ActiveRole,
                Guid.NewGuid().ToString("N"));
        }

        private void BeginGatewayRequest(
            byte[] wavBytes,
            PlayerRole role,
            string requestId)
        {
            if (_gateway == null || _requestInFlight)
            {
                Publish(
                    VoiceCommandStatus.Error,
                    string.Empty,
                    CompanionCommandId.None,
                    "음성 중계 서버를 사용할 수 없습니다.");
                return;
            }

            _requestInFlight = true;
            _activeRequestId = requestId;
            _requestRole = role;
            Publish(
                VoiceCommandStatus.Processing,
                string.Empty,
                CompanionCommandId.None,
                "음성을 명령으로 변환하는 중...");
            _gateway.RequestCommand(
                wavBytes,
                role,
                requestId,
                OnGatewayCompleted);
        }

        private void OnGatewayCompleted(VoiceGatewayResult gatewayResult)
        {
            _requestInFlight = false;
            if (!string.Equals(
                    gatewayResult.RequestId,
                    _activeRequestId,
                    StringComparison.Ordinal)
                || matchRuntime == null
                || !matchRuntime.IsGameplayActive
                || roleSelector == null
                || roleSelector.ActiveRole != _requestRole)
            {
                Publish(
                    VoiceCommandStatus.Rejected,
                    gatewayResult.Transcript,
                    CompanionCommandId.None,
                    CompanionCommandValidator.GetFailureMessage(
                        CompanionCommandFailure.StaleVoiceResult));
                return;
            }

            if (!gatewayResult.Succeeded)
            {
                Publish(
                    VoiceCommandStatus.Error,
                    gatewayResult.Transcript,
                    CompanionCommandId.None,
                    "음성 서비스를 사용할 수 없습니다. 숫자키를 사용하세요.");
                return;
            }

            if (gatewayResult.CommandId == CompanionCommandId.None)
            {
                Publish(
                    VoiceCommandStatus.Rejected,
                    gatewayResult.Transcript,
                    CompanionCommandId.None,
                    "명령을 이해하지 못했습니다.");
                return;
            }

            if (dispatcher == null)
            {
                Publish(
                    VoiceCommandStatus.Error,
                    gatewayResult.Transcript,
                    gatewayResult.CommandId,
                    "동물 명령 시스템을 사용할 수 없습니다.");
                return;
            }

            CompanionCommandResult result = dispatcher.TryDispatch(
                gatewayResult.CommandId,
                CompanionCommandSource.Voice,
                gatewayResult.RequestId);
            Publish(
                result.Accepted
                    ? VoiceCommandStatus.Executed
                    : VoiceCommandStatus.Rejected,
                gatewayResult.Transcript,
                gatewayResult.CommandId,
                result.Accepted
                    ? "음성 명령을 실행합니다."
                    : result.Message);
        }

        private void SetReadyState()
        {
            bool enabled = config != null
                && config.VoiceInputEnabled;
            Publish(
                enabled
                    ? VoiceCommandStatus.Ready
                    : VoiceCommandStatus.Disabled,
                string.Empty,
                CompanionCommandId.None,
                enabled
                    ? "V 키를 누르고 동물에게 명령하세요."
                    : "음성 입력이 비활성화되어 있습니다.");
        }

        private void Publish(
            VoiceCommandStatus status,
            string transcript,
            CompanionCommandId commandId,
            string message)
        {
            Status = status;
            LastFeedback = new VoiceCommandFeedback(
                status,
                transcript ?? string.Empty,
                commandId,
                message ?? string.Empty);
            FeedbackChanged?.Invoke(LastFeedback);
        }

        private void OnDisable()
        {
            if (_recording != null)
            {
                Microphone.End(null);
                Destroy(_recording);
                _recording = null;
            }
        }
    }
}
