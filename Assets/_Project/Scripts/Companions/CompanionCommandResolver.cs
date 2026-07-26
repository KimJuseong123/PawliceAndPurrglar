using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Turns an accepted command into a destination and a reportable outcome.
    ///
    /// DOG-003 reads the scent trail rather than the live thief, so the police
    /// never get perfect information. CAT-004 raises an information-only signal
    /// and is refused while one is already running. Both effects live here
    /// instead of inside the agent so they can be tested without a scene and so
    /// the agent keeps owning only movement and lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionCommandResolver : MonoBehaviour
    {
        [SerializeField]
        private ThiefScentTrail scentTrail;

        [SerializeField]
        private DistractionBoard distractionBoard;

        [SerializeField]
        private Transform policeTransform;

        [SerializeField, Min(1f)]
        private float distractionMaximumRange = 26f;

        public readonly struct Resolution
        {
            public Resolution(
                bool accepted,
                CompanionCommandOutcome outcome,
                Vector3? destination)
            {
                Accepted = accepted;
                Outcome = outcome;
                Destination = destination;
            }

            public bool Accepted { get; }
            public CompanionCommandOutcome Outcome { get; }
            public Vector3? Destination { get; }
        }

        public void Configure(
            ThiefScentTrail configuredTrail,
            DistractionBoard configuredBoard,
            Transform configuredPolice,
            float configuredDistractionRange)
        {
            scentTrail = configuredTrail;
            distractionBoard = configuredBoard;
            policeTransform = configuredPolice;
            distractionMaximumRange =
                Mathf.Max(1f, configuredDistractionRange);
        }

        /// <summary>
        /// Resolves a command. A refusal here is a gameplay result, not a
        /// validation error, so it still consumes the companion's turn and gets
        /// reported to the player.
        /// </summary>
        public Resolution Resolve(
            in CompanionCommandRequest request,
            Vector3 companionPosition,
            float nowSeconds)
        {
            switch (request.CommandId)
            {
                case CompanionCommandId.Track:
                    return ResolveTrack(nowSeconds);
                case CompanionCommandId.Distract:
                    return ResolveDistract(request, nowSeconds);
                default:
                    request.TryGetDestination(out Vector3 fallback);
                    return new Resolution(
                        true,
                        CompanionCommandOutcome.Completed,
                        request.HasTarget ? fallback : null);
            }
        }

        private Resolution ResolveTrack(float nowSeconds)
        {
            if (scentTrail == null
                || !scentTrail.TryGetFreshestPoint(
                    nowSeconds,
                    out Vector3 point))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.TrailMissing,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.TrailFound,
                point);
        }

        private Resolution ResolveDistract(
            in CompanionCommandRequest request,
            float nowSeconds)
        {
            if (distractionBoard == null)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionTargetTooFar,
                    null);
            }

            if (distractionBoard.IsActive)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionAlreadyActive,
                    null);
            }

            if (!request.TryGetDestination(out Vector3 destination))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionTargetTooFar,
                    null);
            }

            // Distracting someone who cannot possibly notice is a wasted order,
            // so distance to the police gates it.
            if (policeTransform != null)
            {
                Vector3 police = policeTransform.position;
                police.y = destination.y;
                if (Vector3.Distance(police, destination)
                    > distractionMaximumRange)
                {
                    return new Resolution(
                        false,
                        CompanionCommandOutcome.DistractionTargetTooFar,
                        null);
                }
            }

            if (!distractionBoard.TryStart(destination, nowSeconds))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionAlreadyActive,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.DistractionStarted,
                destination);
        }
    }
}
