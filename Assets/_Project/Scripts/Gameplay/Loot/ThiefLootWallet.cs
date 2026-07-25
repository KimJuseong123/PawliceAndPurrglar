using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class ThiefLootWallet : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MatchConfig matchConfig;

        public event Action<int, int> SaleAmountChanged;
        public event Action VictoryCheckRequested;

        public int SoldAmount { get; private set; }
        public int TargetAmount =>
            matchConfig != null ? matchConfig.TargetSaleAmount : 0;
        public bool HasReachedTarget =>
            TargetAmount > 0 && SoldAmount >= TargetAmount;
        public LootItem LastSoldLoot { get; private set; }
        public int LastSalePrice { get; private set; }

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            MatchConfig configuredMatchConfig)
        {
            identity = configuredIdentity;
            matchConfig = configuredMatchConfig;
            ResetForMatch();
        }

        public void ResetForMatch()
        {
            SoldAmount = 0;
            LastSoldLoot = null;
            LastSalePrice = 0;
        }

        public void ValidateOrThrow()
        {
            if (identity == null || identity.Role != PlayerRole.Thief)
            {
                throw new InvalidOperationException(
                    $"ThiefLootWallet '{name}' requires the Thief role.");
            }

            if (matchConfig == null)
            {
                throw new InvalidOperationException(
                    $"ThiefLootWallet '{name}' requires MatchConfig.");
            }

            matchConfig.ValidateOrThrow();
        }

        internal bool CanRecordSale(LootItem loot, int price)
        {
            ValidateOrThrow();
            return loot != null
                && loot.Definition != null
                && price > 0;
        }

        internal void RecordSale(LootItem loot, int price)
        {
            if (!CanRecordSale(loot, price)
                || loot.CurrentState != LootState.Sold)
            {
                throw new InvalidOperationException(
                    "Only a successfully sold loot item can be credited.");
            }

            int previousAmount = SoldAmount;
            SoldAmount += price;
            LastSoldLoot = loot;
            LastSalePrice = price;
            SaleAmountChanged?.Invoke(previousAmount, SoldAmount);
            VictoryCheckRequested?.Invoke();
        }

        private void Awake()
        {
            ValidateOrThrow();
        }
    }
}
