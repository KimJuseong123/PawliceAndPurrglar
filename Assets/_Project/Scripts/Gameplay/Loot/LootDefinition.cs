using System;
using PawsAndLoot.Config;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    [CreateAssetMenu(
        menuName = "PawliceAndPurrglar/Loot/Definition",
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

        /// <summary>
        /// Model stem under <c>Assets/_Project/Art/Props</c>, or empty to fall
        /// back to the shared placeholder.
        ///
        /// On the piece rather than on the spot it stands in, for the same
        /// reason the alarm is: it follows the piece. A gold bar dropped in an
        /// alley is still a gold bar, and the loot the thief is carrying is
        /// drawn from this too.
        ///
        /// Every piece used to draw the same jewellery box, so the supermarket,
        /// the bookshop and the jeweller's all sold the same object at three
        /// prices and the thief had no way to tell a five-hundred-gold ring from
        /// a two-hundred-gold loaf until they had picked it up.
        /// </summary>
        [SerializeField]
        private string modelStem = string.Empty;

        public string StableId => stableId;

        /// <summary>
        /// A number for this kind that both machines compute the same way.
        ///
        /// A sale request crosses the wire as "five of this kind", and the kind
        /// has to survive the trip. <c>string.GetHashCode</c> cannot be used:
        /// it is randomised per process, so the host and the client would derive
        /// different numbers for the same gemstone and every sale would be
        /// refused as an unknown kind — on some runs and not others.
        /// </summary>
        public int IdHash => ComputeIdHash(stableId);

        public static int ComputeIdHash(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 0;
            }

            unchecked
            {
                int hash = 23;
                foreach (char character in id)
                {
                    hash = (hash * 31) + character;
                }

                return hash;
            }
        }
        public string DisplayName => displayName;
        public LootRarity Rarity => rarity;
        public LootCarryType CarryType => carryType;
        public bool RaisesAlarm => raisesAlarm;
        public string ModelStem => modelStem;

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
            bool configuredRaisesAlarm = false,
            string configuredModelStem = null)
        {
            stableId = configuredStableId;
            displayName = configuredDisplayName;
            rarity = configuredRarity;
            carryType = configuredCarryType;
            raisesAlarm = configuredRaisesAlarm;
            modelStem = configuredModelStem ?? string.Empty;
        }

        /// <summary>
        /// How big this piece should be drawn, in metres along its longest side.
        ///
        /// Taken from how it is carried rather than written per piece, because
        /// the two are the same fact said twice: a pocket piece is small enough
        /// to pocket. One rule means a new piece cannot be authored at the wrong
        /// size, and it makes the carrying cost visible before it is picked up —
        /// the thief can see that the thing across the room is a two-hander.
        ///
        /// Needed at all because these models are generated and arrive at sizes
        /// with no relation to each other or to the town; the importer scales
        /// each one until it measures this.
        /// </summary>
        public static float GetModelSize(LootCarryType carryType)
        {
            return carryType switch
            {
                LootCarryType.Pocket => 0.22f,
                LootCarryType.OneHand => 0.38f,
                LootCarryType.TwoHand => 0.62f,
                _ => 0.85f
            };
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
