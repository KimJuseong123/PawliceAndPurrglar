using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// The supermarket counter where the officer buys equipment.
    ///
    /// Rides the interaction system like everything else, so range, match-state
    /// gating and the on-screen prompt behave the way loot and ladders do.
    ///
    /// One counter sells one kind. Two counters side by side is a clearer choice
    /// than one that cycles: the officer reads two prices, decides, and presses —
    /// rather than pressing to browse and losing track of what is selected while
    /// being chased.
    ///
    /// <see cref="PlayerInteractionType.Generic"/> for the same reason the rock
    /// is: Loot belongs to the thief, and a counter typed as Loot would be
    /// invisible to the only person allowed to use it. The role restriction lives
    /// here instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoliceSupplyCounter : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField]
        private ThrowableKind kind = ThrowableKind.GlueTrap;

        [SerializeField, Min(1)]
        private int price = 60;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;

        /// <summary>
        /// A completed sale, after both the prop and the money have moved.
        ///
        /// Raised rather than polled because <see cref="SoldCount"/> only rises
        /// on the machine that decided, and a watcher on the count would have to
        /// hold every counter in the map to see it.
        /// </summary>
        public event System.Action<ThrowableKind> Purchased;

        public ThrowableKind Kind => kind;
        public int Price => price;
        public int SoldCount { get; private set; }

        public bool IsAvailable =>
            isActiveAndEnabled
            && ResolveMatchState()?.IsGameplayActive == true;

        public Transform InteractionTransform => transform;

        public PlayerInteractionType InteractionType =>
            PlayerInteractionType.Generic;

        public string Prompt =>
            $"{ThrowableCatalog.GetDisplayName(kind)} 구매 ({price}골드)";

        public void Configure(
            ThrowableKind configuredKind,
            int configuredPrice,
            IMatchStateReader configuredMatchState)
        {
            kind = configuredKind;
            price = Mathf.Max(1, configuredPrice);
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
        }

        /// <summary>
        /// Host side, like every other interaction. Refuses for three separate
        /// reasons and each one has to be a refusal rather than a partial sale:
        /// the wrong role, no money, or quick slots that cannot fit the item. A
        /// sale that took the money and delivered nothing would be worse than
        /// any of them.
        /// </summary>
        public bool TryInteract(PlayerInteractionContext context)
        {
            if (context.Player == null
                || context.Player.Role != PlayerRole.Police
                || !IsAvailable)
            {
                return false;
            }

            var wallet = context.Player.GetComponent<PoliceWallet>();
            var carrier = context.Player.GetComponent<ToolCarrier>();
            if (wallet == null || carrier == null)
            {
                return false;
            }

            if (!carrier.CanStore(kind))
            {
                // Bag full. Refused before the money is touched.
                return false;
            }

            if (!wallet.CanAfford(price))
            {
                return false;
            }

            // Order matters: the prop is handed over first because that is the
            // step that can still fail. Charging first and failing here would
            // take the money for nothing.
            if (!carrier.TryPickUp(kind))
            {
                return false;
            }

            if (!wallet.TrySpend(price))
            {
                // Cannot happen after CanAfford, but if it ever does the officer
                // keeps the prop rather than losing both.
                return true;
            }

            SoldCount++;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Police bought {kind} for {price}.",
                this);
            Purchased?.Invoke(kind);
            return true;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }
    }
}
