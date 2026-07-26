using PawsAndLoot.Gameplay.Players;

namespace PawsAndLoot.Companions
{
    public static class CompanionCommandValidator
    {
        public static CompanionCommandFailure Validate(
            CompanionCommandId commandId,
            PlayerRole issuerRole,
            bool matchActive,
            bool companionAvailable,
            bool commandAvailable,
            bool targetAvailable,
            bool targetReachable)
        {
            if (!matchActive)
            {
                return CompanionCommandFailure.MatchInactive;
            }

            if (!IsImplemented(commandId))
            {
                return CompanionCommandFailure.UnsupportedCommand;
            }

            if (!BelongsToRole(commandId, issuerRole))
            {
                return CompanionCommandFailure.WrongRole;
            }

            if (!companionAvailable)
            {
                return CompanionCommandFailure.CompanionUnavailable;
            }

            if (!commandAvailable)
            {
                return CompanionCommandFailure.Cooldown;
            }

            if (!targetAvailable)
            {
                return CompanionCommandFailure.TargetUnavailable;
            }

            return targetReachable
                ? CompanionCommandFailure.None
                : CompanionCommandFailure.TargetUnreachable;
        }

        public static bool IsImplemented(CompanionCommandId commandId)
        {
            return commandId == CompanionCommandId.Track
                || commandId == CompanionCommandId.Distract;
        }

        public static bool BelongsToRole(
            CompanionCommandId commandId,
            PlayerRole role)
        {
            switch (commandId)
            {
                case CompanionCommandId.Track:
                case CompanionCommandId.Search:
                case CompanionCommandId.Guard:
                case CompanionCommandId.Bark:
                    return role == PlayerRole.Police;
                case CompanionCommandId.Scout:
                case CompanionCommandId.Distract:
                case CompanionCommandId.Steal:
                case CompanionCommandId.Hide:
                    return role == PlayerRole.Thief;
                default:
                    return false;
            }
        }

        public static string GetFailureMessage(
            CompanionCommandFailure failure)
        {
            return failure switch
            {
                CompanionCommandFailure.MatchInactive
                    => "경기 중에만 명령할 수 있습니다.",
                CompanionCommandFailure.UnsupportedCommand
                    => "아직 사용할 수 없는 명령입니다.",
                CompanionCommandFailure.WrongRole
                    => "현재 역할의 동물에게 내릴 수 없는 명령입니다.",
                CompanionCommandFailure.CompanionUnavailable
                    => "동물 파트너가 명령을 받을 수 없습니다.",
                CompanionCommandFailure.Cooldown
                    => "동물 파트너가 다른 행동 중이거나 쿨타임입니다.",
                CompanionCommandFailure.TargetUnavailable
                    => "명령할 대상이 없습니다.",
                CompanionCommandFailure.TargetUnreachable
                    => "대상 위치로 갈 수 없습니다.",
                CompanionCommandFailure.StaleVoiceResult
                    => "늦게 도착한 음성 결과를 무시했습니다.",
                _ => "명령을 실행하지 못했습니다."
            };
        }
    }
}
