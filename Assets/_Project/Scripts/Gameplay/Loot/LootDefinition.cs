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

        /// <summary>
        /// How much of the thief's hands and speed this costs to carry.
        ///
        /// Defaults to one hand, which is what every piece effectively was
        /// before this existed, so a definition written without it keeps
        /// behaving exactly as it did.
        /// </summary>
        [SerializeField]
        private LootCarryType carryType = LootCarryType.OneHand;

        /// <summary>
        /// Whether lifting this sounds the shop's alarm.
        ///
        /// A property of the piece rather than of where it is standing, because
        /// it follows the piece: a ring dropped in an alley and picked up again
        /// is still the ring the whole town is looking for.
        ///
        /// Off by default. An alarm on everything is an alarm on nothing.
        /// </summary>
        [SerializeField]
        private bool raisesAlarm;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public LootRarity Rarity => rarity;
        public LootCarryType CarryType => carryType;
        public bool RaisesAlarm => raisesAlarm;

        /// <summary>
        /// Seconds the thief must stand still to take it.
        /// </summary>
        public float PickupSeconds =>
            LootCarryRules.PickupSeconds(carryType);

        public void Configure(
            string configuredStableId,
            string configuredDisplayName,
            LootRarity configuredRarity)
        {
            Configure(
                configuredStableId,
                configuredDisplayName,
                configuredRarity,
                LootCarryType.OneHand);
        }

        public void Configure(
            string configuredStableId,
            string configuredDisplayName,
            LootRarity configuredRarity,
            LootCarryType configuredCarryType,
            bool configuredRaisesAlarm = false)
        {
            stableId = configuredStableId;
            displayName = configuredDisplayName;
            rarity = configuredRarity;
            carryType = configuredCarryType;
            raisesAlarm = configuredRaisesAlarm;
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

            if (!Enum.IsDefined(typeof(LootCarryType), carryType))
            {
                throw new InvalidOperationException(
                    $"Loot definition '{name}' has unknown carry type "
                    + $"'{carryType}'.");
            }
        }
    }
}
