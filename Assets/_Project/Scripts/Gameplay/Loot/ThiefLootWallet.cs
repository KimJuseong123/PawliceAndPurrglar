using System;
using System.Collections.Generic;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
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

        /// <summary>
        /// Host side. Takes a share of the thief's money off them and reports how
        /// much of it the officer gets to keep.
        ///
        /// This is what makes a thrown rock worth throwing. Without it the police
        /// can stun the thief all night and the number that decides the match
        /// never moves, so the officer's only real play is the arrest — and the
        /// props exist to give them others.
        ///
        /// Taken off the sold total on purpose, because that is the only money in
        /// the game. Two fractions rather than one: the loss is bigger than the
        /// recovery, so money leaves the match. A police economy funded pound for
        /// pound out of the thief's takings would make hitting them a transfer
        /// rather than a setback.
        /// </summary>
        public int Confiscate(float lostFraction, float recoveredFraction)
        {
            ValidateOrThrow();
            if (SoldAmount <= 0 || lostFraction <= 0f)
            {
                return 0;
            }

            int lost = Mathf.Clamp(
                Mathf.RoundToInt(SoldAmount * lostFraction),
                0,
                SoldAmount);
            if (lost <= 0)
            {
                return 0;
            }

            int recovered = Mathf.Clamp(
                Mathf.RoundToInt(SoldAmount * recoveredFraction),
                0,
                lost);

            int previousAmount = SoldAmount;
            SoldAmount -= lost;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Thief lost {lost} of {previousAmount}; police recovered "
                + $"{recovered}.",
                this);
            SaleAmountChanged?.Invoke(previousAmount, SoldAmount);
            return recovered;
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
