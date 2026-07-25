using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Player", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : GameConfigAsset
    {
        [Header("Movement")]
        [SerializeField, Min(0.01f), Tooltip("Normal world-space movement speed in meters per second.")]
        private float moveSpeed = 5f;

        [SerializeField, Min(0.01f), Tooltip("World-space movement speed while dashing.")]
        private float dashSpeed = 9f;

        [SerializeField, Min(0.01f), Tooltip("Duration of one dash in seconds.")]
        private float dashDurationSeconds = 0.25f;

        [SerializeField, Min(0f), Tooltip("Delay before another dash can start.")]
        private float dashCooldownSeconds = 5f;

        [Header("Interaction")]
        [SerializeField, Min(0.01f), Tooltip("Maximum distance for selecting an interactable target.")]
        private float interactionRange = 2f;

        public float MoveSpeed => moveSpeed;
        public float DashSpeed => dashSpeed;
        public float DashDurationSeconds => dashDurationSeconds;
        public float DashCooldownSeconds => dashCooldownSeconds;
        public float InteractionRange => interactionRange;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, moveSpeed, nameof(moveSpeed));
            GameConfigValidation.RequirePositive(this, dashSpeed, nameof(dashSpeed));
            GameConfigValidation.RequirePositive(this, dashDurationSeconds, nameof(dashDurationSeconds));
            GameConfigValidation.RequireNonNegative(this, dashCooldownSeconds, nameof(dashCooldownSeconds));
            GameConfigValidation.RequirePositive(this, interactionRange, nameof(interactionRange));

            if (dashSpeed <= moveSpeed)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(dashSpeed),
                    $"dash speed must exceed move speed ({moveSpeed}), received {dashSpeed}");
            }
        }
    }
}
