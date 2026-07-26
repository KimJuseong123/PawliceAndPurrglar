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
                CompanionCommandId.Steal => true,
                CompanionCommandId.Hide => true,
                // Barking happens where the dog already stands.
                CompanionCommandId.Bark => false,
                _ => false
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
                CompanionCommandId.Steal => "STEAL",
                CompanionCommandId.Hide => "HIDE",
                _ => "NONE"
            };
        }
    }
}
