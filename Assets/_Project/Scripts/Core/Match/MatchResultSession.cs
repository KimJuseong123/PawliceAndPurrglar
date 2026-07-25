using System;

namespace PawsAndLoot.Match
{
    public static class MatchResultSession
    {
        private static MatchResult _currentResult;

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

        public static void Clear()
        {
            _currentResult = default;
            HasResult = false;
        }
    }
}
