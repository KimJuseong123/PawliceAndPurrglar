using System;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Arrest
{
    public sealed class ArrestCompletionController : MonoBehaviour
    {
        public const int DefaultRequiredCatchCount = 3;

        [SerializeField]
        private ArrestProgressController progressController;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField, Min(1)]
        private int requiredCatchCount = DefaultRequiredCatchCount;

        public event Action ArrestCompleted;
        public event Action<int, int> CatchCountChanged;
        public event Action PoliceVictoryRequested;

        public bool IsCompleted { get; private set; }
        public int CurrentCatchCount { get; private set; }
        public int RequiredCatchCount => Mathf.Max(1, requiredCatchCount);

        public void Configure(
            ArrestProgressController configuredProgressController,
            MatchRuntimeState configuredMatchRuntime,
            int configuredRequiredCatchCount = DefaultRequiredCatchCount)
        {
            progressController = configuredProgressController;
            matchRuntime = configuredMatchRuntime;
            requiredCatchCount = Mathf.Max(1, configuredRequiredCatchCount);
            IsCompleted = false;
            CurrentCatchCount = 0;
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

            CurrentCatchCount++;
            bool reachedVictory =
                CurrentCatchCount >= RequiredCatchCount;
            IsCompleted = reachedVictory;
            GameLogger.Info(
                GameLogCategory.Arrest,
                reachedVictory
                    ? "Required arrests completed. Police victory requested."
                    : $"Arrest completed ({CurrentCatchCount}/{RequiredCatchCount}).",
                this);
            if (!reachedVictory)
            {
                progressController.ResetProgress();
            }

            ArrestCompleted?.Invoke();
            CatchCountChanged?.Invoke(CurrentCatchCount, RequiredCatchCount);
            if (reachedVictory)
            {
                PoliceVictoryRequested?.Invoke();
            }

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
