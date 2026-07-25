using System;
using PawsAndLoot.Config;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    [CreateAssetMenu(
        menuName = "Paws & Loot/Loot/Definition",
        fileName = "LootDefinition")]
    public sealed class LootDefinition : ScriptableObject
    {
        [SerializeField]
        private string stableId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private LootRarity rarity;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public LootRarity Rarity => rarity;

        public void Configure(
            string configuredStableId,
            string configuredDisplayName,
            LootRarity configuredRarity)
        {
            stableId = configuredStableId;
            displayName = configuredDisplayName;
            rarity = configuredRarity;
        }

        public int GetPrice(LootConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            return config.GetPrice(rarity);
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new InvalidOperationException(
                    $"Loot definition '{name}' requires a stable ID.");
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new InvalidOperationException(
                    $"Loot definition '{name}' requires a display name.");
            }

            if (!Enum.IsDefined(typeof(LootRarity), rarity))
            {
                throw new InvalidOperationException(
                    $"Loot definition '{name}' has unknown rarity '{rarity}'.");
            }
        }
    }
}
