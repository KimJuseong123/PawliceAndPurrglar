using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    public readonly struct PlayerInteractionContext
    {
        public PlayerInteractionContext(PlayerRoleIdentity player)
        {
            Player = player;
        }

        public PlayerRoleIdentity Player { get; }
        public PlayerRole Role => Player.Role;
        public Transform PlayerTransform => Player.transform;
    }
}
