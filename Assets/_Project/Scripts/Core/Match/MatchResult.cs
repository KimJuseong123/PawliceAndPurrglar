using System;

namespace PawliceAndPurrglar.Match
{
    [Serializable]
    public readonly struct MatchResult
    {
        public MatchResult(
            MatchWinner winner,
            MatchEndReason reason,
            int soldAmount,
            float remainingSeconds)
        {
            if (!Enum.IsDefined(typeof(MatchWinner), winner))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(winner),
                    winner,
                    "Unknown match winner.");
            }

            if (!Enum.IsDefined(typeof(MatchEndReason), reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "Unknown match end reason.");
            }

            Winner = winner;
            Reason = reason;
            SoldAmount = Math.Max(0, soldAmount);
            RemainingSeconds = Math.Max(0f, remainingSeconds);
        }

        public MatchWinner Winner { get; }
        public MatchEndReason Reason { get; }
        public int SoldAmount { get; }
        public float RemainingSeconds { get; }
    }
}
