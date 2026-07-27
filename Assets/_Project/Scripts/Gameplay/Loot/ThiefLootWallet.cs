using System;
using System.Collections.Generic;
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

        private readonly HashSet<LootRequestId>
            _creditedSaleRequests = new();
        private readonly HashSet<LootItem>
            _creditedLoot = new();

        public event Action<int, int> SaleAmountChanged;
        public event Action VictoryCheckRequested;

        public int SoldAmount { get; private set; }
        public int TargetAmount =>
            matchConfig != null ? matchConfig.TargetSaleAmount : 0;
        public bool HasReachedTarget =>
            TargetAmount > 0 && SoldAmount >= TargetAmount;
        public LootItem LastSoldLoot { get; private set; }
        public int LastSalePrice { get; private set; }
        public int CreditedSaleCount =>
            _creditedSaleRequests.Count;

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
            _creditedSaleRequests.Clear();
            _creditedLoot.Clear();
        }

        /// <summary>
        /// NET-006. Adopts the host's score.
        ///
        /// The duplicate-sale guard stays on the host, where the sale actually
        /// happens; a client only ever sees the resulting total, so it cannot
        /// double-credit. <see cref="VictoryCheckRequested"/> is deliberately
        /// not raised here — victory is the host's decision and reaches the
        /// client through the replicated match state instead.
        /// </summary>
        public void ApplyRemoteSale(int soldAmount)
        {
            int clamped = Mathf.Max(0, soldAmount);
            if (clamped == SoldAmount)
            {
                return;
            }

            int previousAmount = SoldAmount;
            SoldAmount = clamped;
            SaleAmountChanged?.Invoke(previousAmount, SoldAmount);
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

        internal bool CanRecordSale(
            LootItem loot,
            int price,
            LootRequestId requestId)
        {
            ValidateOrThrow();
            return loot != null
                && loot.Definition != null
                && price > 0
                && requestId.IsValid
                && !_creditedSaleRequests.Contains(requestId)
                && !_creditedLoot.Contains(loot);
        }

        internal void RecordSale(
            LootItem loot,
            int price,
            LootRequestId requestId)
        {
            if (!CanRecordSale(loot, price, requestId)
                || loot.CurrentState != LootState.Sold)
            {
                throw new InvalidOperationException(
                    "Only a successfully sold loot item can be credited.");
            }

            _creditedSaleRequests.Add(requestId);
            _creditedLoot.Add(loot);
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
