using System;
using UnityEngine;

namespace PawliceAndPurrglar.Config
{
    [CreateAssetMenu(menuName = "PawliceAndPurrglar/Config/Loot", fileName = "LootConfig")]
    public sealed class LootConfig : GameConfigAsset
    {
        /// <summary>
        /// What a piece sells for, against a target of 1,000.
        ///
        /// The ratio between the tiers is the old one — 1 : 1.75 : 2.5 — and
        /// every figure is a fifth of what it was. The fifth is the whole point:
        /// at 200/350/500 two Rare pieces were 1,000, so the thief won with two
        /// presses of the pick-up key and one walk to the merchant, and the
        /// officer's three arrests never got a chance to happen.
        ///
        /// The number to read these against is not the piece, it is the room.
        /// The largest room in town now holds 550 and the smallest 250, so no
        /// single room is a win and the thief has to cross the map at least
        /// twice — which is the only window in which an officer can do anything
        /// at all. See docs/03_GAME_RULES.md 8.6 for the per-room arithmetic.
        /// </summary>
        [Header("Sale Prices")]
        [SerializeField, Min(1)]
        private int commonPrice = 40;

        [SerializeField, Min(1)]
        private int uncommonPrice = 70;

        [SerializeField, Min(1)]
        private int rarePrice = 100;

        public int CommonPrice => commonPrice;
        public int UncommonPrice => uncommonPrice;
        public int RarePrice => rarePrice;

        public int GetPrice(LootRarity rarity)
        {
            return rarity switch
            {
                LootRarity.Common => commonPrice,
                LootRarity.Uncommon => uncommonPrice,
                LootRarity.Rare => rarePrice,
                _ => throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown loot rarity.")
            };
        }

        public override void ValidateOrThrow()
        {
            GameConfigValidation.RequirePositive(this, commonPrice, nameof(commonPrice));
            GameConfigValidation.RequirePositive(this, uncommonPrice, nameof(uncommonPrice));
            GameConfigValidation.RequirePositive(this, rarePrice, nameof(rarePrice));

            if (uncommonPrice < commonPrice)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(uncommonPrice),
                    $"uncommon price must be at least the common price ({commonPrice}), received {uncommonPrice}");
            }

            if (rarePrice < uncommonPrice)
            {
                throw GameConfigValidation.CreateException(
                    this,
                    nameof(rarePrice),
                    $"rare price must be at least the uncommon price ({uncommonPrice}), received {rarePrice}");
            }
        }
    }
}
