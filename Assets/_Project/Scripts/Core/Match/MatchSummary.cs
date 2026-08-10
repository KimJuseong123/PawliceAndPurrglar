using System;

namespace PawliceAndPurrglar.Match
{
    /// <summary>
    /// What the match looked like, for the result screen to report.
    ///
    /// Deliberately separate from <see cref="MatchResult"/>. That type is the
    /// verdict and only <c>MatchResultArbiter</c> may produce it; hanging
    /// presentation counters off it would put reporting data on the one type
    /// the rules layer owns, and would change every existing caller of a
    /// constructor whose whole job is to be hard to get wrong.
    ///
    /// Every field here is read from objects the evaluator already holds at the
    /// moment it decides, so nothing new has to be tracked during play.
    /// </summary>
    [Serializable]
    public readonly struct MatchSummary
    {
        public MatchSummary(
            float elapsedSeconds,
            int catchCount,
            int requiredCatchCount,
            int soldCount,
            int soldAmount,
            int targetAmount)
        {
            ElapsedSeconds = Math.Max(0f, elapsedSeconds);
            CatchCount = Math.Max(0, catchCount);
            RequiredCatchCount = Math.Max(1, requiredCatchCount);
            SoldCount = Math.Max(0, soldCount);
            SoldAmount = Math.Max(0, soldAmount);
            TargetAmount = Math.Max(1, targetAmount);
        }

        /// <summary>
        /// How long the match ran. The result carries how long was left, which
        /// is the opposite of what a player wants to read afterwards.
        /// </summary>
        public float ElapsedSeconds { get; }

        /// <summary>Arrests the police completed.</summary>
        public int CatchCount { get; }

        /// <summary>Arrests the police needed.</summary>
        public int RequiredCatchCount { get; }

        /// <summary>Treasures the thief actually sold, not their value.</summary>
        public int SoldCount { get; }

        public int SoldAmount { get; }

        public int TargetAmount { get; }

        /// <summary>
        /// Whether an evaluator actually filled this in.
        ///
        /// The default value of the struct is not a match that scored nothing;
        /// it is a match nobody reported on, which happens when a result is
        /// stored directly — a test, or a scene opened on its own. The
        /// constructor floors the target at one, so a zero here can only be the
        /// default.
        /// </summary>
        public bool IsReported => TargetAmount > 0;
    }
}
