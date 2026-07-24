using UnityEngine;

namespace PawsAndLoot.TechnicalValidation
{
    public enum TechnicalPlayerRole : byte
    {
        Unassigned = 0,
        Police = 1,
        Thief = 2
    }

    public static class TechnicalRoleAssignment
    {
        public const int MaximumPlayers = 2;

        public static bool CanApprove(int connectedClientCount)
        {
            return connectedClientCount >= 0
                && connectedClientCount < MaximumPlayers;
        }

        public static TechnicalPlayerRole GetRole(
            ulong clientId,
            ulong serverClientId)
        {
            return clientId == serverClientId
                ? TechnicalPlayerRole.Police
                : TechnicalPlayerRole.Thief;
        }

        public static Vector3 GetSpawnPosition(TechnicalPlayerRole role)
        {
            return role switch
            {
                TechnicalPlayerRole.Police => new Vector3(-2.2f, 0.65f, 0f),
                TechnicalPlayerRole.Thief => new Vector3(2.2f, 0.65f, 0f),
                _ => Vector3.zero
            };
        }
    }
}
