using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Companion", fileName = "CompanionConfig")]
    public sealed class CompanionConfig : GameConfigAsset
    {
        [Header("Companion Movement")]
        [SerializeField, Min(0.01f), Tooltip("Dog and cat movement speed in meters per second.")]
        private float moveSpeed = 4.5f;

        [Header("Commands")]
        [SerializeField, Min(0f), Tooltip("Shared prototype cooldown after a command succeeds.")]
        private float commandCooldownSeconds = 2f;

        [SerializeField, Min(0.1f)]
        private float followDistance = 1.5f;

        [SerializeField, Min(0.01f)]
        private float arrivalTolerance = 0.35f;

        [SerializeField, Min(0.1f)]
        private float commandTimeoutSeconds = 8f;

        [SerializeField, Min(0f)]
        private float commandExecuteSeconds = 0.25f;

        [SerializeField, Min(0.1f)]
        private float distractionDistance = 2f;

        [SerializeField, Min(0.1f)]
        private float distractionDurationSeconds = 1f;

        [SerializeField, Min(0.1f)]
        private float targetSampleRadius = 2f;

        public float MoveSpeed => moveSpeed;
        public float CommandCooldownSeconds => commandCooldownSeconds;
        public float FollowDistance => followDistance;
        public float ArrivalTolerance => arrivalTolerance;
        public float CommandTimeoutSeconds => commandTimeoutSeconds;
        public float CommandExecuteSeconds => commandExecuteSeconds;
        public float DistractionDistance => distractionDistance;
        public float DistractionDurationSeconds =>
            distractionDurationSeconds;
        public float TargetSampleRadius => targetSampleRadius;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, moveSpeed, nameof(moveSpeed));
            GameConfigValidation.RequireNonNegative(this, commandCooldownSeconds, nameof(commandCooldownSeconds));
            GameConfigValidation.RequirePositive(this, followDistance, nameof(followDistance));
            GameConfigValidation.RequirePositive(this, arrivalTolerance, nameof(arrivalTolerance));
            GameConfigValidation.RequirePositive(this, commandTimeoutSeconds, nameof(commandTimeoutSeconds));
            GameConfigValidation.RequireNonNegative(this, commandExecuteSeconds, nameof(commandExecuteSeconds));
            GameConfigValidation.RequirePositive(this, distractionDistance, nameof(distractionDistance));
            GameConfigValidation.RequirePositive(this, distractionDurationSeconds, nameof(distractionDurationSeconds));
            GameConfigValidation.RequirePositive(this, targetSampleRadius, nameof(targetSampleRadius));
        }
    }
}
