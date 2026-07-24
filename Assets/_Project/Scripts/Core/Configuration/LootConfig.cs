using System;
using UnityEngine;

namespace PawsAndLoot.Config
{
    [CreateAssetMenu(menuName = "Paws & Loot/Config/Loot", fileName = "LootConfig")]
    public sealed class LootConfig : GameConfigAsset
    {
        [Header("Sale Prices")]
        [SerializeField, Min(1)]
        private int commonPrice = 200;

        [SerializeField, Min(1)]
        private int uncommonPrice = 350;

        [SerializeField, Min(1)]
        private int rarePrice = 500;

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
