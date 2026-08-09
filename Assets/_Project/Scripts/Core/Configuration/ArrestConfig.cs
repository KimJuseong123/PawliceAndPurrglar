using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "PawliceAndPurrglar/Config/Arrest", fileName = "ArrestConfig")]
    public sealed class ArrestConfig : GameConfigAsset
    {
        [Header("Arrest")]
        [SerializeField, Min(0.01f), Tooltip("Maximum police-to-thief distance that allows arrest progress.")]
        private float arrestDistance = 1.75f;

        [SerializeField, Min(0.01f), Tooltip("Time the arrest conditions must remain valid.")]
        private float arrestDurationSeconds = 1.5f;

        /// <summary>
        /// How long a caught thief sits out before being put back on the map.
        ///
        /// This is time nobody is playing, so it is deliberately short. Three
        /// arrests at twenty seconds would be a full minute of a four-minute
        /// match spent watching, which is the cost that made a single-arrest
        /// match attractive in the first place.
        /// </summary>
        [SerializeField, Range(4f, 20f), Tooltip("Seconds an arrested thief is held before respawning.")]
        private float jailSeconds = 10f;

        [SerializeField, Min(0), Tooltip("Gold paid to the officer for each completed arrest. Paid on top of any confiscation, which only happens when the thief has sold something.")]
        private int arrestRewardGold = 200;

        public float ArrestDistance => arrestDistance;
        public float ArrestDurationSeconds => arrestDurationSeconds;
        public float JailSeconds => jailSeconds;

        /// <summary>
        /// What a catch is worth by itself.
        ///
        /// Confiscation already moves money on an arrest, but only from a thief
        /// who has sold something — early in a match the officer catches
        /// somebody with an empty wallet and gets nothing for it. A flat bounty
        /// means the first catch buys tools for the second, which is the loop
        /// the police economy is supposed to have.
        /// </summary>
        public int ArrestRewardGold => arrestRewardGold;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, arrestDistance, nameof(arrestDistance));
            GameConfigValidation.RequirePositive(this, arrestDurationSeconds, nameof(arrestDurationSeconds));
            GameConfigValidation.RequireInRange(
                this,
                jailSeconds,
                4f,
                20f,
                nameof(jailSeconds));
        }
    }
}
