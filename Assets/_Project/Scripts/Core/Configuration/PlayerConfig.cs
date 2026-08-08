using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "PawliceAndPurrglar/Config/Player", fileName = "PlayerConfig")]
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

        [SerializeField, Range(0.01f, 1f), Tooltip("Movement multiplier while carrying loot.")]
        private float lootCarrySpeedMultiplier = 0.9f;

        [SerializeField, Min(0.01f), Tooltip("Upward speed at the moment of a jump, in meters per second.")]
        private float jumpSpeed = 5.4f;

        [Header("Interaction")]
        [SerializeField, Min(0.01f), Tooltip("Maximum distance for selecting an interactable target.")]
        private float interactionRange = 2f;

        public float MoveSpeed => moveSpeed;
        public float DashSpeed => dashSpeed;
        public float DashDurationSeconds => dashDurationSeconds;
        public float DashCooldownSeconds => dashCooldownSeconds;
        public float LootCarrySpeedMultiplier =>
            lootCarrySpeedMultiplier;

        /// <summary>
        /// Upward speed at take-off, not a height, because that is what the motor
        /// adds to its own falling speed.
        ///
        /// 5.4 m/s against Unity's -9.81 gravity is v squared over 2g = 1.49 m. The
        /// figure comes from the furniture indoors: the rooms are the house model at
        /// 2.2x, so a sofa base is a metre tall, a kitchen counter base 1.85 m and a
        /// dining chair seat 1.2 m. 1.17 m cleared the sofa and nothing else, which
        /// is why this went up.
        /// </summary>
        public float JumpSpeed => jumpSpeed;

        /// <summary>
        /// How high that take-off speed actually reaches, so a test can talk about
        /// clearing furniture rather than about a velocity.
        /// </summary>
        public float JumpHeight =>
            jumpSpeed * jumpSpeed / (2f * Mathf.Abs(Physics.gravity.y));
        public float InteractionRange => interactionRange;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, moveSpeed, nameof(moveSpeed));
            GameConfigValidation.RequirePositive(this, dashSpeed, nameof(dashSpeed));
            GameConfigValidation.RequirePositive(this, dashDurationSeconds, nameof(dashDurationSeconds));
            GameConfigValidation.RequireNonNegative(this, dashCooldownSeconds, nameof(dashCooldownSeconds));
            GameConfigValidation.RequirePositive(this, lootCarrySpeedMultiplier, nameof(lootCarrySpeedMultiplier));
            GameConfigValidation.RequirePositive(this, interactionRange, nameof(interactionRange));
            GameConfigValidation.RequirePositive(this, jumpSpeed, nameof(jumpSpeed));

            if (dashSpeed <= moveSpeed)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(dashSpeed),
                    $"dash speed must exceed move speed ({moveSpeed}), received {dashSpeed}");
            }

            if (lootCarrySpeedMultiplier > 1f)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(lootCarrySpeedMultiplier),
                    $"carry multiplier cannot exceed 1, received {lootCarrySpeedMultiplier}");
            }
        }
    }
}
