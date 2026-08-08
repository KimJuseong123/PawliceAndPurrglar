using UnityEngine;

namespace PawsAndLoot.Config
{
    /// <summary>
    /// Where a recording is sent for transcription and interpretation.
    ///
    /// This used to be decided by <c>#if UNITY_WEBGL</c>, which meant the browser
    /// talked to <c>server/</c> and Windows talked to a local Python gateway —
    /// **two AI backends**, so the same sentence could be understood differently
    /// depending on which build you were holding, and nothing tuned on Windows
    /// reached the build being submitted.
    ///
    /// Making it configuration instead of compilation is what lets Windows
    /// development exercise the path WebGL will actually ship on. Moving to WebGL
    /// then costs one URL change.
    /// </summary>
    public enum VoiceTransport
    {
        /// <summary>
        /// The `server/` backend over HTTP, for both platforms. The submission
        /// target (`WebGL`), and therefore the default.
        /// </summary>
        Backend = 0,

        /// <summary>
        /// The local Python gateway on 127.0.0.1, spawned by
        /// <c>LocalAiProcessManager</c>. Offline and free, but unavailable in a
        /// browser and only as accurate as the small local model — kept for
        /// working without a network or an API key.
        /// </summary>
        LocalGateway = 1
    }

    [CreateAssetMenu(menuName = "Paws & Loot/Config/Voice", fileName = "VoiceConfig")]
    public sealed class VoiceConfig : GameConfigAsset
    {
        [Header("Prototype Availability")]
        [SerializeField, Tooltip("Remains disabled until the voice technology spike is complete.")]
        private bool voiceInputEnabled;

        [SerializeField, Tooltip("Keyboard commands must remain available when voice input fails or is disabled.")]
        private bool keyboardFallbackEnabled = true;

        [Header("Capture")]
        [SerializeField, Min(0.1f), Tooltip("Maximum push-to-talk capture length. This is a prototype hypothesis.")]
        private float maximumUtteranceSeconds = 5f;

        [SerializeField, Min(0f), Tooltip("Seconds before another voice command can start after one command completes.")]
        private float postCommandCooldownSeconds = 30f;

        [Header("Backend")]
        [SerializeField, Tooltip("Which backend receives the recording. Chosen here rather than by platform so a Windows build can exercise the WebGL path.")]
        private VoiceTransport transport = VoiceTransport.Backend;

        [SerializeField, Tooltip("Non-secret voice backend URL. Never store an API key here — a WebGL build is fully readable by anyone who downloads it.")]
        private string backendBaseUrl = "http://localhost:3000";

        [SerializeField, Min(0.1f)]
        private float requestTimeoutSeconds = 15f;

        [SerializeField, Min(0.1f)]
        private float maximumFileSizeMegabytes = 5f;

        public bool VoiceInputEnabled => voiceInputEnabled;
        public bool KeyboardFallbackEnabled => keyboardFallbackEnabled;
        public float MaximumUtteranceSeconds => maximumUtteranceSeconds;
        public float PostCommandCooldownSeconds => postCommandCooldownSeconds;
        public VoiceTransport Transport => transport;
        public string BackendBaseUrl => backendBaseUrl;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;
        public float MaximumFileSizeMegabytes => maximumFileSizeMegabytes;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, maximumUtteranceSeconds, nameof(maximumUtteranceSeconds));
            GameConfigValidation.RequireNonNegative(this, postCommandCooldownSeconds, nameof(postCommandCooldownSeconds));
            GameConfigValidation.RequirePositive(this, requestTimeoutSeconds, nameof(requestTimeoutSeconds));
            GameConfigValidation.RequirePositive(this, maximumFileSizeMegabytes, nameof(maximumFileSizeMegabytes));
            GameConfigValidation.RequireTrue(
                this,
                keyboardFallbackEnabled,
                nameof(keyboardFallbackEnabled),
                "keyboard fallback must remain enabled during the MVP");
        }
    }
}
