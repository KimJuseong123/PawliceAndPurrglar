using System;
using System.Collections.Generic;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    [Serializable]
    public sealed class VoiceVisibleTarget
    {
        public string id;
        public string type;
    }

    [DisallowMultipleComponent]
    public sealed class CompanionTargetRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, Transform> targets = new();

        public void Register(string id, Transform target, string type)
        {
            if (string.IsNullOrWhiteSpace(id) || target == null) return;
            targets[id] = target;
        }

        public bool TryResolve(string id, out Transform target)
        {
            if (!string.IsNullOrWhiteSpace(id) && targets.TryGetValue(id, out target))
            {
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            target = null;
            return false;
        }

        public VoiceVisibleTarget[] BuildSnapshot(float maxDistance, Vector3 origin)
        {
            var result = new List<VoiceVisibleTarget>();
            foreach (KeyValuePair<string, Transform> pair in targets)
            {
                if (pair.Value == null
                    || !pair.Value.gameObject.activeInHierarchy
                    || Vector3.Distance(origin, pair.Value.position) > maxDistance)
                {
                    continue;
                }

                result.Add(new VoiceVisibleTarget
                {
                    id = pair.Key,
                    type = pair.Value.GetComponent<LootItem>() != null
                        ? "LOOT"
                        : "ENTITY"
                });
            }

            return result.ToArray();
        }

        public void RebuildFromScene()
        {
            targets.Clear();
            foreach (NetworkPlayerLink link in
                FindObjectsByType<NetworkPlayerLink>(FindObjectsSortMode.None))
            {
                Register(
                    link.Role == PlayerRole.Police
                        ? "player-police"
                        : "player-thief",
                    link.transform,
                    "PLAYER");
            }

            var counts = new Dictionary<string, int>();
            foreach (LootItem loot in
                FindObjectsByType<LootItem>(FindObjectsSortMode.None))
            {
                string baseId = loot.Definition != null
                    ? loot.Definition.StableId
                    : loot.name;
                counts.TryGetValue(baseId, out int index);
                counts[baseId] = index + 1;
                Register(
                    baseId + "-" + index,
                    loot.transform,
                    "LOOT");
            }
        }
    }
}
