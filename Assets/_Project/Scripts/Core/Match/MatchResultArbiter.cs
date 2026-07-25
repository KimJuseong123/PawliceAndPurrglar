using System;

namespace PawsAndLoot.Match
{
    public sealed class MatchResultArbiter
    {
        private bool _arrestRequested;
        private bool _saleCheckRequested;
        private bool _timeoutRequested;

        public bool HasResult { get; private set; }
        public MatchResult CurrentResult { get; private set; }

        public bool RequestArrest()
        {
            return QueueRequest(ref _arrestRequested);
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

            if (HasResult)
            {
                result = CurrentResult;
                return false;
            }

            if (_arrestRequested)
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

        public void Reset()
        {
            _arrestRequested = false;
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
            _arrestRequested = false;
            _saleCheckRequested = false;
            _timeoutRequested = false;
            result = CurrentResult;
            return true;
        }
    }
}
