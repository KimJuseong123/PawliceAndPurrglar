using System;

namespace PawliceAndPurrglar.Match
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
        /// Takes the host's verdict, whatever this machine had worked out.
        ///
        /// Separate from <see cref="TryStore"/> because the two answer different
        /// questions. That one is first-write-wins so a local re-decision cannot
        /// overwrite a verdict already being shown; this one is the host
        /// speaking, and on a client the host's word is the only one that counts.
        ///
        /// It exists because the client's result screen was reading an **empty**
        /// store. The verdict reached the client as a named message, which handed
        /// it to the in-scene evaluator, which raised the event the flow
        /// controller stores from — and every step of that chain lives in the
        /// match scene, which the host's own scene load is in the middle of
        /// unloading. Whether the client got a result screen with numbers on it
        /// or the "no result" fallback came down to which of the two arrived
        /// first. The store is a static, so writing it here cannot lose that
        /// race.
        /// </summary>
        public static void AdoptAuthoritative(MatchResult result)
        {
            _currentResult = result;
            HasResult = true;
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
