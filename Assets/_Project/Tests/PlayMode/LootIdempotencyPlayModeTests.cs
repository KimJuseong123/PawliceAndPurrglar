using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class LootIdempotencyPlayModeTests
    {
        [UnityTest]
        public IEnumerator DuplicateAcquireRequestCannotReplayAfterDrop()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            PlayerFixture player = CreateThief(state);
            LootItem loot = CreateLoot("acquire-idempotency");
            GameObject floor = CreateFloor();
            Physics.SyncTransforms();
            var request = new LootRequestId(100);

            Assert.That(
                player.Carrier.TryAcquire(loot, request),
                Is.True);
            Assert.That(
                player.Carrier.TryAcquire(loot, request),
                Is.False);
            Assert.That(player.Carrier.TryDrop(), Is.True);
            Assert.That(
                player.Carrier.TryAcquire(loot, request),
                Is.False);
            Assert.That(
                player.Carrier.TryAcquire(
                    loot,
                    new LootRequestId(101)),
                Is.True);

            DestroyTestObjects(player, new[] { loot }, null, floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepeatedSaleEventsAndNetworkRequestCreditOnce()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            PlayerFixture player = CreateThief(state);
            LootItem first = CreateLoot("sale-spam");
            LootItem second = CreateLoot("sale-network-duplicate");
            LootSaleZone zone = CreateSaleZone(state, out LootConfig config);
            int victoryRequests = 0;
            player.Wallet.VictoryCheckRequested +=
                () => victoryRequests++;
            Physics.SyncTransforms();

            Assert.That(player.Carrier.TryAcquire(first), Is.True);
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(player.Identity)),
                Is.True);
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(player.Identity)),
                Is.False);
            Assert.That(player.Wallet.SoldAmount, Is.EqualTo(200));

            Assert.That(player.Carrier.TryAcquire(second), Is.True);
            var networkRequest = new LootRequestId(900);
            Assert.That(
                player.Carrier.TrySell(
                    player.Wallet,
                    config,
                    networkRequest),
                Is.True);
            Assert.That(
                player.Carrier.TrySell(
                    player.Wallet,
                    config,
                    networkRequest),
                Is.False);
            Assert.That(
                player.Carrier.TrySell(
                    player.Wallet,
                    config,
                    new LootRequestId(901)),
                Is.False);

            Assert.That(first.CurrentState, Is.EqualTo(LootState.Sold));
            Assert.That(second.CurrentState, Is.EqualTo(LootState.Sold));
            Assert.That(player.Wallet.SoldAmount, Is.EqualTo(400));
            Assert.That(player.Wallet.CreditedSaleCount, Is.EqualTo(2));
            Assert.That(victoryRequests, Is.EqualTo(2));

            DestroyTestObjects(
                player,
                new[] { first, second },
                zone,
                null,
                config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EndingBeforeSaleCannotChangeScore()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            PlayerFixture player = CreateThief(state);
            LootItem loot = CreateLoot("ending-sale");
            LootConfig config =
                ScriptableObject.CreateInstance<LootConfig>();
            Assert.That(player.Carrier.TryAcquire(loot), Is.True);

            state.IsGameplayActive = false;
            Assert.That(
                player.Carrier.TrySell(
                    player.Wallet,
                    config,
                    new LootRequestId(700)),
                Is.False);
            Assert.That(player.Wallet.SoldAmount, Is.EqualTo(0));
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(player.Carrier.HeldLoot, Is.SameAs(loot));

            DestroyTestObjects(
                player,
                new[] { loot },
                null,
                null,
                config);
            yield return null;
        }

        private static PlayerFixture CreateThief(
            IMatchStateReader state)
        {
            var root = new GameObject("Thief Player");
            root.SetActive(false);
            PlayerRoleIdentity identity =
                root.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, state, carryPoint.transform);
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            ThiefLootWallet wallet =
                root.AddComponent<ThiefLootWallet>();
            wallet.Configure(identity, matchConfig);
            root.SetActive(true);
            return new PlayerFixture(
                root,
                matchConfig,
                identity,
                carrier,
                wallet);
        }

        private static LootItem CreateLoot(string stableId)
        {
            var lootObject = new GameObject(stableId);
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                stableId,
                stableId,
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static LootSaleZone CreateSaleZone(
            IMatchStateReader state,
            out LootConfig lootConfig)
        {
            var zoneObject = new GameObject("Sale Zone");
            zoneObject.SetActive(false);
            BoxCollider area = zoneObject.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.size = new Vector3(4f, 4f, 4f);
            lootConfig = ScriptableObject.CreateInstance<LootConfig>();
            LootSaleZone zone =
                zoneObject.AddComponent<LootSaleZone>();
            zone.Configure(area, lootConfig, state);
            zoneObject.SetActive(true);
            return zone;
        }

        private static GameObject CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.25f, 1.25f);
            floor.transform.localScale = new Vector3(8f, 0.5f, 8f);
            return floor;
        }

        private static void DestroyTestObjects(
            PlayerFixture player,
            LootItem[] lootItems,
            LootSaleZone zone,
            GameObject floor = null,
            LootConfig lootConfig = null)
        {
            foreach (LootItem loot in lootItems)
            {
                Object.Destroy(loot.Definition);
                Object.Destroy(loot.gameObject);
            }

            if (zone != null)
            {
                Object.Destroy(zone.gameObject);
            }

            if (floor != null)
            {
                Object.Destroy(floor);
            }

            if (lootConfig != null)
            {
                Object.Destroy(lootConfig);
            }

            Object.Destroy(player.MatchConfig);
            Object.Destroy(player.Root);
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ending;

            public bool IsGameplayActive { get; set; }
        }

        private readonly struct PlayerFixture
        {
            public PlayerFixture(
                GameObject root,
                MatchConfig matchConfig,
                PlayerRoleIdentity identity,
                LootCarrier carrier,
                ThiefLootWallet wallet)
            {
                Root = root;
                MatchConfig = matchConfig;
                Identity = identity;
                Carrier = carrier;
                Wallet = wallet;
            }

            public GameObject Root { get; }
            public MatchConfig MatchConfig { get; }
            public PlayerRoleIdentity Identity { get; }
            public LootCarrier Carrier { get; }
            public ThiefLootWallet Wallet { get; }
        }
    }
}
