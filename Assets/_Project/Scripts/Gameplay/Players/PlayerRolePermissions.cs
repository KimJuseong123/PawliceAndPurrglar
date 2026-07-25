using System;

namespace PawsAndLoot.Gameplay.Players
{
    public static class PlayerRolePermissions
    {
        public static bool CanInteract(
            PlayerRole role,
            PlayerInteractionType interactionType)
        {
            if (!Enum.IsDefined(typeof(PlayerRole), role)
                || !Enum.IsDefined(
                    typeof(PlayerInteractionType),
                    interactionType))
            {
                return false;
            }

            return interactionType switch
            {
                PlayerInteractionType.Generic => true,
                PlayerInteractionType.Traversal => true,
                PlayerInteractionType.Loot => role == PlayerRole.Thief,
                PlayerInteractionType.Sale => role == PlayerRole.Thief,
                PlayerInteractionType.Arrest => role == PlayerRole.Police,
                _ => false
            };
        }
    }
}
