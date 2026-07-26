using System;
using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Voice", fileName = "VoiceConfig")]
    public sealed class VoiceConfig : GameConfigAsset
    {
        [Header("Prototype Availability")]
        [SerializeField, Tooltip("Enables the approved bounded push-to-talk voice command slice.")]
        private bool voiceInputEnabled = true;

        [SerializeField, Tooltip("Keyboard commands must remain available when voice input fails or is disabled.")]
        private bool keyboardFallbackEnabled = true;

        [Header("Capture")]
        [SerializeField, Min(0.1f), Tooltip("Maximum push-to-talk capture length. This is a prototype hypothesis.")]
        private float maximumUtteranceSeconds = 5f;

        [SerializeField, Min(0.05f)]
        private float minimumUtteranceSeconds = 0.25f;

        [SerializeField, Min(1f)]
        private float requestTimeoutSeconds = 15f;

        [SerializeField]
        private string gatewayEndpoint =
            "http://127.0.0.1:8787/v1/voice-command";

        public bool VoiceInputEnabled => voiceInputEnabled;
        public bool KeyboardFallbackEnabled => keyboardFallbackEnabled;
        public float MaximumUtteranceSeconds => maximumUtteranceSeconds;
        public float MinimumUtteranceSeconds => minimumUtteranceSeconds;
        public float RequestTimeoutSeconds => requestTimeoutSeconds;
        public string GatewayEndpoint => gatewayEndpoint;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, maximumUtteranceSeconds, nameof(maximumUtteranceSeconds));
            GameConfigValidation.RequirePositive(this, minimumUtteranceSeconds, nameof(minimumUtteranceSeconds));
            GameConfigValidation.RequirePositive(this, requestTimeoutSeconds, nameof(requestTimeoutSeconds));
            GameConfigValidation.RequireTrue(
                this,
                keyboardFallbackEnabled,
                nameof(keyboardFallbackEnabled),
                "keyboard fallback must remain enabled during the MVP");
            if (minimumUtteranceSeconds > maximumUtteranceSeconds)
            {
                throw new GameConfigurationException(
                    $"{name}.{nameof(minimumUtteranceSeconds)} must not exceed {nameof(maximumUtteranceSeconds)}.");
            }

            if (string.IsNullOrWhiteSpace(gatewayEndpoint)
                || !Uri.TryCreate(
                    gatewayEndpoint,
                    UriKind.Absolute,
                    out Uri endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp
                    && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                throw new GameConfigurationException(
                    $"{name}.{nameof(gatewayEndpoint)} must be an absolute HTTP or HTTPS URL.");
            }
        }
    }
}
