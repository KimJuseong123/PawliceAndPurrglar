namespace PawsAndLoot.Companions
{
    /// <summary>
    /// UI-006. What a command actually achieved, so the HUD can report a
    /// reason instead of leaving the player guessing.
    /// </summary>
    public enum CompanionCommandOutcome
    {
        None = 0,

        /// <summary>Dog found a usable trail and is following it.</summary>
        TrailFound = 1,

        /// <summary>Trail has gone cold, so nothing to track.</summary>
        TrailMissing = 2,

        /// <summary>Cat raised a distraction signal.</summary>
        DistractionStarted = 3,

        /// <summary>A distraction is already running.</summary>
        DistractionAlreadyActive = 4,

        /// <summary>Nothing close enough to be worth distracting.</summary>
        DistractionTargetTooFar = 5,

        /// <summary>Command finished normally with no special result.</summary>
        Completed = 6,

        /// <summary>Command was abandoned by the recovery path.</summary>
        Abandoned = 7
    }

    public static class CompanionCommandOutcomeText
    {
        /// <summary>
        /// Player facing sentence for a companion result. Kept beside the enum
        /// so UI never invents its own wording.
        /// </summary>
        public static string Describe(
            CompanionKind kind,
            CompanionCommandOutcome outcome)
        {
            string who = kind == CompanionKind.Dog ? "강아지" : "고양이";
            string what = outcome switch
            {
                CompanionCommandOutcome.TrailFound =>
                    "흔적을 찾았습니다.",
                CompanionCommandOutcome.TrailMissing =>
                    "추적할 흔적이 없습니다.",
                CompanionCommandOutcome.DistractionStarted =>
                    "교란을 시작합니다.",
                CompanionCommandOutcome.DistractionAlreadyActive =>
                    "이미 교란 중입니다.",
                CompanionCommandOutcome.DistractionTargetTooFar =>
                    "경찰이 너무 멀리 있습니다.",
                CompanionCommandOutcome.Completed =>
                    "명령을 마쳤습니다.",
                CompanionCommandOutcome.Abandoned =>
                    "명령을 포기하고 돌아왔습니다.",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(what)
                ? string.Empty
                : $"{who}: {what}";
        }

        public static string Describe(
            CompanionKind kind,
            CompanionCommandRejection rejection)
        {
            string who = kind == CompanionKind.Dog ? "강아지" : "고양이";
            string what = rejection switch
            {
                CompanionCommandRejection.MatchNotPlaying =>
                    "지금은 명령할 수 없습니다.",
                CompanionCommandRejection.WrongFaction =>
                    "이 명령은 상대 진영 것입니다.",
                CompanionCommandRejection.CompanionMissing =>
                    "동료가 없습니다.",
                CompanionCommandRejection.CompanionDisabled =>
                    "동료가 움직일 수 없습니다.",
                CompanionCommandRejection.OnCooldown =>
                    "아직 쉬고 있습니다.",
                CompanionCommandRejection.UnknownCommand =>
                    "모르는 명령입니다.",
                CompanionCommandRejection.TargetMissing =>
                    "대상이 없습니다.",
                CompanionCommandRejection.TargetUnreachable =>
                    "갈 수 없는 곳입니다.",
                _ => string.Empty
            };

            return string.IsNullOrEmpty(what)
                ? string.Empty
                : $"{who}: {what}";
        }
    }
}
