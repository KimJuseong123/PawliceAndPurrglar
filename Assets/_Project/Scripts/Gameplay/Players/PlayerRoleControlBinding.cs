using System;
using PawsAndLoot.Gameplay.Loot;

namespace PawsAndLoot.Gameplay.Players
{
    [Serializable]
    public sealed class PlayerRoleControlBinding
    {
        public PlayerRoleControlBinding(
            PlayerRoleIdentity identity,
            PlayerKeyboardInput keyboardInput,
            PlayerInteractionScanner interactionScanner = null,
            PlayerInteractionInput interactionInput = null,
            LootDropInput lootDropInput = null)
        {
            Identity = identity;
            KeyboardInput = keyboardInput;
            InteractionScanner = interactionScanner;
            InteractionInput = interactionInput;
            LootDropInput = lootDropInput;
        }

        public PlayerRoleIdentity Identity;
        public PlayerKeyboardInput KeyboardInput;
        public PlayerInteractionScanner InteractionScanner;
        public PlayerInteractionInput InteractionInput;
        public LootDropInput LootDropInput;

        public PlayerRole Role => Identity.Role;
    }
}
