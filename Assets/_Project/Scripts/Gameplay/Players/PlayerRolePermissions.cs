using System;

namespace PawliceAndPurrglar.Gameplay.Players
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

                // Both roles, since the raccoon started selling to the officer.
                //
                // It was thief-only, and that is what made the officer's shop
                // impossible: the scanner never offered the market as a target, so
                // the key press had nothing to act on and the screen was never
                // asked to open. The symptom was "the interaction does not work",
                // pointing at the newest code, while the cause was a permission
                // written when only one role had business there.
                //
                // Selling is still the thief's alone — that refusal lives in
                // <c>LootSaleZone.TryInteract</c>, which is where it can say which
                // role it wants. This only decides who may stand at the stall.
                PlayerInteractionType.Sale => true,
                PlayerInteractionType.Arrest => role == PlayerRole.Police,

                // Nobody, on purpose. A walk-through door is not something the
                // press should ever land on.
                PlayerInteractionType.Automatic => false,
                _ => false
            };
        }
    }
}
