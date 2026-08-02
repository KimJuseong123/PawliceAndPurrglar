using System;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    public readonly struct VoiceValidationResult
    {
        public VoiceValidationResult(
            bool accepted,
            CompanionCommandRejection rejection,
            string error)
        {
            Accepted = accepted;
            Rejection = rejection;
            Error = error ?? string.Empty;
        }

        public bool Accepted { get; }
        public CompanionCommandRejection Rejection { get; }
        public string Error { get; }
    }

    public static class VoiceCommandValidator
    {
        private const float MaximumCoordinate = 10000f;

        public static VoiceValidationResult Validate(
            VoiceCommandResult result,
            LocalAiVoiceResponse response,
            PlayerRole role,
            CompanionKind kind,
            CompanionCommandDispatcher dispatcher,
            Transform issuer,
            Transform targetEntity,
            Vector3? targetPosition)
        {
            if (result == null || response == null)
            {
                return Reject(
                    CompanionCommandRejection.UnknownCommand,
                    "VOICE_RESULT_MISSING");
            }

            if (!VoiceCommandMapper.IsFinite(result.confidence)
                || result.confidence < 0f
                || result.confidence > 1f)
            {
                return Reject(
                    CompanionCommandRejection.UnknownCommand,
                    "VOICE_CONFIDENCE_INVALID");
            }

            if (!VoiceCommandMapper.TryMap(
                    result.interpretedCommand,
                    kind,
                    out CompanionCommandId commandId))
            {
                return Reject(
                    CompanionCommandRejection.UnknownCommand,
                    "VOICE_INTENT_UNSUPPORTED");
            }

            if (!Enum.IsDefined(typeof(CompanionCommandId), commandId)
                || commandId == CompanionCommandId.None
                || !CompanionCommandCatalog.BelongsTo(commandId, role))
            {
                return Reject(
                    CompanionCommandRejection.WrongFaction,
                    "VOICE_COMMAND_NOT_ALLOWED");
            }

            if (!IsAllowedTargetType(result.targetType))
            {
                return Reject(
                    CompanionCommandRejection.TargetMissing,
                    "VOICE_TARGET_TYPE_INVALID");
            }

            if (!IsSafeTransform(targetEntity)
                || (targetPosition.HasValue
                    && !IsSafePosition(targetPosition.Value)))
            {
                return Reject(
                    CompanionCommandRejection.TargetUnreachable,
                    "VOICE_TARGET_POSITION_INVALID");
            }

            if (issuer != null && targetEntity != null
                && PlanarDistance(issuer.position, targetEntity.position) > 18f)
            {
                return Reject(
                    CompanionCommandRejection.TargetUnreachable,
                    "VOICE_TARGET_TOO_FAR");
            }

            if (issuer != null && targetPosition.HasValue
                && PlanarDistance(issuer.position, targetPosition.Value) > 18f)
            {
                return Reject(
                    CompanionCommandRejection.TargetUnreachable,
                    "VOICE_POSITION_TOO_FAR");
            }

            if (dispatcher == null)
            {
                return Reject(
                    CompanionCommandRejection.CompanionMissing,
                    "VOICE_DISPATCHER_MISSING");
            }

            CompanionAgent agent = dispatcher.FindAgent(kind);
            if (agent == null)
            {
                return Reject(
                    CompanionCommandRejection.CompanionMissing,
                    "VOICE_COMPANION_MISSING");
            }

            var request = new CompanionCommandRequest(
                commandId,
                role,
                kind,
                CompanionCommandInputSource.Voice,
                Time.time,
                targetEntity,
                targetPosition,
                result.requestId,
                result.targetId);
            CompanionCommandValidator.Context context = agent.BuildValidationContext();
            if (!CompanionCommandValidator.TryValidate(
                    request,
                    context,
                    out CompanionCommandRejection rejection))
            {
                return Reject(rejection, "VOICE_COMMAND_REJECTED");
            }

            return new VoiceValidationResult(
                true,
                CompanionCommandRejection.None,
                string.Empty);
        }

        private static VoiceValidationResult Reject(
            CompanionCommandRejection rejection,
            string error) => new(false, rejection, error);

        private static bool IsAllowedTargetType(string value)
        {
            string normalized = (value ?? "NONE").Trim().ToUpperInvariant();
            return normalized is "NONE"
                or "SELF_POSITION"
                or "LOOK_POSITION"
                or "VISIBLE_TARGET"
                or "NAMED_TARGET"
                or "LAST_KNOWN_TARGET";
        }

        private static bool IsSafeTransform(Transform value) => value == null
            || IsSafePosition(value.position);

        private static bool IsSafePosition(Vector3 value) =>
            VoiceCommandMapper.IsFinite(value.x)
            && VoiceCommandMapper.IsFinite(value.y)
            && VoiceCommandMapper.IsFinite(value.z)
            && Mathf.Abs(value.x) <= MaximumCoordinate
            && Mathf.Abs(value.y) <= MaximumCoordinate
            && Mathf.Abs(value.z) <= MaximumCoordinate;

        private static float PlanarDistance(Vector3 left, Vector3 right)
        {
            left.y = 0f;
            right.y = 0f;
            return Vector3.Distance(left, right);
        }
    }
}
