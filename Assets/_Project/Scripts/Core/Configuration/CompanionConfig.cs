using UnityEngine;

namespace PawliceAndPurrglar.Config
{
    [CreateAssetMenu(menuName = "PawliceAndPurrglar/Config/Companion", fileName = "CompanionConfig")]
    public sealed class CompanionConfig : GameConfigAsset
    {
        [Header("Companion Movement")]
        [SerializeField, Min(0.01f), Tooltip("Dog and cat movement speed in meters per second.")]
        private float moveSpeed = 4.5f;

        [Header("Commands")]
        [SerializeField, Min(0f), Tooltip("Shared prototype cooldown after a command succeeds.")]
        private float commandCooldownSeconds = 2f;

        [Header("CAT-010 Bite")]
        [SerializeField, Min(0f), Tooltip("How long the officer is held after the cat reaches them. Matches ThrowableCatalog.RockStunSeconds on purpose — a bite that held longer than a rock would be a balance change hiding inside a new command.")]
        private float biteStunSeconds = 1.2f;

        public float MoveSpeed => moveSpeed;
        public float CommandCooldownSeconds => commandCooldownSeconds;
        public float BiteStunSeconds => biteStunSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, moveSpeed, nameof(moveSpeed));
            GameConfigValidation.RequireNonNegative(this, commandCooldownSeconds, nameof(commandCooldownSeconds));
            GameConfigValidation.RequireNonNegative(this, biteStunSeconds, nameof(biteStunSeconds));
        }
    }
}
