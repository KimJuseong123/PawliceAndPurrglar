using System;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Match
{
    public sealed class MatchResultEvaluator : MonoBehaviour
    {
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private ThiefLootWallet thiefWallet;

        [SerializeField]
        private ArrestCompletionController arrestCompletion;

        private readonly MatchResultArbiter _arbiter = new();
        private bool _subscribed;

        public event Action<MatchResult> ResultDecided;

        public bool HasResult => _arbiter.HasResult;
        public MatchResult CurrentResult => _arbiter.CurrentResult;

        public void Configure(
            MatchRuntimeState configuredMatchRuntime,
            ThiefLootWallet configuredThiefWallet,
            ArrestCompletionController configuredArrestCompletion)
        {
            Unsubscribe();
            matchRuntime = configuredMatchRuntime;
            thiefWallet = configuredThiefWallet;
            arrestCompletion = configuredArrestCompletion;
            _arbiter.Reset();
            ValidateOrThrow();
            Subscribe();
        }

        public bool RequestArrestVictory()
        {
            return CanAcceptRequest() && _arbiter.RequestArrest();
        }

        public bool RequestSaleVictoryCheck()
        {
            return CanAcceptRequest() && _arbiter.RequestSaleCheck();
        }

        public bool RequestTimeExpired()
        {
            return CanAcceptRequest() && _arbiter.RequestTimeout();
        }

        public bool EvaluatePendingRequests()
        {
            if (!_arbiter.TryResolve(
                    thiefWallet.SoldAmount,
                    thiefWallet.TargetAmount,
                    matchRuntime.RemainingMatchSeconds,
                    out MatchResult result))
            {
                return false;
            }

            // Reported before the verdict is handed on: this is the one place
            // that holds the wallet, the arrest counter and the clock at the
            // instant the match ended. A moment later the match scene unloads
            // and every one of them reads zero.
            MatchResultSession.ReportSummary(BuildSummary());

            GameLogger.Info(
                GameLogCategory.Match,
                $"Match result decided: {result.Winner} / {result.Reason}.",
                this);
            ResultDecided?.Invoke(result);
            return true;
        }

        private MatchSummary BuildSummary()
        {
            return new MatchSummary(
                matchRuntime.MatchDurationSeconds
                - matchRuntime.RemainingMatchSeconds,
                arrestCompletion.CurrentCatchCount,
                arrestCompletion.RequiredCatchCount,
                thiefWallet.CreditedSaleCount,
                thiefWallet.SoldAmount,
                thiefWallet.TargetAmount);
        }

        public void ValidateOrThrow()
        {
            if (matchRuntime == null
                || thiefWallet == null
                || arrestCompletion == null)
            {
                throw new InvalidOperationException(
                    $"MatchResultEvaluator '{name}' has missing references.");
            }

            thiefWallet.ValidateOrThrow();
            arrestCompletion.ValidateOrThrow();
        }

        private bool CanAcceptRequest()
        {
            return !_arbiter.HasResult
                && matchRuntime != null
                && matchRuntime.CurrentState == MatchState.Playing;
        }

        private void Subscribe()
        {
            if (_subscribed
                || matchRuntime == null
                || thiefWallet == null
                || arrestCompletion == null)
            {
                return;
            }

            matchRuntime.TimerExpired += HandleTimerExpired;
            thiefWallet.VictoryCheckRequested +=
                HandleSaleVictoryCheckRequested;
            arrestCompletion.PoliceVictoryRequested +=
                HandlePoliceVictoryRequested;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (matchRuntime != null)
            {
                matchRuntime.TimerExpired -= HandleTimerExpired;
            }

            if (thiefWallet != null)
            {
                thiefWallet.VictoryCheckRequested -=
                    HandleSaleVictoryCheckRequested;
            }

            if (arrestCompletion != null)
            {
                arrestCompletion.PoliceVictoryRequested -=
                    HandlePoliceVictoryRequested;
            }

            _subscribed = false;
        }

        private void HandleTimerExpired()
        {
            RequestTimeExpired();
        }

        private void HandleSaleVictoryCheckRequested()
        {
            RequestSaleVictoryCheck();
        }

        private void HandlePoliceVictoryRequested()
        {
            RequestArrestVictory();
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void LateUpdate()
        {
            EvaluatePendingRequests();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
