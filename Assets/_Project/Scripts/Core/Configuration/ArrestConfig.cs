using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Arrest", fileName = "ArrestConfig")]
    public sealed class ArrestConfig : GameConfigAsset
    {
        [Header("Arrest")]
        [SerializeField, Min(0.01f), Tooltip("Maximum police-to-thief distance that allows arrest progress.")]
        private float arrestDistance = 1.75f;

        [SerializeField, Min(0.01f), Tooltip("Time the arrest conditions must remain valid.")]
        private float arrestDurationSeconds = 1.5f;

        public float ArrestDistance => arrestDistance;
        public float ArrestDurationSeconds => arrestDurationSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, arrestDistance, nameof(arrestDistance));
            GameConfigValidation.RequirePositive(this, arrestDurationSeconds, nameof(arrestDurationSeconds));
        }
    }
}
