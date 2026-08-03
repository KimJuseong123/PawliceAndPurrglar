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

        /// <summary>
        /// Arrests recorded so far, for the HUD to show and the jail to react
        /// to. Reading it from the arbiter keeps one count rather than two that
        /// can disagree.
        /// </summary>
        public int ArrestCount => _arbiter.ArrestCount;

        /// <summary>
        /// Whether this machine gets to decide. Clients are told.
        ///
        /// Left true by default so a single-player editor scene and every test
        /// keeps working untouched; the network layer turns it off on the
        /// client the same way it does for interiors and the jail.
        /// </summary>
        public bool HasAuthority { get; private set; } = true;

        public void SetAuthority(bool hasAuthority)
        {
            HasAuthority = hasAuthority;
        }

        /// <summary>
        /// Takes the host's verdict as final.
        ///
        /// Goes through the same arbiter the local path uses, so everything
        /// downstream — the ending, the result screen, the rematch — sees one
        /// kind of decided match rather than two.
        /// </summary>
        public bool AdoptDecidedResult(MatchResult result)
        {
            if (_arbiter.HasResult)
            {
                return false;
            }

            _arbiter.Adopt(result);
            GameLogger.Info(
                GameLogCategory.Match,
                $"Match result received from host: {result.Winner} / "
                + $"{result.Reason}.",
                this);
            ResultDecided?.Invoke(result);
            return true;
        }

        public bool EvaluatePendingRequests()
        {
            // A client that works the result out for itself will eventually
            // disagree with the host, and did: its arrest count stalls because
            // the release that re-arms the next catch is a host-side timer.
            if (!HasAuthority)
            {
                return false;
            }

            if (!_arbiter.TryResolve(
                    thiefWallet.SoldAmount,
                    thiefWallet.TargetAmount,
                    matchRuntime.ArrestsToWin,
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
