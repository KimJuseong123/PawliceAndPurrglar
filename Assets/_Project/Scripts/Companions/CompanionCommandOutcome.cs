namespace PawliceAndPurrglar.Companions
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
        Abandoned = 7,

        /// <summary>DOG-004. Dog is sweeping the requested area.</summary>
        SearchStarted = 8,

        /// <summary>DOG-005. Dog is holding the requested spot.</summary>
        GuardStarted = 9,

        /// <summary>DOG-006. Dog barked and the thief was close enough.</summary>
        BarkRevealedThief = 10,

        /// <summary>DOG-006. Dog barked at nothing.</summary>
        BarkFoundNobody = 11,

        /// <summary>CAT-003. Cat reported nearby loot or police.</summary>
        ScoutReported = 12,

        /// <summary>CAT-003. Cat found nothing worth reporting.</summary>
        ScoutFoundNothing = 13,

        /// <summary>CAT-005. Cat is fetching nearby loot.</summary>
        StealStarted = 14,

        /// <summary>CAT-005. No reachable loot to fetch.</summary>
        StealNoLoot = 15,

        /// <summary>CAT-005. The thief's hands are already full.</summary>
        StealOwnerBusy = 16,

        /// <summary>CAT-006. Loot was hidden at a stash.</summary>
        HideStored = 17,

        /// <summary>CAT-006. No stash within reach, or nothing to hide.</summary>
        HideUnavailable = 18,

        /// <summary>CAT-005. Cat has the loot and is bringing it back.</summary>
        StealCarrying = 19,

        /// <summary>CAT-005. Cat handed the loot to its owner.</summary>
        StealDelivered = 20,

        /// <summary>Cat found a nearby rooftop and is climbing to it.</summary>
        RoofClimbStarted = 21,

        /// <summary>Cat reached the rooftop landing.</summary>
        RoofClimbReached = 22,

        /// <summary>No usable rooftop was close enough for the cat.</summary>
        RoofClimbUnavailable = 23,

        /// <summary>CAT-010. Cat is running at the officer to bite them.</summary>
        BiteStarted = 24,

        /// <summary>CAT-010. The bite landed and the officer is held.</summary>
        BiteLanded = 25,

        /// <summary>CAT-010. No officer close enough to run at.</summary>
        BiteNoTarget = 26,

        /// <summary>
        /// CAT-010. The officer was reached but could not be held — they are
        /// already stunned, or still inside the re-stun guard.
        ///
        /// Separate from <see cref="BiteNoTarget"/> because the two ask for
        /// opposite things from the player: one means "get closer", the other
        /// means "wait". A single failure would teach neither.
        /// </summary>
        BiteImmune = 27
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
                CompanionCommandOutcome.SearchStarted =>
                    "주변을 수색합니다.",
                CompanionCommandOutcome.GuardStarted =>
                    "이 자리를 지킵니다.",
                CompanionCommandOutcome.BarkRevealedThief =>
                    "짖었습니다! 도둑이 근처에 있습니다.",
                CompanionCommandOutcome.BarkFoundNobody =>
                    "짖었지만 아무도 없습니다.",
                CompanionCommandOutcome.ScoutReported =>
                    "주변 정보를 알려왔습니다.",
                CompanionCommandOutcome.ScoutFoundNothing =>
                    "알릴 만한 것이 없습니다.",
                CompanionCommandOutcome.StealStarted =>
                    "보물을 가져오러 갑니다.",
                CompanionCommandOutcome.StealNoLoot =>
                    "가져올 보물이 없습니다.",
                CompanionCommandOutcome.StealOwnerBusy =>
                    "이미 보물을 들고 있습니다.",
                CompanionCommandOutcome.HideStored =>
                    "보물을 숨겼습니다.",
                CompanionCommandOutcome.HideUnavailable =>
                    "숨길 곳이 없습니다.",
                CompanionCommandOutcome.StealCarrying =>
                    "보물을 물고 돌아옵니다.",
                CompanionCommandOutcome.StealDelivered =>
                    "보물을 건네주었습니다.",
                CompanionCommandOutcome.RoofClimbStarted =>
                    "가까운 지붕으로 올라갑니다.",
                CompanionCommandOutcome.RoofClimbReached =>
                    "지붕 위에 도착했습니다.",
                CompanionCommandOutcome.RoofClimbUnavailable =>
                    "근처에 올라갈 지붕이 없습니다.",
                CompanionCommandOutcome.BiteStarted =>
                    "경찰에게 달려갑니다!",
                CompanionCommandOutcome.BiteLanded =>
                    "경찰을 물었습니다!",
                CompanionCommandOutcome.BiteNoTarget =>
                    "물 수 있는 거리에 경찰이 없습니다.",
                CompanionCommandOutcome.BiteImmune =>
                    "경찰이 아직 정신을 못 차렸습니다.",
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
