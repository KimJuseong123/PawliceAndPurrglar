using System;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    public enum CompanionCommandId
    {
        None = 0,
        Track = 1,
        Search = 2,
        Guard = 3,
        Bark = 4,
        Scout = 5,
        Distract = 6,
        Steal = 7,
        Hide = 8
    }

    public enum CompanionCommandSource
    {
        Keyboard = 0,
        Voice = 1
    }

    public enum CompanionCommandFailure
    {
        None = 0,
        MatchInactive,
        UnsupportedCommand,
        WrongRole,
        CompanionUnavailable,
        Cooldown,
        TargetUnavailable,
        TargetUnreachable,
        ExecutionFailed,
        StaleVoiceResult
    }

    [Serializable]
    public readonly struct CompanionCommandRequest
    {
        public CompanionCommandRequest(
            string requestId,
            CompanionCommandId commandId,
            PlayerRole issuerRole,
            string companionId,
            string targetEntityId,
            Vector3 targetPosition,
            double issuedAt,
            CompanionCommandSource source)
        {
            RequestId = requestId;
            CommandId = commandId;
            IssuerRole = issuerRole;
            CompanionId = companionId;
            TargetEntityId = targetEntityId;
            TargetPosition = targetPosition;
            IssuedAt = issuedAt;
            Source = source;
        }

        public string RequestId { get; }
        public CompanionCommandId CommandId { get; }
        public PlayerRole IssuerRole { get; }
        public string CompanionId { get; }
        public string TargetEntityId { get; }
        public Vector3 TargetPosition { get; }
        public double IssuedAt { get; }
        public CompanionCommandSource Source { get; }
    }

    public readonly struct CompanionCommandResult
    {
        private CompanionCommandResult(
            CompanionCommandRequest request,
            bool accepted,
            CompanionCommandFailure failure,
            string message)
        {
            Request = request;
            Accepted = accepted;
            Failure = failure;
            Message = message;
        }

        public CompanionCommandRequest Request { get; }
        public bool Accepted { get; }
        public CompanionCommandFailure Failure { get; }
        public string Message { get; }

        public static CompanionCommandResult Accept(
            CompanionCommandRequest request)
        {
            return new CompanionCommandResult(
                request,
                true,
                CompanionCommandFailure.None,
                "Command accepted.");
        }

        public static CompanionCommandResult Reject(
            CompanionCommandRequest request,
            CompanionCommandFailure failure,
            string message)
        {
            return new CompanionCommandResult(
                request,
                false,
                failure,
                message);
        }
    }
}
