using System;
using PawsAndLoot.Core;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Match
{
    public sealed class MatchResultFlowController : MonoBehaviour
    {
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private MatchEndController matchEndController;

        private bool _subscribed;
        private bool _transitionStarted;

        public event Action<MatchResult> ResultSceneRequested;

        public bool TransitionStarted => _transitionStarted;

        public void Configure(
            MatchRuntimeState configuredMatchRuntime,
            MatchEndController configuredMatchEndController)
        {
            Unsubscribe();
            matchRuntime = configuredMatchRuntime;
            matchEndController = configuredMatchEndController;
            _transitionStarted = false;
            ValidateOrThrow();
            Subscribe();
        }

        public void ValidateOrThrow()
        {
            if (matchRuntime == null || matchEndController == null)
            {
                throw new InvalidOperationException(
                    $"MatchResultFlowController '{name}' has missing references.");
            }

            matchEndController.ValidateOrThrow();
        }

        private void Subscribe()
        {
            if (_subscribed || matchEndController == null)
            {
                return;
            }

            matchEndController.MatchEndingStarted +=
                HandleMatchEndingStarted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted -=
                    HandleMatchEndingStarted;
            }

            _subscribed = false;
        }

        private void HandleMatchEndingStarted(MatchResult result)
        {
            if (_transitionStarted
                || matchRuntime.CurrentState != MatchState.Ending
                || !MatchResultSession.TryStore(result)
                || !matchRuntime.TryTransitionTo(MatchState.Result))
            {
                return;
            }

            _transitionStarted = true;
            GameLogger.Info(
                GameLogCategory.Match,
                "Opening the result scene.",
                this);
            ResultSceneRequested?.Invoke(result);
            GameSceneLoader.Load(GameSceneId.Result);
        }

        private void Awake()
        {
            MatchResultSession.Clear();
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
