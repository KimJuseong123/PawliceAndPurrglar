using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Match", fileName = "MatchConfig")]
    public sealed class MatchConfig : GameConfigAsset
    {
        [Header("Match Rules")]
        [SerializeField, Min(1f), Tooltip("Length of the PLAYING phase in seconds.")]
        private float matchDurationSeconds = 240f;

        [SerializeField, Min(1), Tooltip("Gold the thief must sell to win.")]
        private int targetSaleAmount = 1000;

        /// <summary>
        /// The officer's threshold, sitting beside the thief's on purpose.
        ///
        /// One arrest used to end the match, so a four-minute game could be over
        /// in thirty seconds and one mistake cost the thief everything. Three
        /// catches with a spell in the cells between them keeps both sides
        /// playing for the whole clock.
        /// </summary>
        [SerializeField, Min(1), Tooltip("Arrests the police must complete to win.")]
        private int arrestsToWin = 3;

        [SerializeField, Range(3f, 5f), Tooltip("READY phase countdown before gameplay starts.")]
        private float readyCountdownSeconds = 3f;

        public float MatchDurationSeconds => matchDurationSeconds;
        public int TargetSaleAmount => targetSaleAmount;
        public int ArrestsToWin => arrestsToWin;
        public float ReadyCountdownSeconds => readyCountdownSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, matchDurationSeconds, nameof(matchDurationSeconds));
            GameConfigValidation.RequirePositive(this, targetSaleAmount, nameof(targetSaleAmount));
            GameConfigValidation.RequirePositive(this, arrestsToWin, nameof(arrestsToWin));
            GameConfigValidation.RequireInRange(
                this,
                readyCountdownSeconds,
                3f,
                5f,
                nameof(readyCountdownSeconds));
        }
    }
}
