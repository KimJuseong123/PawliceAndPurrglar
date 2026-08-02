using System;
using System.Collections;
using System.IO;
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
        Unavailable = 0,
        Starting = 1,
        Idle = 2,
        Recording = 3,
        Encoding = 4,
        Transcribing = 5,
        Interpreting = 6,
        Executing = 7,
        Cooldown = 8,
        Error = 9,

        // Compatibility names used by the previous WebGL presenter and tests.
        PermissionRequested = Starting,
        Listening = Recording,
        Uploading = Encoding,
        PetReaction = Executing,
        Completed = Cooldown,
        Failed = Error,
        CommandAccepted = Cooldown,
        CommandConfused = Error
    }

    [DisallowMultipleComponent]
    public sealed class VoiceCommandInput : MonoBehaviour
    {
        private const float DefaultMaximumRecordingSeconds = 5f;
        private const float DefaultPostCommandCooldownSeconds = 30f;

        [SerializeField] private VoiceConfig config;
        [SerializeField] private string gameSessionId;
        [SerializeField] private string petId;
        [SerializeField] private string capabilityToken;
        [SerializeField] private PlayerRoleIdentity issuer;
        [SerializeField] private bool preserveDebugRecordings;

        private readonly VoiceCommandBackendClient backendClient = new();
        private NativeVoiceCaptureProvider nativeCapture;
        private string clientCommandId;
        private bool captureInProgress;
        private bool finishRequested;
        private string lastRecordingPath;

        public VoiceCommandInputState State { get; private set; } =
            VoiceCommandInputState.Idle;
        public float ListeningElapsedSeconds { get; private set; }
        public float CooldownRemainingSeconds { get; private set; }
        public string LastTranscript { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public VoiceCommandResult LastResult { get; private set; }
        public string PetId => petId;
        public string VoiceProviderName =>
#if UNITY_WEBGL && !UNITY_EDITOR
            "WebGL Gateway";
#else
            "Local AI";
#endif
        public PlayerRole IssuerRole => issuer != null
            ? issuer.Role
            : petId == "dog" ? PlayerRole.Police : PlayerRole.Thief;
        public float MaximumRecordingSeconds => config != null
            ? Mathf.Max(0.1f, config.MaximumUtteranceSeconds)
            : DefaultMaximumRecordingSeconds;
        public float PostCommandCooldownSeconds => config != null
            ? Mathf.Max(0f, config.PostCommandCooldownSeconds)
            : DefaultPostCommandCooldownSeconds;
        public event Action<VoiceCommandInputState> StateChanged;
        public event Action<string> TranscriptReceived;
        public event Action<VoiceCommandResult> ResultReceived;
        public event Action<string> ErrorReceived;

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

            if (issuer == null)
            {
                issuer = GetComponent<PlayerRoleIdentity>();
            }
        }

        private void OnDestroy()
        {
            VoiceCapabilityStore.Changed -= HandleCapabilityChanged;
            CancelListening();
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
            ApplyServerDecision(executing, LastResult);
        }

        public void ApplyServerDecision(
            bool executing,
            VoiceCommandResult result)
        {
            LastResult = result ?? LastResult;
            if (LastResult != null)
            {
                ResultReceived?.Invoke(LastResult);
            }

            if (executing)
            {
                SetState(VoiceCommandInputState.Executing);
            }

            // The legacy WebGL callback arrives after the server has already
            // completed execution. Keep the visible terminal state consistent
            // with the native LocalAI pipeline.
            BeginCooldown();
        }

        public void ApplyServerFailure(string error)
        {
            SetError(error);
        }

        public void StartListening()
        {
            if (captureInProgress
                || CooldownRemainingSeconds > 0f
                || State == VoiceCommandInputState.Cooldown)
            {
                return;
            }

            if (config != null && !config.VoiceInputEnabled)
            {
                SetError("VOICE_INPUT_DISABLED");
                return;
            }

            captureInProgress = true;
            finishRequested = false;
            ListeningElapsedSeconds = 0f;
            LastError = string.Empty;
            LastResult = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(capabilityToken))
            {
                SetError("VOICE_CAPABILITY_MISSING");
                return;
            }

            SetState(VoiceCommandInputState.Recording);
            PawsAndLoot_VoiceMediaRecorder_Start(
                gameObject.name,
                nameof(OnCaptureResult),
                MaximumRecordingSeconds);
#else
            StartCoroutine(BeginNativeCapture());
#endif
        }

        public void StopListening()
        {
            if (!captureInProgress || finishRequested)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceMediaRecorder_Stop(gameObject.name);
#else
            if (State == VoiceCommandInputState.Recording)
            {
                FinishNativeCapture();
            }
#endif
        }

        public void CancelListening()
        {
            finishRequested = true;
            nativeCapture?.Cancel();
            captureInProgress = false;
            StopAllCoroutines();
            if (State == VoiceCommandInputState.Recording
                || State == VoiceCommandInputState.Starting)
            {
                SetState(VoiceCommandInputState.Idle);
            }
        }

        private IEnumerator BeginNativeCapture()
        {
            nativeCapture ??= new NativeVoiceCaptureProvider();
            yield return nativeCapture.Begin(
                MaximumRecordingSeconds,
                () => SetState(VoiceCommandInputState.Starting),
                () => SetState(VoiceCommandInputState.Recording),
                SetError);
        }

        private void FinishNativeCapture()
        {
            if (finishRequested || nativeCapture == null)
            {
                return;
            }

            finishRequested = true;
            StartCooldownTimer();
            SetState(VoiceCommandInputState.Encoding);
            StartCoroutine(EndNativeCapture());
        }

        private IEnumerator EndNativeCapture()
        {
            VoiceCaptureData data = null;
            string error = string.Empty;
            yield return nativeCapture.End(
                completed => data = completed,
                failed => error = failed);

            if (!captureInProgress)
            {
                yield break;
            }

            if (data == null || data.AudioBytes == null || data.AudioBytes.Length == 0)
            {
                SetError(string.IsNullOrWhiteSpace(error)
                    ? "VOICE_AUDIO_EMPTY"
                    : error);
                yield break;
            }

            string directory = Path.Combine(
                Application.persistentDataPath,
                "VoiceTemp");
            Directory.CreateDirectory(directory);
            lastRecordingPath = Path.Combine(
                directory,
                Guid.NewGuid().ToString("N") + ".wav");

            try
            {
                File.WriteAllBytes(lastRecordingPath, data.AudioBytes);
            }
            catch (Exception exception)
            {
                SetError("VOICE_WAV_WRITE_FAILED:" + exception.Message);
                yield break;
            }

            LocalAiProcessManager manager =
                FindFirstObjectByType<LocalAiProcessManager>();
            if (manager == null)
            {
                SetError("LOCAL_AI_MANAGER_MISSING");
                yield break;
            }

            SetState(VoiceCommandInputState.Transcribing);
            bool ready = false;
            string startupError = string.Empty;
            yield return manager.EnsureReady((success, error) =>
            {
                ready = success;
                startupError = error;
            });

            if (!captureInProgress)
            {
                yield break;
            }

            if (!ready)
            {
                SetError(string.IsNullOrWhiteSpace(startupError)
                    ? "LOCAL_AI_NOT_READY"
                    : startupError);
                yield break;
            }

            LocalAiVoiceClient client = new();
            string context = LocalAiCommandContext.BuildJson(
                issuer,
                petId);
            LocalAiVoiceResponse response = null;
            string requestError = string.Empty;
            yield return client.Submit(
                manager != null ? manager.GatewayBaseUrl : string.Empty,
                data.AudioBytes,
                config != null ? config.RequestTimeoutSeconds : 30f,
                petId,
                context,
                result => response = result,
                errorMessage => requestError = errorMessage,
                () => SetState(VoiceCommandInputState.Interpreting));

            if (response == null)
            {
                SetError(string.IsNullOrWhiteSpace(requestError)
                    ? "LOCAL_AI_REQUEST_FAILED"
                    : requestError);
                yield break;
            }

            LastTranscript = response.transcript ?? string.Empty;
            TranscriptReceived?.Invoke(LastTranscript);
            VoiceCommandResult resultDto = response.ToVoiceCommandResult();
            LastResult = resultDto;

            LocalAiCommandExecutor executor =
                FindFirstObjectByType<LocalAiCommandExecutor>();
            if (executor == null)
            {
                GameObject executorObject = new("Local AI Command Executor");
                DontDestroyOnLoad(executorObject);
                executor = executorObject.AddComponent<LocalAiCommandExecutor>();
            }
            SetState(VoiceCommandInputState.Executing);
            bool accepted = executor != null
                && executor.TryExecute(this, resultDto, response);
            if (!accepted && executor == null)
            {
                SetError("LOCAL_AI_EXECUTOR_MISSING");
            }
            else
            {
                ResultReceived?.Invoke(resultDto);
                BeginCooldown();
            }

            captureInProgress = false;
            CleanupRecording();
        }

        public void OnCaptureResult(string json)
        {
            if (!captureInProgress) return;
            captureInProgress = false;
            StartCooldownTimer();

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

            if (config != null
                && config.MaximumFileSizeMegabytes > 0f
                && audio.Length > config.MaximumFileSizeMegabytes * 1024f * 1024f)
            {
                SetError("VOICE_AUDIO_TOO_LARGE");
                return;
            }

            clientCommandId = Guid.NewGuid().ToString("N");
            SubmitMetadataToHost();
            SetState(VoiceCommandInputState.Encoding);
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

                link.SubmitVoiceCommandMetadataRpc(clientCommandId, petId);
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
                _ => SetState(VoiceCommandInputState.Transcribing),
                SetError);
        }

        private void Update()
        {
            if (CooldownRemainingSeconds > 0f)
            {
                CooldownRemainingSeconds = Mathf.Max(
                    0f,
                    CooldownRemainingSeconds - Time.unscaledDeltaTime);
                if (CooldownRemainingSeconds <= 0f
                    && State == VoiceCommandInputState.Cooldown)
                {
                    SetState(VoiceCommandInputState.Idle);
                }
            }

            if (State == VoiceCommandInputState.Recording)
            {
                ListeningElapsedSeconds += Time.unscaledDeltaTime;
                if (ListeningElapsedSeconds >= MaximumRecordingSeconds)
                {
                    FinishNativeCapture();
                }
            }
        }

        private void BeginCooldown()
        {
            captureInProgress = false;
            StartCooldownTimer();
            SetState(VoiceCommandInputState.Cooldown);
        }

        private void StartCooldownTimer()
        {
            if (CooldownRemainingSeconds <= 0f)
            {
                CooldownRemainingSeconds = PostCommandCooldownSeconds;
            }
        }

        private void SetError(string error)
        {
            captureInProgress = false;
            LastError = error ?? "VOICE_ERROR";
            ErrorReceived?.Invoke(LastError);
            SetState(VoiceCommandInputState.Error);
            CleanupRecording();
        }

        private void SetState(VoiceCommandInputState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void CleanupRecording()
        {
            if (preserveDebugRecordings || string.IsNullOrWhiteSpace(lastRecordingPath))
            {
                return;
            }

            try
            {
                if (File.Exists(lastRecordingPath))
                {
                    File.Delete(lastRecordingPath);
                }
            }
            catch (IOException)
            {
                // Cleanup is best effort; the request result remains authoritative.
            }

            lastRecordingPath = string.Empty;
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

        [Serializable]
        private sealed class VoiceCapturePayload
        {
            public string base64Audio;
            public string mimeType;
            public string error;
        }
    }
}
