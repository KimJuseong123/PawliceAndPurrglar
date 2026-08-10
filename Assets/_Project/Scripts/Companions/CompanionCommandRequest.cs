using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// COMP-003. One command request, whatever produced it.
    ///
    /// Number keys, on-screen buttons and future voice input all build this
    /// same struct, which is what keeps the input layer out of the animal AI.
    /// </summary>
    public readonly struct CompanionCommandRequest
    {
        public CompanionCommandRequest(
            CompanionCommandId commandId,
            PlayerRole issuerRole,
            CompanionKind companionKind,
            CompanionCommandInputSource inputSource,
            float issuedAtSeconds,
            Transform targetEntity = null,
            Vector3? targetPosition = null,
            string commandCorrelationId = null,
            string targetEntityId = null)
        {
            CommandId = commandId;
            IssuerRole = issuerRole;
            CompanionKind = companionKind;
            InputSource = inputSource;
            IssuedAtSeconds = issuedAtSeconds;
            TargetEntity = targetEntity;
            TargetPosition = targetPosition;
            CommandCorrelationId = commandCorrelationId;
            TargetEntityId = targetEntityId;
        }

        public CompanionCommandId CommandId { get; }

        /// <summary>Which side issued it. Stands in for IssuerPlayerId until
        /// networking assigns real ids.</summary>
        public PlayerRole IssuerRole { get; }

        /// <summary>Which animal it addresses. Stands in for CompanionId while
        /// each side owns exactly one companion.</summary>
        public CompanionKind CompanionKind { get; }

        public CompanionCommandInputSource InputSource { get; }
        public float IssuedAtSeconds { get; }

        /// <summary>Optional target object, null for commands that need none.</summary>
        public Transform TargetEntity { get; }

        /// <summary>Optional world position, null for commands that need none.</summary>
        public Vector3? TargetPosition { get; }
        public string CommandCorrelationId { get; }
        public string TargetEntityId { get; }

        public bool HasTarget =>
            TargetEntity != null || TargetPosition.HasValue;

        /// <summary>
        /// Resolved destination, preferring a live entity over a stale point so
        /// a moving target is followed rather than a remembered position.
        /// </summary>
        public bool TryGetDestination(out Vector3 destination)
        {
            if (TargetEntity != null)
            {
                destination = TargetEntity.position;
                return true;
            }

            if (TargetPosition.HasValue)
            {
                destination = TargetPosition.Value;
                return true;
            }

            destination = default;
            return false;
        }
    }
}
