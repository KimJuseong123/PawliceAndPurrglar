using System;

namespace PawsAndLoot.Match
{
    public sealed class MatchResultArbiter
    {
        private bool _saleCheckRequested;
        private bool _timeoutRequested;

        public bool HasResult { get; private set; }
        public MatchResult CurrentResult { get; private set; }

        /// <summary>
        /// How many times the thief has been caught this match.
        ///
        /// One arrest used to end it, which made a four-minute match capable of
        /// finishing in thirty seconds and gave the thief no way back from a
        /// single mistake. Counting means the officer has to do it three times
        /// and the chase resumes in between.
        /// </summary>
        public int ArrestCount { get; private set; }

        /// <summary>
        /// Records a completed arrest. Unlike the sale and timeout requests this
        /// is not deduplicated here: each call is a separate catch, and the
        /// caller latches its own completion so one arrest cannot report twice.
        /// </summary>
        public bool RequestArrest()
        {
            if (HasResult)
            {
                return false;
            }

            ArrestCount++;
            return true;
        }

        public bool RequestSaleCheck()
        {
            return QueueRequest(ref _saleCheckRequested);
        }

        public bool RequestTimeout()
        {
            return QueueRequest(ref _timeoutRequested);
        }

        public bool TryResolve(
            int soldAmount,
            int targetAmount,
            int arrestsToWin,
            float remainingSeconds,
            out MatchResult result)
        {
            if (targetAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetAmount),
                    targetAmount,
                    "Target amount must be positive.");
            }

            if (arrestsToWin <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arrestsToWin),
                    arrestsToWin,
                    "Arrests to win must be positive.");
            }

            if (HasResult)
            {
                result = CurrentResult;
                return false;
            }

            if (ArrestCount >= arrestsToWin)
            {
                return Decide(
                    MatchWinner.Police,
                    MatchEndReason.ThiefArrested,
                    soldAmount,
                    remainingSeconds,
                    out result);
            }

            if ((_saleCheckRequested || _timeoutRequested)
                && soldAmount >= targetAmount)
            {
                return Decide(
                    MatchWinner.Thief,
                    MatchEndReason.SaleTargetReached,
                    soldAmount,
                    remainingSeconds,
                    out result);
            }

            if (_timeoutRequested)
            {
                return Decide(
                    MatchWinner.Police,
                    MatchEndReason.TimeExpiredBelowTarget,
                    soldAmount,
                    remainingSeconds,
                    out result);
            }

            _saleCheckRequested = false;
            result = default;
            return false;
        }

        /// <summary>
        /// Stores a verdict decided elsewhere, for a client mirroring the host.
        /// Latches exactly like a locally decided one so a late local request
        /// cannot overwrite it.
        /// </summary>
        public void Adopt(MatchResult result)
        {
            if (HasResult)
            {
                return;
            }

            CurrentResult = result;
            HasResult = true;
            _saleCheckRequested = false;
            _timeoutRequested = false;
        }

        public void Reset()
        {
            ArrestCount = 0;
            _saleCheckRequested = false;
            _timeoutRequested = false;
            HasResult = false;
            CurrentResult = default;
        }

        private bool QueueRequest(ref bool request)
        {
            if (HasResult || request)
            {
                return false;
            }

            request = true;
            return true;
        }

        private bool Decide(
            MatchWinner winner,
            MatchEndReason reason,
            int soldAmount,
            float remainingSeconds,
            out MatchResult result)
        {
            CurrentResult = new MatchResult(
                winner,
                reason,
                soldAmount,
                remainingSeconds);
            HasResult = true;
            _saleCheckRequested = false;
            _timeoutRequested = false;
            result = CurrentResult;
            return true;
        }
    }
}
