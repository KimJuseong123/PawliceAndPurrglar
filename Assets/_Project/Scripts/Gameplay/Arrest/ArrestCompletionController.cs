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

        /// <summary>
        /// Whether an arrest has landed and not yet been served.
        ///
        /// This used to be a one-way latch because one arrest ended the match,
        /// so nothing ever needed to happen afterwards. Now the thief comes back
        /// and can be caught again, and the latch is what stops a single catch
        /// from being counted every frame while the officer is still standing
        /// on them. <see cref="ClearForNextArrest"/> is what re-arms it, and
        /// that is called when the thief is released rather than on a timer —
        /// releasing is the moment they can be caught again.
        /// </summary>
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

        /// <summary>
        /// Re-arms the controller once the thief is back on the map.
        ///
        /// The progress controller has to be reset with it. Leaving it marked
        /// completed means the next arrest can never start, which looks like a
        /// broken sensor rather than a missed reset.
        /// </summary>
        public void ClearForNextArrest()
        {
            IsCompleted = false;
            if (progressController != null)
            {
                progressController.ResetProgress();
            }
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
