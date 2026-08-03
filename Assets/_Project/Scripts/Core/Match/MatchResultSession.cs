using System;

namespace PawsAndLoot.Match
{
    public static class MatchResultSession
    {
        private static MatchResult _currentResult;
        private static MatchSummary _currentSummary;

        public static bool HasResult { get; private set; }

        public static MatchResult CurrentResult =>
            HasResult
                ? _currentResult
                : throw new InvalidOperationException(
                    "No completed match result is available.");

        public static bool TryStore(MatchResult result)
        {
            if (HasResult)
            {
                return false;
            }

            _currentResult = result;
            HasResult = true;
            return true;
        }

        public static bool TryGet(out MatchResult result)
        {
            result = _currentResult;
            return HasResult;
        }

        /// <summary>
        /// Records the match's counters. Written by the evaluator when it
        /// decides, which is before the verdict itself is stored.
        ///
        /// Not folded into <see cref="TryStore"/>, whose first-write-wins guard
        /// exists to stop a second verdict overwriting the first. The summary
        /// is a report about the same match, not a competing answer, and the
        /// evaluator is the only thing that holds every input it needs.
        /// </summary>
        public static void ReportSummary(MatchSummary summary)
        {
            _currentSummary = summary;
        }

        public static bool TryGetSummary(out MatchSummary summary)
        {
            summary = _currentSummary;
            return HasResult;
        }

        public static void Clear()
        {
            _currentResult = default;
            _currentSummary = default;
            HasResult = false;
        }
    }
}
