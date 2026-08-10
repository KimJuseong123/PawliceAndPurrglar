using System;
using System.Collections.Generic;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Voice
{
    public static class LocalAiCommandContext
    {
        public static string BuildJson(
            PlayerRoleIdentity issuer,
            string petId)
        {
            PlayerRole role = issuer != null
                ? issuer.Role
                : petId == "dog" ? PlayerRole.Police : PlayerRole.Thief;
            Vector3 origin = issuer != null ? issuer.transform.position : Vector3.zero;
            Vector3 lookPosition = issuer != null
                ? issuer.transform.position + issuer.transform.forward * 8f
                : Vector3.zero;
            lookPosition.y = origin.y;

            CompanionTargetRegistry registry =
                UnityEngine.Object.FindFirstObjectByType<CompanionTargetRegistry>();
            VoiceVisibleTarget[] snapshot = registry != null
                ? registry.BuildSnapshot(18f, origin)
                : Array.Empty<VoiceVisibleTarget>();
            var ids = new List<string>();
            foreach (VoiceVisibleTarget target in snapshot)
            {
                if (target != null && !string.IsNullOrWhiteSpace(target.id))
                {
                    ids.Add(target.id);
                }
            }

            CompanionKind kind = CompanionCommandCatalog.GetCompanionKind(role);
            var context = new LocalAiContextDto
            {
                actorRole = role.ToString().ToUpperInvariant(),
                animalType = kind.ToString().ToUpperInvariant(),
                dogState = "IDLE",
                lookTargetId = string.Empty,
                lookWorldPosition = Vector3Dto.FromVector3(lookPosition),
                visibleTargetIds = ids.ToArray(),
                availableCommands = GetAvailableCommands(kind)
            };
            return JsonUtility.ToJson(context);
        }

        private static string[] GetAvailableCommands(CompanionKind kind)
        {
            return kind == CompanionKind.Dog
                ? new[] { "TRACK", "SEARCH", "GUARD", "BARK", "STAY", "STOP" }
                : new[] { "SCOUT", "DISTRACT", "ROOF", "HIDE", "STAY", "STOP" };
        }
    }
}
