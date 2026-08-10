using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    public sealed class PlayerRoleIdentity : MonoBehaviour
    {
        [SerializeField]
        private PlayerRole role;

        public PlayerRole Role => role;

        public void Configure(PlayerRole configuredRole)
        {
            role = configuredRole;
        }

        public bool CanInteract(PlayerInteractionType interactionType)
        {
            return PlayerRolePermissions.CanInteract(role, interactionType);
        }
    }
}
