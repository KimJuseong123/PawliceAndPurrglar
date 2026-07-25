using System;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Arrest
{
    public sealed class ArrestCompletionController : MonoBehaviour
    {
        [SerializeField]
        private ArrestProgressController progressController;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        public event Action ArrestCompleted;
        public event Action PoliceVictoryRequested;
        public event Action ResultPresentationRequested;

        public bool IsCompleted { get; private set; }

        public void Configure(
            ArrestProgressController configuredProgressController,
            MatchRuntimeState configuredMatchRuntime)
        {
            progressController = configuredProgressController;
            matchRuntime = configuredMatchRuntime;
            IsCompleted = false;
            ValidateOrThrow();
        }

        public bool TryCompleteArrest()
        {
            if (IsCompleted
                || matchRuntime == null
                || matchRuntime.CurrentState != MatchState.Playing
                || progressController == null
                || !progressController.TryMarkCompleted())
            {
                return false;
            }

            if (!matchRuntime.TryTransitionTo(MatchState.Ending))
            {
                progressController.ResetProgress();
                throw new InvalidOperationException(
                    "Arrest completion could not enter the Ending state.");
            }

            IsCompleted = true;
            GameLogger.Info(
                GameLogCategory.Arrest,
                "Arrest completed. Police victory requested.",
                this);
            ArrestCompleted?.Invoke();
            PoliceVictoryRequested?.Invoke();
            ResultPresentationRequested?.Invoke();
            return true;
        }

        public void ValidateOrThrow()
        {
            if (progressController == null || matchRuntime == null)
            {
                throw new InvalidOperationException(
                    $"ArrestCompletionController '{name}' has missing references.");
            }
        }

        private void Awake()
        {
            ValidateOrThrow();
        }

        private void Update()
        {
            TryCompleteArrest();
        }
    }
}
