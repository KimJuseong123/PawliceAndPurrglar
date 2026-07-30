using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(
        menuName = "Paws & Loot/Config/Pet Cognition",
        fileName = "PetCognitionConfig")]
    public sealed class PetCognitionConfig : GameConfigAsset
    {
        [Header("Decision thresholds")]
        [SerializeField, Range(0f, 1f)]
        private float dogCorrectThreshold = 0.62f;

        [SerializeField, Range(0f, 1f)]
        private float catCorrectThreshold = 0.78f;

        [SerializeField, Range(0f, 1f)]
        private float confusedThreshold = 0.34f;

        [Header("MVP personality")]
        [SerializeField, Range(0f, 1f)]
        private float dogObedience = 0.9f;

        [SerializeField, Range(0f, 1f)]
        private float catObedience = 0.52f;

        [SerializeField, Range(0f, 1f)]
        private float defaultAttention = 0.85f;

        [SerializeField, Range(0f, 1f)]
        private float defaultDistraction = 0.05f;

        public float DogCorrectThreshold => dogCorrectThreshold;
        public float CatCorrectThreshold => catCorrectThreshold;
        public float ConfusedThreshold => confusedThreshold;
        public float DogObedience => dogObedience;
        public float CatObedience => catObedience;
        public float DefaultAttention => defaultAttention;
        public float DefaultDistraction => defaultDistraction;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequireInRange(
                this,
                dogCorrectThreshold,
                0f,
                1f,
                nameof(dogCorrectThreshold));
            GameConfigValidation.RequireInRange(
                this,
                catCorrectThreshold,
                0f,
                1f,
                nameof(catCorrectThreshold));
            GameConfigValidation.RequireInRange(
                this,
                confusedThreshold,
                0f,
                1f,
                nameof(confusedThreshold));
            GameConfigValidation.RequireInRange(
                this,
                dogObedience,
                0f,
                1f,
                nameof(dogObedience));
            GameConfigValidation.RequireInRange(
                this,
                catObedience,
                0f,
                1f,
                nameof(catObedience));
            GameConfigValidation.RequireInRange(
                this,
                defaultAttention,
                0f,
                1f,
                nameof(defaultAttention));
            GameConfigValidation.RequireInRange(
                this,
                defaultDistraction,
                0f,
                1f,
                nameof(defaultDistraction));
        }
    }
}
