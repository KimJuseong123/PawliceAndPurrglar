using System;

namespace PawsAndLoot.Gameplay.Players
{
    [Serializable]
    public sealed class PlayerRoleControlBinding
    {
        public PlayerRoleControlBinding(
            PlayerRoleIdentity identity,
            PlayerKeyboardInput keyboardInput,
            PlayerInteractionScanner interactionScanner = null,
            PlayerInteractionInput interactionInput = null)
        {
            Identity = identity;
            KeyboardInput = keyboardInput;
            InteractionScanner = interactionScanner;
            InteractionInput = interactionInput;
        }

        public PlayerRoleIdentity Identity;
        public PlayerKeyboardInput KeyboardInput;
        public PlayerInteractionScanner InteractionScanner;
        public PlayerInteractionInput InteractionInput;

        public PlayerRole Role => Identity.Role;
    }
}
