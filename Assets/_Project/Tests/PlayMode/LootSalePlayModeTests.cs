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
    public sealed class LootSalePlayModeTests
    {
        [UnityTest]
        public IEnumerator ValidSaleCreditsWalletAndRequestsVictoryCheck()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            PlayerFixture player = CreateThief(state);
            LootItem loot = CreateLoot();
            LootSaleZone zone = CreateSaleZone(state, out LootConfig config);
            int victoryRequests = 0;
            player.Wallet.VictoryCheckRequested +=
                () => victoryRequests++;
            Assert.That(player.Carrier.TryAcquire(loot), Is.True);
            Assert.That(player.Penalty.IsApplied, Is.True);
            Physics.SyncTransforms();

            bool sold = zone.TryInteract(
                new PlayerInteractionContext(player.Identity));

            Assert.That(sold, Is.True);
            Assert.That(player.Wallet.SoldAmount, Is.EqualTo(200));
            Assert.That(player.Wallet.TargetAmount, Is.EqualTo(1000));
            Assert.That(player.Wallet.LastSoldLoot, Is.SameAs(loot));
            Assert.That(player.Wallet.LastSalePrice, Is.EqualTo(200));
            Assert.That(victoryRequests, Is.EqualTo(1));
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Sold));
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(player.Carrier.HeldLoot, Is.Null);
            Assert.That(player.Penalty.IsApplied, Is.False);
            Assert.That(loot.PresentationRoot.gameObject.activeSelf, Is.False);

            DestroyTestObjects(player, loot, zone, config);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaleRequiresPlayingCarriedLootAndZoneContainment()
        {
            var state = new MutableMatchState();
            PlayerFixture player = CreateThief(state);
            LootItem loot = CreateLoot();
            LootSaleZone zone = CreateSaleZone(state, out LootConfig config);
            Assert.That(player.Carrier.TryAcquire(loot), Is.False);

            state.IsGameplayActive = true;
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(player.Identity)),
                Is.False);

            var policeObject = new GameObject("Police Player");
            PlayerRoleIdentity policeIdentity =
                policeObject.AddComponent<PlayerRoleIdentity>();
            policeIdentity.Configure(PlayerRole.Police);
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(policeIdentity)),
                Is.False);

            Assert.That(player.Carrier.TryAcquire(loot), Is.True);
            player.Root.transform.position = new Vector3(10f, 0f, 0f);
            Physics.SyncTransforms();
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(player.Identity)),
                Is.False);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));

            player.Root.transform.position = Vector3.zero;
            state.IsGameplayActive = false;
            Physics.SyncTransforms();
            Assert.That(
                zone.TryInteract(
                    new PlayerInteractionContext(player.Identity)),
                Is.False);
            Assert.That(player.Wallet.SoldAmount, Is.EqualTo(0));

            Object.Destroy(policeObject);
            DestroyTestObjects(player, loot, zone, config);
            yield return null;
        }

        private static PlayerFixture CreateThief(
            IMatchStateReader state)
        {
            var root = new GameObject("Thief Player");
            root.SetActive(false);
            CharacterController controller =
                root.AddComponent<CharacterController>();
            PlayerConfig playerConfig =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, playerConfig, state, null);
            PlayerRoleIdentity identity =
                root.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, state, carryPoint.transform);
            LootCarryMovementPenalty penalty =
                root.AddComponent<LootCarryMovementPenalty>();
            penalty.Configure(carrier, motor);
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            ThiefLootWallet wallet =
                root.AddComponent<ThiefLootWallet>();
            wallet.Configure(identity, matchConfig);
            root.SetActive(true);
            return new PlayerFixture(
                root,
                playerConfig,
                matchConfig,
                identity,
                carrier,
                wallet,
                penalty);
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "sale-test-loot",
                "Sale Test Loot",
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

        private static void DestroyTestObjects(
            PlayerFixture player,
            LootItem loot,
            LootSaleZone zone,
            LootConfig lootConfig)
        {
            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            Object.Destroy(zone.gameObject);
            Object.Destroy(lootConfig);
            Object.Destroy(player.PlayerConfig);
            Object.Destroy(player.MatchConfig);
            Object.Destroy(player.Root);
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }

        private readonly struct PlayerFixture
        {
            public PlayerFixture(
                GameObject root,
                PlayerConfig playerConfig,
                MatchConfig matchConfig,
                PlayerRoleIdentity identity,
                LootCarrier carrier,
                ThiefLootWallet wallet,
                LootCarryMovementPenalty penalty)
            {
                Root = root;
                PlayerConfig = playerConfig;
                MatchConfig = matchConfig;
                Identity = identity;
                Carrier = carrier;
                Wallet = wallet;
                Penalty = penalty;
            }

            public GameObject Root { get; }
            public PlayerConfig PlayerConfig { get; }
            public MatchConfig MatchConfig { get; }
            public PlayerRoleIdentity Identity { get; }
            public LootCarrier Carrier { get; }
            public ThiefLootWallet Wallet { get; }
            public LootCarryMovementPenalty Penalty { get; }
        }
    }
}
