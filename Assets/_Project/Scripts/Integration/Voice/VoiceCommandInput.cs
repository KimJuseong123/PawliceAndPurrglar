using System;
using System.Collections;
using System.IO;
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

    /// <summary>
    /// Push-to-talk voice input for one animal.
    ///
    /// The state machine below is **platform-agnostic on purpose.** Capture is the
    /// only thing that genuinely differs between a browser and a Windows build
    /// (`Microphone` does not exist in one, `MediaRecorder` in the other), and that
    /// difference lives behind <see cref="IVoiceCaptureProvider"/>. Where the bytes
    /// are *sent* is <see cref="VoiceConfig.Transport"/> — configuration, not
    /// compilation.
    ///
    /// It used to be `#if UNITY_WEBGL` for both, which meant the browser talked to
    /// `server/` and Windows talked to a local Python gateway: **two AI backends**,
    /// so the same sentence could be understood differently depending on which
    /// build you held, and nothing tuned during Windows playtesting reached the
    /// build being submitted (`VOICE-012`).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCommandInput : MonoBehaviour
    {
        private const float DefaultMaximumRecordingSeconds = 5f;
        private const float DefaultPostCommandCooldownSeconds = 30f;

        /// <summary>
        /// Shortest recording the key can produce. The native capture rejects
        /// anything under 250ms outright; this leaves room above that so a tap
        /// still carries a word rather than the start of one.
        /// </summary>
        private const float MinimumRecordingSeconds = 0.7f;

        [SerializeField] private VoiceConfig config;
        [SerializeField] private string gameSessionId;
        [SerializeField] private string petId;
        [SerializeField] private string capabilityToken;
        [SerializeField] private PlayerRoleIdentity issuer;
        [SerializeField] private bool preserveDebugRecordings;

        /// <summary>
        /// Whether this component belongs to the player sitting at this machine.
        ///
        /// One of these is attached to each role object, so both existed on both
        /// machines and the voice key started both. Two captures then fought over
        /// one recording device and one of them got silence (`ISSUE-069`).
        /// </summary>
        [SerializeField] private bool isLocallyControlled = true;

        private readonly VoiceCommandBackendClient backendClient = new();
        private IVoiceCaptureProvider capture;
        private BrowserVoiceCaptureProvider browserCapture;
        private string clientCommandId;
        private bool captureInProgress;
        private bool finishRequested;
        private bool releaseRequested;
        private string lastRecordingPath;

        public VoiceCommandInputState State { get; private set; } =
            VoiceCommandInputState.Idle;
        public float ListeningElapsedSeconds { get; private set; }
        public float CooldownRemainingSeconds { get; private set; }
        public string LastTranscript { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public VoiceCommandResult LastResult { get; private set; }
        public string PetId => petId;

        /// <summary>
        /// Named after where the answer comes from rather than the platform, which
        /// is now the honest description — a Windows build set to `Backend` really
        /// is using the same service the WebGL submission will.
        /// </summary>
        public string VoiceProviderName => Transport == VoiceTransport.LocalGateway
            ? "Local AI"
            : "Voice Backend";

        public VoiceTransport Transport => config != null
            ? config.Transport
            : VoiceTransport.Backend;

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

        public bool IsLocallyControlled
        {
            get => isLocallyControlled;
            set => isLocallyControlled = value;
        }

        /// <summary>
        /// Whether this machine is allowed to open the microphone for this role.
        ///
        /// The role selector is asked first and its answer wins, in a session and
        /// offline alike. Both role objects exist on both machines, so a
        /// serialized flag cannot distinguish them — the machine's own role can.
        /// </summary>
        public bool CanCaptureLocally()
        {
            if (issuer == null)
            {
                return isLocallyControlled;
            }

            LocalPlayerRoleSelector selector =
                FindFirstObjectByType<LocalPlayerRoleSelector>();
            if (selector == null)
            {
                return isLocallyControlled;
            }

            return selector.IsGameplayInputEnabled
                && selector.ActiveRole == issuer.Role;
        }

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

            BeginCooldown();
        }

        public void ApplyServerFailure(string error)
        {
            SetError(error);
        }

        /// <summary>
        /// Which capture backend this platform has. The one place a platform check
        /// is unavoidable — a browser has no `Microphone` and a Windows player has
        /// no `MediaRecorder`, and no amount of configuration changes that.
        /// </summary>
        private IVoiceCaptureProvider ResolveCapture()
        {
            if (capture != null)
            {
                return capture;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            browserCapture = new BrowserVoiceCaptureProvider(
                gameObject.name,
                nameof(OnCaptureResult));
            capture = browserCapture;
#else
            capture = new NativeVoiceCaptureProvider();
#endif
            return capture;
        }

        public void StartListening()
        {
            if (captureInProgress
                || CooldownRemainingSeconds > 0f
                || State == VoiceCommandInputState.Cooldown)
            {
                return;
            }

            // Silent, not an error. The router broadcasts the key to every voice
            // component, so the other role's component reaches here on every
            // press; reporting a failure would fill the feed with a refusal the
            // player did not cause.
            if (!CanCaptureLocally())
            {
                return;
            }

            if (config != null && !config.VoiceInputEnabled)
            {
                SetError("VOICE_INPUT_DISABLED");
                return;
            }

            // Only the shared backend needs a session capability. The local
            // gateway runs on this machine and has nobody to authenticate to.
            if (Transport == VoiceTransport.Backend
                && string.IsNullOrWhiteSpace(capabilityToken))
            {
                SetError("VOICE_CAPABILITY_MISSING");
                return;
            }

            captureInProgress = true;
            finishRequested = false;
            releaseRequested = false;
            ListeningElapsedSeconds = 0f;
            LastError = string.Empty;
            LastResult = null;

            StartCoroutine(BeginCapture());
        }

        /// <summary>
        /// Records the key release. It does not stop the capture on the spot.
        ///
        /// Opening a device takes a moment, so a quick tap releases while the
        /// state is still <c>Starting</c> and there is nothing to stop. A release
        /// below <see cref="MinimumRecordingSeconds"/> also produces audio the
        /// capture rejects as too short. So the release is remembered and
        /// <see cref="Update"/> ends the recording once it is long enough to
        /// contain a word — a tap becomes a short command instead of a failure.
        /// </summary>
        public void StopListening()
        {
            if (!captureInProgress || finishRequested)
            {
                return;
            }

            releaseRequested = true;
            if (State == VoiceCommandInputState.Recording
                && ListeningElapsedSeconds >= MinimumRecordingSeconds)
            {
                FinishCapture();
            }
        }

        public void CancelListening()
        {
            finishRequested = true;
            capture?.Cancel();
            captureInProgress = false;
            StopAllCoroutines();
            if (State == VoiceCommandInputState.Recording
                || State == VoiceCommandInputState.Starting)
            {
                SetState(VoiceCommandInputState.Idle);
            }
        }

        private IEnumerator BeginCapture()
        {
            yield return ResolveCapture().Begin(
                MaximumRecordingSeconds,
                () => SetState(VoiceCommandInputState.Starting),
                () => SetState(VoiceCommandInputState.Recording),
                SetError);
        }

        private void FinishCapture()
        {
            if (finishRequested || capture == null)
            {
                return;
            }

            finishRequested = true;
            SetState(VoiceCommandInputState.Encoding);
            StartCoroutine(EndCapture());
        }

        private IEnumerator EndCapture()
        {
            VoiceCaptureData data = null;
            string error = string.Empty;
            yield return capture.End(
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

            if (config != null
                && config.MaximumFileSizeMegabytes > 0f
                && data.AudioBytes.Length
                    > config.MaximumFileSizeMegabytes * 1024f * 1024f)
            {
                SetError("VOICE_AUDIO_TOO_LARGE");
                yield break;
            }

            if (Transport == VoiceTransport.LocalGateway)
            {
                yield return SubmitToLocalGateway(data);
                yield break;
            }

            yield return SubmitToBackend(data);
        }

        /// <summary>
        /// The shared `server/` backend. The answer does not come back in this
        /// response — it arrives as a socket event and lands on
        /// <see cref="ApplyServerTranscript"/> / <see cref="ApplyServerDecision"/>.
        /// </summary>
        private IEnumerator SubmitToBackend(VoiceCaptureData data)
        {
            clientCommandId = Guid.NewGuid().ToString("N");
            SubmitMetadataToHost();

            VoiceCommandResponse response = null;
            yield return backendClient.Submit(
                VoiceBackendAddress.Resolve(config),
                gameSessionId,
                petId,
                clientCommandId,
                capabilityToken,
                data.AudioBytes,
                data.MimeType,
                config != null ? config.RequestTimeoutSeconds : 15f,
                result =>
                {
                    response = result;
                    SetState(VoiceCommandInputState.Transcribing);
                },
                SetError);

            if (response == null)
            {
                yield break;
            }

            // Nothing to hand over means the socket is still the delivery route
            // (an older server, or a deployment where `?wait` is ignored). The
            // socket listener will finish the command, so this must not report a
            // failure — that would blame the request for arriving early.
            if (response.classification == null
                && string.IsNullOrWhiteSpace(response.transcript))
            {
                yield break;
            }

            // Handed to the bridge rather than acted on here: the obedience roll
            // has to run in one place, on the host, or the two screens disagree
            // about whether the animal listened.
            FindFirstObjectByType<CompanionVoiceCommandBridge>()
                ?.ApplyBackendResult(
                    response.commandId,
                    petId,
                    response.transcript,
                    response.classification,
                    0);
        }

        /// <summary>
        /// The local Python gateway, which answers synchronously in its HTTP
        /// response and so executes the command here.
        /// </summary>
        private IEnumerator SubmitToLocalGateway(VoiceCaptureData data)
        {
            // Written only when asked for. The request carries the bytes, so a
            // file per command was pure disk churn — and it was the one step in
            // this path that could not work in a browser.
            if (preserveDebugRecordings)
            {
                WriteDebugRecording(data.AudioBytes);
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
            yield return manager.EnsureReady((success, message) =>
            {
                ready = success;
                startupError = message;
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
            string context = LocalAiCommandContext.BuildJson(issuer, petId);
            LocalAiVoiceResponse response = null;
            string requestError = string.Empty;
            yield return client.Submit(
                manager.GatewayBaseUrl,
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
            executor.TryExecute(this, resultDto, response);
            ResultReceived?.Invoke(resultDto);
            BeginCooldown();
            CleanupRecording();
        }

        private void WriteDebugRecording(byte[] audio)
        {
            try
            {
                string directory = Path.Combine(
                    Application.persistentDataPath,
                    "VoiceTemp");
                Directory.CreateDirectory(directory);
                lastRecordingPath = Path.Combine(
                    directory,
                    Guid.NewGuid().ToString("N") + ".wav");
                File.WriteAllBytes(lastRecordingPath, audio);
            }
            catch (Exception exception)
            {
                // Never fails the command: a debug artefact is not the point of
                // the request.
                Debug.LogWarning(
                    "Voice debug recording could not be written: "
                    + exception.Message);
                lastRecordingPath = string.Empty;
            }
        }

        /// <summary>
        /// The browser plugin's `SendMessage` target. Forwards to the provider,
        /// which is the thing waiting on the bytes.
        /// </summary>
        public void OnCaptureResult(string json)
        {
            browserCapture?.SubmitCallbackPayload(json);
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

            if (State != VoiceCommandInputState.Recording)
            {
                return;
            }

            ListeningElapsedSeconds += Time.unscaledDeltaTime;
            if (ListeningElapsedSeconds < MaximumRecordingSeconds)
            {
                // The key was let go earlier; end as soon as the recording is
                // long enough to be a command.
                if (releaseRequested
                    && ListeningElapsedSeconds >= MinimumRecordingSeconds)
                {
                    FinishCapture();
                }

                return;
            }

            FinishCapture();
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

            // No cooldown on failure. It used to start the moment the player
            // stopped speaking, before the result was known, so a recording that
            // was too quiet or a gateway that was not running locked the key for
            // thirty seconds. A failed attempt spends nothing.
            CooldownRemainingSeconds = 0f;
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
            if (preserveDebugRecordings
                || string.IsNullOrWhiteSpace(lastRecordingPath))
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
    }
}
