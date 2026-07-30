using System;
using System.Collections;
using System.Runtime.InteropServices;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    public enum VoiceCommandInputState
    {
        Idle = 0,
        Listening = 1,
        Uploading = 2,
        Transcribing = 3,
        Interpreting = 4,
        PetReaction = 5,
        Executing = 6,
        Error = 7
    }

    [DisallowMultipleComponent]
    public sealed class VoiceCommandInput : MonoBehaviour
    {
        [SerializeField] private VoiceConfig config;
        [SerializeField] private string gameSessionId;
        [SerializeField] private string petId;
        [SerializeField] private string capabilityToken;

        private readonly VoiceCommandBackendClient backendClient = new();
        private string clientCommandId;
        private bool captureInProgress;

        public VoiceCommandInputState State { get; private set; } =
            VoiceCommandInputState.Idle;
        public float ListeningElapsedSeconds { get; private set; }
        public string LastTranscript { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public event Action<VoiceCommandInputState> StateChanged;
        public event Action<string> TranscriptReceived;

        public void Configure(
            VoiceConfig configuredConfig,
            string configuredSessionId,
            string configuredPetId,
            string configuredCapabilityToken)
        {
            config = configuredConfig;
            gameSessionId = configuredSessionId;
            petId = configuredPetId;
            capabilityToken = configuredCapabilityToken;
        }

        public void SetCapabilityToken(string token) => capabilityToken = token;

        private void Start()
        {
            VoiceCapabilityStore.Changed += HandleCapabilityChanged;
            if (string.IsNullOrWhiteSpace(capabilityToken))
            {
                capabilityToken = VoiceCapabilityStore.Token;
            }

            if (string.IsNullOrWhiteSpace(gameSessionId))
            {
                gameSessionId = VoiceCapabilityStore.SessionId;
            }
        }

        private void OnDestroy()
        {
            VoiceCapabilityStore.Changed -= HandleCapabilityChanged;
        }

        private void HandleCapabilityChanged()
        {
            capabilityToken = VoiceCapabilityStore.Token;
            gameSessionId = VoiceCapabilityStore.SessionId;
        }

        public void ApplyServerTranscript(string transcript)
        {
            LastTranscript = transcript ?? string.Empty;
            TranscriptReceived?.Invoke(LastTranscript);
            SetState(VoiceCommandInputState.Interpreting);
        }

        public void ApplyServerDecision(bool executing)
        {
            SetState(
                executing
                    ? VoiceCommandInputState.Executing
                    : VoiceCommandInputState.PetReaction);
        }

        public void StartListening()
        {
            if (captureInProgress || config == null || !config.VoiceInputEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(capabilityToken))
            {
                SetError("VOICE_CAPABILITY_MISSING");
                return;
            }

            captureInProgress = true;
            ListeningElapsedSeconds = 0f;
            LastError = string.Empty;
            SetState(VoiceCommandInputState.Listening);
#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceMediaRecorder_Start(
                gameObject.name,
                nameof(OnCaptureResult),
                config.MaximumUtteranceSeconds);
#else
            SetError("WEBGL_MICROPHONE_REQUIRED");
#endif
        }

        public void StopListening()
        {
            if (!captureInProgress) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceMediaRecorder_Stop(gameObject.name);
#endif
        }

        public void OnCaptureResult(string json)
        {
            if (!captureInProgress) return;
            captureInProgress = false;

            VoiceCapturePayload payload;
            try
            {
                payload = JsonUtility.FromJson<VoiceCapturePayload>(json);
            }
            catch (Exception exception)
            {
                SetError("VOICE_CAPTURE_RESPONSE_INVALID:" + exception.Message);
                return;
            }

            if (payload == null || !string.IsNullOrWhiteSpace(payload.error))
            {
                SetError(payload?.error ?? "VOICE_CAPTURE_FAILED");
                return;
            }

            byte[] audio;
            try
            {
                audio = Convert.FromBase64String(payload.base64Audio ?? string.Empty);
            }
            catch (FormatException)
            {
                SetError("VOICE_AUDIO_ENCODING_INVALID");
                return;
            }

            if (audio.Length == 0)
            {
                SetError("VOICE_AUDIO_EMPTY");
                return;
            }

            if (config.MaximumFileSizeMegabytes > 0f
                && audio.Length > config.MaximumFileSizeMegabytes * 1024f * 1024f)
            {
                SetError("VOICE_AUDIO_TOO_LARGE");
                return;
            }

            clientCommandId = Guid.NewGuid().ToString("N");
            SubmitMetadataToHost();
            SetState(VoiceCommandInputState.Uploading);
            StartCoroutine(Upload(audio, payload.mimeType));
        }

        private void SubmitMetadataToHost()
        {
            CompanionKind expectedKind = petId == "dog"
                ? CompanionKind.Dog
                : CompanionKind.Cat;
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                if (!link.IsOwner
                    || CompanionCommandCatalog.GetCompanionKind(link.Role)
                    != expectedKind)
                {
                    continue;
                }

                link.SubmitVoiceCommandMetadataRpc(
                    clientCommandId,
                    petId);
                return;
            }

            FindFirstObjectByType<CompanionVoiceCommandBridge>()
                ?.SubmitOfflineVoiceContext(clientCommandId, petId);
        }

        private IEnumerator Upload(byte[] audio, string mimeType)
        {
            yield return backendClient.Submit(
                config.BackendBaseUrl,
                gameSessionId,
                petId,
                clientCommandId,
                capabilityToken,
                audio,
                mimeType,
                config.RequestTimeoutSeconds,
                response => SetState(VoiceCommandInputState.Transcribing),
                SetError);
        }

        private void Update()
        {
            if (State == VoiceCommandInputState.Listening)
            {
                ListeningElapsedSeconds += Time.unscaledDeltaTime;
            }
        }

        private void SetError(string error)
        {
            captureInProgress = false;
            LastError = error;
            SetState(VoiceCommandInputState.Error);
        }

        private void SetState(VoiceCommandInputState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceMediaRecorder_Start(
            string gameObjectName,
            string callbackMethod,
            float maximumSeconds);

        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceMediaRecorder_Stop(
            string gameObjectName);
#endif
    }
}
