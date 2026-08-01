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

            IsCompleted = true;
            GameLogger.Info(
                GameLogCategory.Arrest,
                "Arrest completed.",
                this);
            ArrestCompleted?.Invoke();
            PoliceVictoryRequested?.Invoke();
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
