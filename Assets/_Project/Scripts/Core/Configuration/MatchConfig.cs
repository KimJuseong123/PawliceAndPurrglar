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

        [SerializeField, Range(3f, 5f), Tooltip("READY phase countdown before gameplay starts.")]
        private float readyCountdownSeconds = 3f;

        public float MatchDurationSeconds => matchDurationSeconds;
        public int TargetSaleAmount => targetSaleAmount;
        public float ReadyCountdownSeconds => readyCountdownSeconds;

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, matchDurationSeconds, nameof(matchDurationSeconds));
            GameConfigValidation.RequirePositive(this, targetSaleAmount, nameof(targetSaleAmount));
            GameConfigValidation.RequireInRange(
                this,
                readyCountdownSeconds,
                3f,
                5f,
                nameof(readyCountdownSeconds));
        }
    }
}
