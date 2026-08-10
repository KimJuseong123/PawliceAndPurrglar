using System;
using PawliceAndPurrglar.Gameplay.Map;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Players
{
    public static class PlayerRoleSpawnResolver
    {
        public static GreyboxLocationId GetLocationId(PlayerRole role)
        {
            return role switch
            {
                PlayerRole.Police => GreyboxLocationId.PoliceSpawn,
                PlayerRole.Thief => GreyboxLocationId.ThiefSpawn,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "Unknown player role.")
            };
        }

        public static Transform Resolve(
            GreyboxMapDefinition map,
            PlayerRole role)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            return map.GetLocation(GetLocationId(role));
        }
    }
}
