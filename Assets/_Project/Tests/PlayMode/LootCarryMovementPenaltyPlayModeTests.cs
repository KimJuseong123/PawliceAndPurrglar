using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class LootCarryMovementPenaltyPlayModeTests
    {
        [Test]
        public void CarryPenaltyCanRestoreBeforeMotorAwake()
        {
            var root = new GameObject("Inactive Player");
            root.SetActive(false);
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();

            Assert.DoesNotThrow(
                () => motor.SetLootCarryPenalty(true));
            Assert.That(motor.IsLootCarryPenaltyActive, Is.True);

            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator CarryPenaltyAppliesOnceAndClearsOnDrop()
        {
            GameObject floor = CreateFloor();
            PlayerFixture player = CreatePlayer();
            LootItem loot = CreateLoot();
            Physics.SyncTransforms();

            player.Motor.Move(Vector2.right, 0.1f);
            Assert.That(
                player.Motor.LastPlanarVelocity.magnitude,
                Is.EqualTo(5f).Within(0.001f));

            Assert.That(player.Carrier.TryAcquire(loot), Is.True);
            Assert.That(player.Penalty.IsApplied, Is.True);
            Assert.That(
                player.Motor.MovementSpeedMultiplier,
                Is.EqualTo(0.9f).Within(0.001f));
            player.Motor.Move(Vector2.right, 0.1f);
            Assert.That(
                player.Motor.LastPlanarVelocity.magnitude,
                Is.EqualTo(4.5f).Within(0.001f));

            Assert.That(player.Carrier.TryAcquire(loot), Is.False);
            Assert.That(
                player.Motor.MovementSpeedMultiplier,
                Is.EqualTo(0.9f).Within(0.001f));

            Assert.That(player.Carrier.TryDrop(), Is.True);
            Assert.That(player.Penalty.IsApplied, Is.False);
            Assert.That(
                player.Motor.MovementSpeedMultiplier,
                Is.EqualTo(1f));

            DestroyTestObjects(player, loot, floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerLifecycleRestoresBaseSpeedForRematch()
        {
            GameObject floor = CreateFloor();
            PlayerFixture player = CreatePlayer();
            LootItem loot = CreateLoot();
            Physics.SyncTransforms();
            Assert.That(player.Carrier.TryAcquire(loot), Is.True);
            Assert.That(player.Penalty.IsApplied, Is.True);

            player.Root.SetActive(false);
            Assert.That(
                player.Motor.MovementSpeedMultiplier,
                Is.EqualTo(1f));

            player.Root.SetActive(true);
            Assert.That(player.Carrier.HasLoot, Is.False);
            Assert.That(player.Penalty.IsApplied, Is.False);
            player.Motor.Move(Vector2.right, 0.1f);
            Assert.That(
                player.Motor.LastPlanarVelocity.magnitude,
                Is.EqualTo(5f).Within(0.001f));

            DestroyTestObjects(player, loot, floor);
            yield return null;
        }

        private static PlayerFixture CreatePlayer()
        {
            var root = new GameObject("Thief Player");
            root.SetActive(false);
            root.transform.position = Vector3.up;
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(
                controller,
                config,
                new ActiveMatchState(),
                null);
            PlayerRoleIdentity identity =
                root.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            carryPoint.transform.localPosition =
                new Vector3(0.7f, 0.8f, 0.4f);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(
                identity,
                new ActiveMatchState(),
                carryPoint.transform);
            LootCarryMovementPenalty penalty =
                root.AddComponent<LootCarryMovementPenalty>();
            penalty.Configure(carrier, motor);
            root.SetActive(true);
            return new PlayerFixture(
                root,
                config,
                motor,
                carrier,
                penalty);
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            BoxCollider collider = lootObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.75f;
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "penalty-test-loot",
                "Penalty Test Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
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
            LootItem loot,
            GameObject floor)
        {
            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            Object.Destroy(player.Config);
            Object.Destroy(player.Root);
            Object.Destroy(floor);
        }

        private sealed class ActiveMatchState : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }

        private readonly struct PlayerFixture
        {
            public PlayerFixture(
                GameObject root,
                PlayerConfig config,
                PlayerMovementMotor motor,
                LootCarrier carrier,
                LootCarryMovementPenalty penalty)
            {
                Root = root;
                Config = config;
                Motor = motor;
                Carrier = carrier;
                Penalty = penalty;
            }

            public GameObject Root { get; }
            public PlayerConfig Config { get; }
            public PlayerMovementMotor Motor { get; }
            public LootCarrier Carrier { get; }
            public LootCarryMovementPenalty Penalty { get; }
        }
    }
}
