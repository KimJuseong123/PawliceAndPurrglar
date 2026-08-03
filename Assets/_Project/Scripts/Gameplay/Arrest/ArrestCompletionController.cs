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

            // Latched on every catch, not only the last one.
            //
            // Left unlatched for the first two, Update calls this again the
            // very next frame while the officer is still standing on the thief,
            // and the tally runs away from what actually happened on screen.
            IsCompleted = true;
            CurrentCatchCount++;
            GameLogger.Info(
                GameLogCategory.Arrest,
                $"Arrest completed ({CurrentCatchCount}/{RequiredCatchCount}).",
                this);

            ArrestCompleted?.Invoke();
            CatchCountChanged?.Invoke(CurrentCatchCount, RequiredCatchCount);

            // Raised every time, and the arbiter decides.
            //
            // This used to fire only on the third catch, which sounds right and
            // is not: the arbiter counts the requests it receives and needs
            // three of them. Forwarding only the third meant the officer had to
            // catch the thief three times to send one request, and three
            // requests to win — nine catches, except the unlatched middle
            // catches made the tally unstable long before that. A two-process
            // run jailed the thief four times, counted two arrests and declared
            // nobody the winner.
            //
            // Two places counting the same thing is the shape of the bug that
            // already split the host and the client over who had won
            // (`ISSUE-046`). The count here is for the screen; the count that
            // ends the match lives in one place.
            PoliceVictoryRequested?.Invoke();
            return true;
        }

        /// <summary>
        /// Takes the host's tally on a machine that is not counting.
        ///
        /// A client never runs this controller — the host owns the arrest — so
        /// its own count stays at zero for the whole match and the screen said
        /// so. Nothing about the rules changes here; the number is being told
        /// to a machine that could not work it out.
        /// </summary>
        public void ApplyReplicatedCatchCount(int catchCount)
        {
            int clamped = Mathf.Max(0, catchCount);
            if (clamped == CurrentCatchCount)
            {
                return;
            }

            CurrentCatchCount = clamped;
            CatchCountChanged?.Invoke(CurrentCatchCount, RequiredCatchCount);
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
