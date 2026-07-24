using UnityEngine;

namespace PawsAndLoot.Config
{
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

        public bool VoiceInputEnabled => voiceInputEnabled;
        public bool KeyboardFallbackEnabled => keyboardFallbackEnabled;
        public float MaximumUtteranceSeconds => maximumUtteranceSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, maximumUtteranceSeconds, nameof(maximumUtteranceSeconds));
            GameConfigValidation.RequireTrue(
                this,
                keyboardFallbackEnabled,
                nameof(keyboardFallbackEnabled),
                "keyboard fallback must remain enabled during the MVP");
        }
    }
}
