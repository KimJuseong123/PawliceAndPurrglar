using System;
using System.Collections.Generic;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
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

        /// <summary>
        /// Raised only by the shelf path, immediately before
        /// <see cref="SaleAmountChanged"/>.
        ///
        /// The two ways money arrives are a sale at the merchant and a trinket
        /// pocketed in somebody's house, and from the amount alone they are
        /// indistinguishable — so both used to make the merchant's sound. A room
        /// is meant to feel like picking something up, not like closing a deal.
        ///
        /// A separate event rather than an argument on the existing one: every
        /// current subscriber cares about the running total and nothing else,
        /// and widening their signature to say so would be noise in all of them.
        /// </summary>
        public event Action<int> CashPocketed;

        /// <summary>
        /// Ids of the shelves already emptied, so one press pays once.
        /// </summary>
        private readonly HashSet<int> _creditedCashSources = new();

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
            _creditedCashSources.Clear();
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
        /// Host side. Credits cash pocketed straight off a shelf.
        ///
        /// Unlike a treasure, small valuables from inside a house are not carried
        /// to the merchant — they go in a pocket and count at once. That is what
        /// makes a room worth entering: a quick, small, certain gain against the
        /// slow, large, risky business of hauling a treasure across town past
        /// somebody hunting you.
        ///
        /// Deduplicated by the source's own id for the same reason a sale is: an
        /// interact key can be pressed faster than the world can hide what it just
        /// gave away, and a second press must not pay twice.
        /// </summary>
        public bool TryCreditCash(int amount, int sourceId)
        {
            ValidateOrThrow();
            if (amount <= 0 || !_creditedCashSources.Add(sourceId))
            {
                return false;
            }

            int previousAmount = SoldAmount;
            SoldAmount += amount;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Thief pocketed {amount}, now holding {SoldAmount}.",
                this);

            // Before the total changes, so a listener on both can tell which of
            // the two paths this rise came from.
            CashPocketed?.Invoke(amount);
            SaleAmountChanged?.Invoke(previousAmount, SoldAmount);
            VictoryCheckRequested?.Invoke();
            return true;
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
