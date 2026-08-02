using System;
using PawsAndLoot.Config;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Arrest
{
    /// <summary>
    /// Turns a completed arrest into a spell in the cells and a return to the
    /// map.
    ///
    /// Separate from both halves on purpose. The arrest controller decides
    /// whether a catch happened and knows nothing about where the station is;
    /// the jail moves a character and knows nothing about arrests. This is the
    /// only piece that needs both, and it is the only piece that needs the map.
    /// </summary>
    public sealed class ArrestJailCoordinator : MonoBehaviour
    {
        [SerializeField]
        private ArrestCompletionController arrestCompletion;

        [SerializeField]
        private ThiefJailState jail;

        [SerializeField]
        private Transform cellPoint;

        [SerializeField]
        private Transform releasePoint;

        [SerializeField]
        private ArrestConfig arrestConfig;

        /// <summary>
        /// Where a released thief may come back, when there is more than one
        /// place. Optional: without it the single release point is used, which
        /// is what every test fixture builds.
        /// </summary>
        [SerializeField]
        private PawsAndLoot.Gameplay.Players.ThiefSpawnPoints releasePoints;

        private bool _subscribed;

        public void Configure(
            ArrestCompletionController configuredArrestCompletion,
            ThiefJailState configuredJail,
            Transform configuredCellPoint,
            Transform configuredReleasePoint,
            ArrestConfig configuredArrestConfig,
            PawsAndLoot.Gameplay.Players.ThiefSpawnPoints configuredReleasePoints
                = null)
        {
            releasePoints = configuredReleasePoints;
            Unsubscribe();
            arrestCompletion = configuredArrestCompletion;
            jail = configuredJail;
            cellPoint = configuredCellPoint;
            releasePoint = configuredReleasePoint;
            arrestConfig = configuredArrestConfig;
            ValidateOrThrow();
            Subscribe();
        }

        public void ValidateOrThrow()
        {
            if (arrestCompletion == null
                || jail == null
                || cellPoint == null
                || releasePoint == null
                || arrestConfig == null)
            {
                throw new InvalidOperationException(
                    $"ArrestJailCoordinator '{name}' has missing references.");
            }
        }

        private void HandleArrestCompleted()
        {
            if (jail == null || cellPoint == null || releasePoint == null)
            {
                return;
            }

            // Drawn here rather than when the jail term ends, so the whole
            // sentence has one answer. Deciding at release would let the same
            // arrest resolve differently if this ran twice.
            //
            // And drawn on this machine only. This handler fires from the
            // arrest being completed, which is a host decision; the client's
            // thief arrives at the drawn corner by the position replication
            // that was already carrying it.
            Vector3 release = releasePoints != null
                ? releasePoints.Draw(releasePoint.position)
                : releasePoint.position;

            jail.TryJail(
                arrestConfig.JailSeconds,
                cellPoint.position,
                release);
        }

        /// <summary>
        /// Re-arms the arrest only when the thief is actually back out.
        ///
        /// Doing it on a timer of its own would eventually drift away from the
        /// release and leave a window where the thief is still in the cell and
        /// can be arrested again, which reads as the officer scoring twice for
        /// standing still.
        /// </summary>
        private void HandleReleased()
        {
            arrestCompletion?.ClearForNextArrest();
            GameLogger.Info(
                GameLogCategory.Arrest,
                "Arrest re-armed after release.",
                this);
        }

        private void Subscribe()
        {
            if (_subscribed || arrestCompletion == null || jail == null)
            {
                return;
            }

            arrestCompletion.ArrestCompleted += HandleArrestCompleted;
            jail.Released += HandleReleased;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (arrestCompletion != null)
            {
                arrestCompletion.ArrestCompleted -= HandleArrestCompleted;
            }

            if (jail != null)
            {
                jail.Released -= HandleReleased;
            }

            _subscribed = false;
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
