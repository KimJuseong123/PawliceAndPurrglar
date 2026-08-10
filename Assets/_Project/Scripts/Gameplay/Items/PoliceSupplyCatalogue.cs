using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// What the officer can buy, and the rule for buying it.
    ///
    /// This used to live entirely in <see cref="PoliceSupplyCounter"/>, one
    /// counter per kind, standing on the pavement outside the supermarket. Those
    /// three counters were removed on 2026-08-09 once the raccoon sold the same
    /// three things at the same prices — and taking them out took the rule with
    /// them, because <c>TryInteract</c> was both the shop window and the till.
    /// The stall still listed goods (it had a hard-coded fallback for the
    /// display) and every purchase answered "지금은 살 수 없어요", which reads as
    /// the officer being broke rather than as the shop having no counter to ask.
    ///
    /// So the till moved here, where it does not need a GameObject. The counter
    /// component still exists and still works — it now delegates — because the
    /// technical-validation scenes place them and the regression probe counts
    /// them.
    /// </summary>
    public static class PoliceSupplyCatalogue
    {
        /// <summary>
        /// The three props and their prices.
        ///
        /// The numbers are the ones the counters were built with, which is where
        /// the balance for them was done. Cheapest first, so the stall lists them
        /// in the order it always did.
        /// </summary>
        public static readonly (ThrowableKind Kind, int Price)[] Stock =
        {
            (ThrowableKind.TunaCan, 40),
            (ThrowableKind.GlueTrap, 60),
            (ThrowableKind.SensorLight, 90)
        };

        /// <summary>
        /// A completed sale, wherever it was rung up.
        ///
        /// Static because the listener — the sound observer — used to subscribe
        /// to each counter it found in the scene, and with no counters there it
        /// found nothing and the purchase went silent. Raised by the counter as
        /// well as by <see cref="TryBuy"/>, so a scene that still has counters
        /// reports through exactly one path.
        /// </summary>
        public static event Action<ThrowableKind> Purchased;

        /// <summary>
        /// Cached because a purchase is a click, not a frame, but the officer can
        /// click faster than a scene search should run.
        ///
        /// Kept as the concrete type so the null check is Unity's: an interface
        /// reference to a destroyed component is not null in C# and the cache
        /// would answer for a scene that has gone.
        /// </summary>
        private static MatchRuntimeState _matchState;

        /// <summary>
        /// Stands in for the scene's match state in a test that has no scene.
        /// </summary>
        private static IMatchStateReader _matchStateOverride;

        public static int SoldCount { get; private set; }

        /// <summary>
        /// The price, or -1 when the raccoon does not stock it.
        /// </summary>
        public static int GetPrice(ThrowableKind kind)
        {
            foreach ((ThrowableKind stocked, int price) in Stock)
            {
                if (stocked == kind)
                {
                    return price;
                }
            }

            return -1;
        }

        public static bool Sells(ThrowableKind kind)
        {
            return GetPrice(kind) >= 0;
        }

        /// <summary>
        /// Sells one, on the machine that decides.
        ///
        /// Refuses for four separate reasons and each has to be a refusal rather
        /// than a partial sale: the wrong role, a match that is not being played,
        /// no room, or no money. The prop is handed over before the money is
        /// taken, because handing over is the step that can still fail and
        /// charging first would take the money for nothing.
        /// </summary>
        public static bool TryBuy(PlayerRoleIdentity player, ThrowableKind kind)
        {
            if (player == null || player.Role != PlayerRole.Police)
            {
                return false;
            }

            int price = GetPrice(kind);
            if (price < 0 || !IsShopOpen())
            {
                return false;
            }

            var wallet = player.GetComponent<PoliceWallet>();
            var carrier = player.GetComponent<ToolCarrier>();
            if (wallet == null || carrier == null)
            {
                return false;
            }

            if (!carrier.CanStore(kind) || !wallet.CanAfford(price))
            {
                return false;
            }

            if (!carrier.TryPickUp(kind))
            {
                return false;
            }

            if (!wallet.TrySpend(price))
            {
                // Cannot happen after CanAfford, but if it ever does the officer
                // keeps the prop rather than losing both.
                Report(kind, price);
                return true;
            }

            Report(kind, price);
            return true;
        }

        /// <summary>
        /// Announces a sale that a counter rang up itself, so there is one event
        /// for a purchase however it was made.
        /// </summary>
        internal static void ReportCounterSale(ThrowableKind kind, int price)
        {
            Report(kind, price);
        }

        /// <summary>
        /// Reset between play mode tests, which share one editor session. The
        /// count and the subscriber list would otherwise carry over and a test
        /// asserting "bought once" would see the previous test's sale.
        ///
        /// Takes the match state to shop against, because play mode tests run in
        /// whatever scene the previous test left behind: a `MatchRuntimeState`
        /// sitting there between matches answers "not playing" and the shop
        /// closes for reasons that have nothing to do with the test.
        /// </summary>
        public static void ResetForTests(IMatchStateReader matchState = null)
        {
            SoldCount = 0;
            Purchased = null;
            _matchState = null;
            _matchStateOverride = matchState;
        }

        private static void Report(ThrowableKind kind, int price)
        {
            SoldCount++;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Police bought {kind} for {price}.");
            Purchased?.Invoke(kind);
        }

        /// <summary>
        /// Open only while a match is being played.
        ///
        /// A scene with no match state at all — a focused test, a validation
        /// scene — is treated as open. Refusing there would mean the rule could
        /// not be exercised without building a match around it, and the thing
        /// being defended is that the officer cannot shop after the whistle, not
        /// that shopping needs a particular component present.
        /// </summary>
        private static bool IsShopOpen()
        {
            if (_matchStateOverride != null)
            {
                return _matchStateOverride.IsGameplayActive;
            }

            if (_matchState == null)
            {
                _matchState =
                    UnityEngine.Object.FindFirstObjectByType<MatchRuntimeState>();
            }

            return _matchState == null || _matchState.IsGameplayActive;
        }
    }
}
