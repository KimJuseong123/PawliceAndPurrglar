using System;
using PawliceAndPurrglar.Gameplay.Arrest;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Match
{
    public sealed class MatchEndController : MonoBehaviour
    {
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private MatchResultEvaluator resultEvaluator;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private ArrestProgressController arrestProgress;

        private bool _subscribed;

        public event Action<MatchResult> MatchEndingStarted;

        public bool HasEnded { get; private set; }
        public MatchResult FinalResult { get; private set; }

        public void Configure(
            MatchRuntimeState configuredMatchRuntime,
            MatchResultEvaluator configuredResultEvaluator,
            LocalPlayerRoleSelector configuredRoleSelector,
            ArrestProgressController configuredArrestProgress)
        {
            Unsubscribe();
            matchRuntime = configuredMatchRuntime;
            resultEvaluator = configuredResultEvaluator;
            roleSelector = configuredRoleSelector;
            arrestProgress = configuredArrestProgress;
            HasEnded = false;
            FinalResult = default;
            ValidateOrThrow();
            Subscribe();
        }

        public bool TryEndMatch(MatchResult result)
        {
            if (HasEnded
                || matchRuntime == null
                || matchRuntime.CurrentState != MatchState.Playing
                || !matchRuntime.TryTransitionTo(MatchState.Ending))
            {
                return false;
            }

            HasEnded = true;
            FinalResult = result;
            roleSelector.DisableGameplayInput();
            arrestProgress.HandleMatchEnded();

            GameLogger.Info(
                GameLogCategory.Match,
                $"Match ending started: {result.Winner} / {result.Reason}.",
                this);
            MatchEndingStarted?.Invoke(result);
            return true;
        }

        public void ValidateOrThrow()
        {
            if (matchRuntime == null
                || resultEvaluator == null
                || roleSelector == null
                || arrestProgress == null)
            {
                throw new InvalidOperationException(
                    $"MatchEndController '{name}' has missing references.");
            }

            resultEvaluator.ValidateOrThrow();
            arrestProgress.ValidateOrThrow();
        }

        private void Subscribe()
        {
            if (_subscribed || resultEvaluator == null)
            {
                return;
            }

            resultEvaluator.ResultDecided += HandleResultDecided;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (resultEvaluator != null)
            {
                resultEvaluator.ResultDecided -= HandleResultDecided;
            }

            _subscribed = false;
        }

        private void HandleResultDecided(MatchResult result)
        {
            TryEndMatch(result);
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
