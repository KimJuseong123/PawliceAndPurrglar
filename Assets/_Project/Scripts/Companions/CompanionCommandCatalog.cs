using PawsAndLoot.Gameplay.Players;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Which commands belong to which side and which of them need a target.
    /// Kept as one table so the validator, the UI and any future voice
    /// classifier all read the same source instead of duplicating rules.
    /// </summary>
    public static class CompanionCommandCatalog
    {
        public static CompanionKind GetCompanionKind(PlayerRole role)
        {
            return role == PlayerRole.Police
                ? CompanionKind.Dog
                : CompanionKind.Cat;
        }

        public static bool BelongsTo(
            CompanionCommandId commandId,
            PlayerRole role)
        {
            return commandId switch
            {
                CompanionCommandId.Track
                    or CompanionCommandId.Search
                    or CompanionCommandId.Guard
                    or CompanionCommandId.Bark =>
                    role == PlayerRole.Police,
                CompanionCommandId.Scout
                    or CompanionCommandId.Distract
                    or CompanionCommandId.Steal
                    or CompanionCommandId.Hide =>
                    role == PlayerRole.Thief,
                CompanionCommandId.Stop
                    or CompanionCommandId.FollowOwner
                    or CompanionCommandId.Stay
                    or CompanionCommandId.ReturnOwner
                    or CompanionCommandId.Cancel => true,
                _ => false
            };
        }

        /// <summary>
        /// Commands that are meaningless without a destination. A command that
        /// does not require one must not be refused for lacking it.
        /// </summary>
        public static bool RequiresTarget(CompanionCommandId commandId)
        {
            return commandId switch
            {
                CompanionCommandId.Track => true,
                CompanionCommandId.Search => true,
                CompanionCommandId.Guard => true,
                CompanionCommandId.Scout => true,
                CompanionCommandId.Distract => true,
                CompanionCommandId.Steal => false,
                CompanionCommandId.Hide => true,
                // Barking happens where the dog already stands.
                CompanionCommandId.Bark => false,
                _ => false
            };
        }

        public static CompanionCommandId FromIntent(
            string intent,
            CompanionKind kind)
        {
            return intent switch
            {
                "STOP" => CompanionCommandId.Stop,
                "FOLLOW_OWNER" => CompanionCommandId.FollowOwner,
                "STAY" => CompanionCommandId.Stay,
                "RETURN_OWNER" => CompanionCommandId.ReturnOwner,
                "CANCEL" => CompanionCommandId.Cancel,
                "SEARCH_AREA" => CompanionCommandId.Search,
                "FETCH_OBJECT" => kind == CompanionKind.Cat
                    ? CompanionCommandId.Steal
                    : CompanionCommandId.Search,
                "ROOF" or "CLIMB_ROOF" => kind == CompanionKind.Cat
                    ? CompanionCommandId.Steal
                    : CompanionCommandId.None,
                "CHASE_TARGET" => kind == CompanionKind.Dog
                    ? CompanionCommandId.Track
                    : CompanionCommandId.Distract,
                "GUARD_AREA" => kind == CompanionKind.Dog
                    ? CompanionCommandId.Guard
                    : CompanionCommandId.Hide,
                "INSPECT_TARGET" => kind == CompanionKind.Cat
                    ? CompanionCommandId.Scout
                    : CompanionCommandId.Search,
                "DISTRACT_TARGET" => CompanionCommandId.Distract,

                // BARK and HIDE had no intent name at all until 2026-08-07, so
                // `Ctrl+4` reached them and voice could not — a perfect transcript
                // of "짖으라고" resolved to `None` and the dog did nothing. The
                // silence is what made it look like a microphone problem.
                // `VoiceCommandReachabilityTests` now fails if a number-key
                // command loses its voice route again.
                "BARK" => kind == CompanionKind.Dog
                    ? CompanionCommandId.Bark
                    : CompanionCommandId.Distract,
                "HIDE" => kind == CompanionKind.Cat
                    ? CompanionCommandId.Hide
                    : CompanionCommandId.Guard,
                "MOVE_TO_POSITION" => kind == CompanionKind.Dog
                    ? CompanionCommandId.Guard
                    : CompanionCommandId.Scout,
                _ => CompanionCommandId.None
            };
        }

        /// <summary>
        /// Maps the prototype number keys to commands. Voice will map onto the
        /// same ids later, which is the whole point of DEC-002.
        /// </summary>
        public static CompanionCommandId FromNumberKey(
            PlayerRole role,
            int numberKey)
        {
            if (role == PlayerRole.Police)
            {
                return numberKey switch
                {
                    1 => CompanionCommandId.Track,
                    2 => CompanionCommandId.Search,
                    3 => CompanionCommandId.Guard,
                    4 => CompanionCommandId.Bark,
                    _ => CompanionCommandId.None
                };
            }

            return numberKey switch
            {
                1 => CompanionCommandId.Scout,
                2 => CompanionCommandId.Distract,
                3 => CompanionCommandId.Steal,
                4 => CompanionCommandId.Hide,
                _ => CompanionCommandId.None
            };
        }

        public static CompanionCommandId FromDebugNumberKey(
            PlayerRole role,
            int numberKey) => FromNumberKey(role, numberKey);

        /// <summary>
        /// The commands one role can give, in the order they belong on screen:
        /// the four that are this animal's own, then the five either animal
        /// obeys.
        ///
        /// Ordered rather than a set, because the table this fills is read while
        /// somebody is being chased and a list that reshuffles is a list nobody
        /// learns.
        /// </summary>
        public static CompanionCommandId[] GetCommandsFor(PlayerRole role)
        {
            return role == PlayerRole.Police
                ? new[]
                {
                    CompanionCommandId.Track,
                    CompanionCommandId.Search,
                    CompanionCommandId.Guard,
                    CompanionCommandId.Bark,
                    CompanionCommandId.Stop,
                    CompanionCommandId.FollowOwner,
                    CompanionCommandId.Stay,
                    CompanionCommandId.ReturnOwner,
                    CompanionCommandId.Cancel
                }
                : new[]
                {
                    CompanionCommandId.Scout,
                    CompanionCommandId.Distract,
                    CompanionCommandId.Steal,
                    CompanionCommandId.Hide,
                    CompanionCommandId.Stop,
                    CompanionCommandId.FollowOwner,
                    CompanionCommandId.Stay,
                    CompanionCommandId.ReturnOwner,
                    CompanionCommandId.Cancel
                };
        }

        /// <summary>
        /// What the command is called in Korean, for the player rather than for
        /// a log.
        /// </summary>
        public static string GetKoreanName(CompanionCommandId commandId)
        {
            return commandId switch
            {
                CompanionCommandId.Track => "추적",
                CompanionCommandId.Search => "수색",
                CompanionCommandId.Guard => "경계",
                CompanionCommandId.Bark => "짖기",
                CompanionCommandId.Scout => "정찰",
                CompanionCommandId.Distract => "유인",
                CompanionCommandId.Steal => "지붕/훔치기",
                CompanionCommandId.Hide => "숨기",
                CompanionCommandId.Stop => "멈추기",
                CompanionCommandId.FollowOwner => "따라오기",
                CompanionCommandId.Stay => "기다리기",
                CompanionCommandId.ReturnOwner => "돌아오기",
                CompanionCommandId.Cancel => "취소",
                _ => "없음"
            };
        }

        /// <summary>
        /// What to actually say.
        ///
        /// These are the stems the server's `exact-command-matcher` compares
        /// against, written out as something a person would say. They are not
        /// the only sentences that work — the matcher compares stems at the jamo
        /// level and a model handles the rest — but they are the ones guaranteed
        /// to resolve with the model unreachable, which is what a player needs
        /// printed on their screen.
        ///
        /// Kept next to the ids rather than in the view, so a command that gains
        /// or loses a phrase changes in one place. The five shared ones are the
        /// absolute commands and are matched without a model at all.
        /// </summary>
        public static string GetSpokenExamples(CompanionCommandId commandId)
        {
            return commandId switch
            {
                CompanionCommandId.Track => "\"냄새 맡아\" · \"쫓아가\" · \"추적\"",
                CompanionCommandId.Search => "\"찾아봐\" · \"수색해\" · \"뒤져봐\"",
                CompanionCommandId.Guard => "\"지켜\" · \"경계해\" · \"감시해\"",
                CompanionCommandId.Bark => "\"짖어\" · \"소리질러\"",
                CompanionCommandId.Scout => "\"정찰해\" · \"확인해\" · \"살펴봐\"",
                CompanionCommandId.Distract => "\"유인해\" · \"할퀴어\" · \"야옹\"",
                CompanionCommandId.Steal => "\"훔쳐와\" · \"가져와\" · \"지붕으로\"",
                CompanionCommandId.Hide => "\"숨어\" · \"은신해\"",
                CompanionCommandId.Stop => "\"멈춰\" · \"그만\"",
                CompanionCommandId.FollowOwner => "\"따라와\" · \"이리와\"",
                CompanionCommandId.Stay => "\"기다려\" · \"가만히 있어\"",
                CompanionCommandId.ReturnOwner => "\"돌아와\"",
                CompanionCommandId.Cancel => "\"취소\" · \"하지마\"",
                _ => string.Empty
            };
        }

        public static string GetDisplayName(CompanionCommandId commandId)
        {
            return commandId switch
            {
                CompanionCommandId.Track => "TRACK",
                CompanionCommandId.Search => "SEARCH",
                CompanionCommandId.Guard => "GUARD",
                CompanionCommandId.Bark => "BARK",
                CompanionCommandId.Scout => "SCOUT",
                CompanionCommandId.Distract => "DISTRACT",
                CompanionCommandId.Steal => "ROOF",
                CompanionCommandId.Hide => "HIDE",
                CompanionCommandId.Stop => "STOP",
                CompanionCommandId.FollowOwner => "FOLLOW OWNER",
                CompanionCommandId.Stay => "STAY",
                CompanionCommandId.ReturnOwner => "RETURN OWNER",
                CompanionCommandId.Cancel => "CANCEL",
                _ => "NONE"
            };
        }
    }
}
