using PawliceAndPurrglar.Gameplay.Players;

namespace PawliceAndPurrglar.Companions
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
                    or CompanionCommandId.Hide
                    or CompanionCommandId.Bite =>
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
        ///
        /// The answer has to match what <see cref="CompanionCommandResolver"/>
        /// actually reads. Three of these said yes while their resolver never
        /// looked at the request's target at all — TRACK reads the scent trail,
        /// SCOUT sweeps for the nearest loot, HIDE sweeps for the nearest
        /// stash. Every one of them was refused here for lacking a target the
        /// resolver was never going to use.
        ///
        /// That made them unreachable by voice specifically, because no voice
        /// path can supply a target: the deterministic matcher hardcodes
        /// `targetId: null` (`resolveWithMatcher`) and the model may only name
        /// something already in `visibleTargets`. "냄새 맡아" — the one command
        /// whose whole purpose is finding a thief you cannot see — therefore
        /// needed the thief visible to be allowed to run.
        /// </summary>
        public static bool RequiresTarget(CompanionCommandId commandId)
        {
            return commandId switch
            {
                // Tracks the scent trail from wherever the dog stands. A cold
                // trail is a gameplay outcome (`TrailMissing`), not a refusal.
                CompanionCommandId.Track => false,
                CompanionCommandId.Search => true,
                CompanionCommandId.Guard => true,
                // Sweeps its own surroundings and reports what it noticed.
                CompanionCommandId.Scout => false,
                CompanionCommandId.Distract => true,
                CompanionCommandId.Steal => false,
                // Walks to the nearest free hiding spot.
                CompanionCommandId.Hide => false,
                // Runs at the officer, who the resolver already holds a
                // reference to. Asking the player to name them would put this
                // command back out of reach of voice for no gain — there is only
                // ever one officer.
                CompanionCommandId.Bite => false,
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

                // CAT-010. Never substituted for the dog: a police dog biting
                // overlaps the arrest and was refused on purpose. An order the
                // dog cannot carry out resolves to nothing, which the feed
                // reports, rather than quietly becoming a different command.
                "BITE" => kind == CompanionKind.Cat
                    ? CompanionCommandId.Bite
                    : CompanionCommandId.None,
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
        /// Whether either animal obeys this command.
        ///
        /// The five absolute commands sit at 20 and above, which the validator
        /// already relies on. Exposed because the on-screen table draws a line
        /// between an animal's own orders and the shared ones, and it had that
        /// line hardcoded at four rows — the count was right for both animals
        /// until CAT-010 gave the cat a fifth of its own, at which point the
        /// cat's table would have filed "물기" under "either animal obeys these"
        /// while the dog cannot obey it at all.
        /// </summary>
        public static bool IsSharedByBothAnimals(CompanionCommandId commandId)
        {
            return (int)commandId >= (int)CompanionCommandId.Stop;
        }

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
                    CompanionCommandId.Bite,
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
                CompanionCommandId.Bite => "물기",
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
                CompanionCommandId.Bite => "\"물어\" · \"깨물어\" · \"공격해\"",
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
                CompanionCommandId.Bite => "BITE",
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
