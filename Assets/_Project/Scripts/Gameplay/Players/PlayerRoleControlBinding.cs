using System;

namespace PawsAndLoot.Gameplay.Players
{
    [Serializable]
    public sealed class PlayerRoleControlBinding
    {
        public PlayerRoleControlBinding(
            PlayerRoleIdentity identity,
            PlayerKeyboardInput keyboardInput)
        {
            Identity = identity;
            KeyboardInput = keyboardInput;
        }

        public PlayerRoleIdentity Identity;
        public PlayerKeyboardInput KeyboardInput;

        public PlayerRole Role => Identity.Role;
    }
}
