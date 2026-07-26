using System;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// COMP-005. Decides whether a request may reach the animal AI.
    ///
    /// Pure so the rules are testable without a scene, and shared by every
    /// input source. A refusal must never consume the cooldown, otherwise a
    /// mistyped key would punish the player.
    /// </summary>
    public static class CompanionCommandValidator
    {
        public readonly struct Context
        {
            public Context(
                bool isMatchPlaying,
                bool companionExists,
                bool companionEnabled,
                float cooldownRemainingSeconds,
                bool targetReachable = true)
            {
                IsMatchPlaying = isMatchPlaying;
                CompanionExists = companionExists;
                CompanionEnabled = companionEnabled;
                CooldownRemainingSeconds = cooldownRemainingSeconds;
                TargetReachable = targetReachable;
            }

            public bool IsMatchPlaying { get; }
            public bool CompanionExists { get; }
            public bool CompanionEnabled { get; }
            public float CooldownRemainingSeconds { get; }
            public bool TargetReachable { get; }
        }

        /// <summary>
        /// Checks in a fixed order so the reported reason is deterministic.
        /// Returns true only when the command may be dispatched.
        /// </summary>
        public static bool TryValidate(
            in CompanionCommandRequest request,
            in Context context,
            out CompanionCommandRejection rejection)
        {
            if (request.CommandId == CompanionCommandId.None
                || !Enum.IsDefined(
                    typeof(CompanionCommandId),
                    request.CommandId))
            {
                rejection = CompanionCommandRejection.UnknownCommand;
                return false;
            }

            if (!context.IsMatchPlaying)
            {
                rejection = CompanionCommandRejection.MatchNotPlaying;
                return false;
            }

            if (!CompanionCommandCatalog.BelongsTo(
                    request.CommandId,
                    request.IssuerRole)
                || CompanionCommandCatalog.GetCompanionKind(
                       request.IssuerRole)
                   != request.CompanionKind)
            {
                rejection = CompanionCommandRejection.WrongFaction;
                return false;
            }

            if (!context.CompanionExists)
            {
                rejection = CompanionCommandRejection.CompanionMissing;
                return false;
            }

            if (!context.CompanionEnabled)
            {
                rejection = CompanionCommandRejection.CompanionDisabled;
                return false;
            }

            if (context.CooldownRemainingSeconds > 0f)
            {
                rejection = CompanionCommandRejection.OnCooldown;
                return false;
            }

            if (CompanionCommandCatalog.RequiresTarget(request.CommandId))
            {
                if (!request.TryGetDestination(out _))
                {
                    rejection = CompanionCommandRejection.TargetMissing;
                    return false;
                }

                if (!context.TargetReachable)
                {
                    rejection = CompanionCommandRejection.TargetUnreachable;
                    return false;
                }
            }

            rejection = CompanionCommandRejection.None;
            return true;
        }
    }
}
