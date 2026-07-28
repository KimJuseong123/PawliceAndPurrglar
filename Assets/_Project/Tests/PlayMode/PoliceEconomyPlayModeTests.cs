using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The police economy: money comes off a thief who was caught out, and buys
    /// equipment.
    ///
    /// The properties worth pinning down are the ones that would quietly break
    /// the match rather than throw: money appearing from nowhere, the officer's
    /// purse being read as a score, and a shop that charges without delivering.
    /// </summary>
    public sealed class PoliceEconomyPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConfiscationTakesHalfAndGivesThePoliceAFifth()
        {
            // Built inactive and activated last.
            //
            // ThiefLootWallet validates in Awake, and AddComponent on a live
            // GameObject runs Awake immediately — before there is an identity to
            // validate against. That is why every other test here builds players
            // this way.
            var thiefObject = new GameObject("Thief");
            thiefObject.SetActive(false);
            PlayerRoleIdentity thief =
                thiefObject.AddComponent<PlayerRoleIdentity>();
            thief.Configure(PlayerRole.Thief);
            ThiefLootWallet wallet =
                thiefObject.AddComponent<ThiefLootWallet>();
            wallet.Configure(thief, LoadMatchConfig());
            thiefObject.SetActive(true);

            var policeObject = new GameObject("Police");
            policeObject.SetActive(false);
            PlayerRoleIdentity police =
                policeObject.AddComponent<PlayerRoleIdentity>();
            police.Configure(PlayerRole.Police);
            PoliceWallet purse =
                policeObject.AddComponent<PoliceWallet>();
            purse.Configure(0);
            policeObject.SetActive(true);
            yield return null;

            // Nothing banked yet, so the first hit of a match costs the thief
            // only the stun. That is deliberate: an empty thief must not owe
            // money they never had.
            Assert.That(
                LootConfiscationRule.Apply(thief, police),
                Is.EqualTo(0));
            Assert.That(purse.Amount, Is.EqualTo(0));

            wallet.ApplyRemoteSale(400);
            int recovered = LootConfiscationRule.Apply(thief, police);

            Assert.That(
                wallet.SoldAmount,
                Is.EqualTo(200),
                "Half of 400 has to leave the thief.");
            Assert.That(
                recovered,
                Is.EqualTo(80),
                "A fifth of 400 is what the officer keeps.");
            Assert.That(purse.Amount, Is.EqualTo(80));
            Assert.That(
                recovered,
                Is.LessThan(400 - wallet.SoldAmount),
                "The officer must recover less than the thief lost, or a hit "
                + "is a transfer rather than a setback.");

            Object.DestroyImmediate(thiefObject);
            Object.DestroyImmediate(policeObject);
        }

        /// <summary>
        /// Only one direction. The thief taking the officer's purse would mean
        /// the officer's equipment funds itself out of its own failures.
        /// </summary>
        [UnityTest]
        public IEnumerator ConfiscationNeverRunsAgainstThePolice()
        {
            var policeObject = new GameObject("Police");
            policeObject.SetActive(false);
            PlayerRoleIdentity police =
                policeObject.AddComponent<PlayerRoleIdentity>();
            police.Configure(PlayerRole.Police);
            PoliceWallet purse =
                policeObject.AddComponent<PoliceWallet>();
            purse.Configure(500);
            policeObject.SetActive(true);

            var thiefObject = new GameObject("Thief");
            thiefObject.SetActive(false);
            PlayerRoleIdentity thief =
                thiefObject.AddComponent<PlayerRoleIdentity>();
            thief.Configure(PlayerRole.Thief);
            thiefObject.SetActive(true);
            yield return null;

            // Roles swapped: the thief as beneficiary, the officer as victim.
            Assert.That(
                LootConfiscationRule.Apply(police, thief),
                Is.EqualTo(0));
            Assert.That(purse.Amount, Is.EqualTo(500));

            Object.DestroyImmediate(thiefObject);
            Object.DestroyImmediate(policeObject);
        }

        [UnityTest]
        public IEnumerator CounterSellsOnlyToAnOfficerWhoCanAffordIt()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            var counterObject = new GameObject("Counter");
            PoliceSupplyCounter counter =
                counterObject.AddComponent<PoliceSupplyCounter>();
            counter.Configure(ThrowableKind.GlueTrap, 60, state);

            var policeObject = new GameObject("Police");
            policeObject.SetActive(false);
            PlayerRoleIdentity police =
                policeObject.AddComponent<PlayerRoleIdentity>();
            police.Configure(PlayerRole.Police);
            PoliceWallet purse =
                policeObject.AddComponent<PoliceWallet>();
            purse.Configure(50);
            ToolCarrier carrier =
                policeObject.AddComponent<ToolCarrier>();
            carrier.Configure(police, state);
            policeObject.SetActive(true);
            yield return null;

            // Short of the price: refused, and nothing is taken.
            Assert.That(
                counter.TryInteract(
                    new PlayerInteractionContext(police)),
                Is.False);
            Assert.That(purse.Amount, Is.EqualTo(50));
            Assert.That(carrier.HasTool, Is.False);

            purse.Recover(40);
            Assert.That(
                counter.TryInteract(
                    new PlayerInteractionContext(police)),
                Is.True);
            Assert.That(carrier.HeldKind, Is.EqualTo(ThrowableKind.GlueTrap));
            Assert.That(
                purse.Amount,
                Is.EqualTo(30),
                "90 less the 60 price.");

            // Hands already full: refused before the money is touched, so a
            // mistimed press never costs a purchase.
            Assert.That(
                counter.TryInteract(
                    new PlayerInteractionContext(police)),
                Is.False);
            Assert.That(purse.Amount, Is.EqualTo(30));

            // And the thief cannot shop here at all.
            var thiefObject = new GameObject("Thief");
            thiefObject.SetActive(false);
            PlayerRoleIdentity thief =
                thiefObject.AddComponent<PlayerRoleIdentity>();
            thief.Configure(PlayerRole.Thief);
            thiefObject.AddComponent<PoliceWallet>().Configure(999);
            ToolCarrier thiefCarrier =
                thiefObject.AddComponent<ToolCarrier>();
            thiefCarrier.Configure(thief, state);
            thiefObject.SetActive(true);
            yield return null;

            Assert.That(
                counter.TryInteract(
                    new PlayerInteractionContext(thief)),
                Is.False,
                "The thief buying police equipment would collapse the two "
                + "kits into one.");
            Assert.That(thiefCarrier.HasTool, Is.False);

            Object.DestroyImmediate(counterObject);
            Object.DestroyImmediate(policeObject);
            Object.DestroyImmediate(thiefObject);
        }

        /// <summary>
        /// The shop is the only source now. Free props on the map would leave the
        /// purse with nothing to buy, which is a number in the corner of the
        /// screen rather than an economy.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSceneSellsPolicePropsRatherThanScatteringThem()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            PoliceSupplyCounter[] counters = Object
                .FindObjectsByType<PoliceSupplyCounter>(
                    FindObjectsSortMode.None);
            Assert.That(
                counters.Select(counter => counter.Kind),
                Is.EquivalentTo(new[]
                {
                    ThrowableKind.GlueTrap,
                    ThrowableKind.SensorLight
                }));
            foreach (PoliceSupplyCounter counter in counters)
            {
                Assert.That(counter.Price, Is.GreaterThan(0));
            }

            Assert.That(
                Object.FindObjectsByType<ThrowablePickup>(
                        FindObjectsSortMode.None)
                    .Where(pickup =>
                        ThrowableCatalog.GetOwner(pickup.Kind)
                        == PlayerRole.Police),
                Is.Empty,
                "A police prop lying free on the map undercuts the shop it is "
                + "meant to be bought from.");

            // The officer can afford something at kick-off. A first tool that
            // requires the skill the tool provides punishes starting.
            PoliceWallet purse = Object
                .FindObjectsByType<PoliceWallet>(
                    FindObjectsSortMode.None)
                .First();
            Assert.That(
                counters.Any(counter =>
                    purse.CanAfford(counter.Price)),
                Is.True,
                "The officer starts unable to buy anything at all.");
        }

        private static MatchConfig LoadMatchConfig()
        {
            return Resources.FindObjectsOfTypeAll<MatchConfig>()
                .FirstOrDefault()
                ?? ScriptableObject.CreateInstance<MatchConfig>();
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState =>
                IsGameplayActive ? MatchState.Playing : MatchState.Lobby;
            public bool IsGameplayActive { get; set; }
        }
    }
}
