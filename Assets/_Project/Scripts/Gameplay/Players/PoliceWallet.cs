using System;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// The officer's recovered money, and the only thing they can spend.
    ///
    /// Deliberately not a score. The thief's total decides the match; this one
    /// buys equipment and nothing else reads it. That separation is what keeps
    /// the police economy from quietly becoming a second win condition — an
    /// officer who could win by hoarding would stop chasing.
    ///
    /// Filled by taking money off a thief who was hit, so every coin in here was
    /// taken from the number that actually matters. Spending it is the officer
    /// choosing between equipment now and more equipment later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceWallet : MonoBehaviour
    {
        /// <summary>
        /// Enough for one purchase at the start.
        ///
        /// Zero would mean the officer cannot buy anything until they land a
        /// hit, and landing a hit is exactly what equipment helps with. A game
        /// where the first tool requires the skill the tool provides is a game
        /// that punishes the player for starting.
        /// </summary>
        [SerializeField, Min(0)]
        private int startingAmount = 120;

        public event Action<int> AmountChanged;

        public int Amount { get; private set; }
        public int RecoveredTotal { get; private set; }
        public int SpentTotal { get; private set; }

        /// <summary>
        /// The purse has to fill itself at runtime.
        ///
        /// <see cref="Amount"/> is a property, not a serialised field, so the
        /// starting amount handed out when the scene was built does not survive
        /// into the build — the officer would begin every match holding zero and
        /// be unable to buy the first tool. The same class of mistake as the leg
        /// animator that had no Awake and silently did nothing.
        /// </summary>
        private void Awake()
        {
            if (Amount == 0 && SpentTotal == 0 && RecoveredTotal == 0)
            {
                ResetForMatch();
            }
        }

        public void Configure(int configuredStartingAmount = -1)
        {
            if (configuredStartingAmount >= 0)
            {
                startingAmount = configuredStartingAmount;
            }

            ResetForMatch();
        }

        public void ResetForMatch()
        {
            Amount = startingAmount;
            RecoveredTotal = 0;
            SpentTotal = 0;
            AmountChanged?.Invoke(Amount);
        }

        /// <summary>
        /// Host side. Adds money taken off the thief.
        /// </summary>
        public void Recover(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Amount += amount;
            RecoveredTotal += amount;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Police recovered {amount}, now holding {Amount}.",
                this);
            AmountChanged?.Invoke(Amount);
        }

        public bool CanAfford(int price)
        {
            return price > 0 && Amount >= price;
        }

        /// <summary>
        /// Host side. Returns false when there is not enough, so a refused
        /// purchase costs nothing and the caller hands nothing over.
        /// </summary>
        public bool TrySpend(int price)
        {
            if (!CanAfford(price))
            {
                return false;
            }

            Amount -= price;
            SpentTotal += price;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Police spent {price}, now holding {Amount}.",
                this);
            AmountChanged?.Invoke(Amount);
            return true;
        }

        /// <summary>
        /// Adopts the host's figure on a machine that does not decide.
        ///
        /// Cannot be refused, for the same reason the tool slot cannot: an
        /// officer shown less money than they have would be told they cannot
        /// afford something the host would happily sell them.
        /// </summary>
        public void ApplyReplicated(int amount)
        {
            int clamped = Mathf.Max(0, amount);
            if (clamped == Amount)
            {
                return;
            }

            Amount = clamped;
            AmountChanged?.Invoke(Amount);
        }
    }
}
