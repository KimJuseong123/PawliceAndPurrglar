using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Pawlice and Purrglar/Config/Companion", fileName = "CompanionConfig")]
    public sealed class CompanionConfig : GameConfigAsset
    {
        [Header("Companion Movement")]
        [SerializeField, Min(0.01f), Tooltip("Dog and cat movement speed in meters per second.")]
        private float moveSpeed = 4.5f;

        [Header("Commands")]
        [SerializeField, Min(0f), Tooltip("Shared prototype cooldown after a command succeeds.")]
        private float commandCooldownSeconds = 2f;

        public float MoveSpeed => moveSpeed;
        public float CommandCooldownSeconds => commandCooldownSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, moveSpeed, nameof(moveSpeed));
            GameConfigValidation.RequireNonNegative(this, commandCooldownSeconds, nameof(commandCooldownSeconds));
        }
    }
}
