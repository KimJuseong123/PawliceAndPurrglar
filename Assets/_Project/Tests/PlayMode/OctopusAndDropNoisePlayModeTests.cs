using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Sensing;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The frozen octopus and the sound of dropping something heavy.
    ///
    /// Both are worth having only because of what they are not. The octopus is
    /// not a second rock — it takes sight and leaves speed alone — and the drop
    /// noise is not on everything, only on what was heavy enough to slow the
    /// thief down in the first place. Collapse either distinction and the prop
    /// still works, still logs nothing, and stops being a decision.
    /// </summary>
    public sealed class OctopusAndDropNoisePlayModeTests
    {
        [Test]
        public void TheOctopusTakesSightAndTheRockTakesTime()
        {
            // The one that holds you does not blind you.
            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.Rock),
                Is.GreaterThan(0f));
            Assert.That(
                ThrowableCatalog.GetBlindSeconds(ThrowableKind.Rock),
                Is.EqualTo(0f));

            // And the one that blinds you does not hold you. Giving the thief
            // two props that both buy seconds would be giving them one prop
            // twice.
            Assert.That(
                ThrowableCatalog.GetBlindSeconds(ThrowableKind.FrozenOctopus),
                Is.GreaterThan(0f));
            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.FrozenOctopus),
                Is.EqualTo(0f),
                "The octopus must not hold anybody. That is the rock's job.");

            // Thrown, not placed. A blinding you have to walk into would be a
            // trap, and the thief already has two of those.
            Assert.That(
                ThrowableCatalog.GetUse(ThrowableKind.FrozenOctopus),
                Is.EqualTo(ThrowableUse.Thrown));
            Assert.That(
                ThrowableCatalog.GetOwner(ThrowableKind.FrozenOctopus),
                Is.EqualTo(PlayerRole.Thief));
        }

        [Test]
        public void BlindnessStacksAndRunsOut()
        {
            var victim = new GameObject("Victim");
            BlindedState blinded = victim.AddComponent<BlindedState>();

            Assert.That(blinded.IsBlinded, Is.False);
            Assert.That(blinded.AppliedCount, Is.EqualTo(0));

            Assert.That(blinded.TryApply(2f), Is.True);
            Assert.That(blinded.IsBlinded, Is.True);

            // Two in a row is worse than one. A stun refuses to stack because
            // being held forever is not a game; being unable to see for longer
            // is survivable, so it adds up.
            blinded.TryApply(2f);
            Assert.That(
                blinded.RemainingSeconds,
                Is.EqualTo(4f).Within(0.001f));
            Assert.That(blinded.AppliedCount, Is.EqualTo(2));

            blinded.Tick(4.1f);
            Assert.That(blinded.IsBlinded, Is.False);
            Assert.That(
                blinded.AppliedCount,
                Is.EqualTo(2),
                "Wearing off is not forgetting that it happened.");

            Object.DestroyImmediate(victim);
        }

        [UnityTest]
        public IEnumerator DroppingSomethingHeavySaysWhereYouAre()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();
            GameObject floor = CreateFloor();
            Fixture thief = CreatePlayer();
            yield return null;

            // A pocket piece makes no sound worth hearing. An event nobody can
            // act on is worse than no event — it teaches the officer to ignore
            // the one signal that matters.
            LootItem watch = CreateLoot(LootCarryType.Pocket);
            Physics.SyncTransforms();
            Assert.That(thief.Carrier.TryAcquire(watch), Is.True);
            Assert.That(thief.Carrier.TryDrop(), Is.True);
            Assert.That(
                board.ReportedCount,
                Is.EqualTo(0),
                "A watch hitting the pavement is not news.");

            LootItem bar = CreateLoot(LootCarryType.Bulky);
            Physics.SyncTransforms();
            Assert.That(thief.Carrier.TryAcquire(bar), Is.True);
            Assert.That(thief.Carrier.TryDrop(), Is.True);

            Assert.That(
                board.ReportedCount,
                Is.EqualTo(1),
                "Dropping the heaviest thing on the map should be heard.");
            Assert.That(
                board.Latest.Radius,
                Is.EqualTo(
                    LootCarryRules.DropNoiseRadius(LootCarryType.Bulky)));

            // The same fact from two sides: what makes it slow to carry is what
            // makes it loud to drop.
            Assert.That(
                LootCarryRules.DropNoiseRadius(LootCarryType.Bulky),
                Is.GreaterThan(
                    LootCarryRules.DropNoiseRadius(LootCarryType.OneHand)));

            Object.DestroyImmediate(watch.Definition);
            Object.DestroyImmediate(watch.gameObject);
            Object.DestroyImmediate(bar.Definition);
            Object.DestroyImmediate(bar.gameObject);
            Object.DestroyImmediate(thief.Root);
            Object.DestroyImmediate(thief.Config);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(floor);
        }

        private static Fixture CreatePlayer()
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
            motor.Configure(controller, config, new Active(), null);
            PlayerRoleIdentity identity =
                root.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, new Active(), carryPoint.transform);
            root.SetActive(true);
            return new Fixture(root, config, carrier);
        }

        private static LootItem CreateLoot(LootCarryType carryType)
        {
            var lootObject = new GameObject($"Loot {carryType}");
            lootObject.SetActive(false);
            lootObject.transform.position = new Vector3(2f, 0.5f, 0f);
            BoxCollider collider = lootObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.75f;
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                $"drop-test-{carryType}",
                $"Drop Test {carryType}",
                LootRarity.Common,
                carryType);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static GameObject CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.5f, 20f);
            return floor;
        }

        private sealed class Active : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }

        private readonly struct Fixture
        {
            public Fixture(
                GameObject root,
                PlayerConfig config,
                LootCarrier carrier)
            {
                Root = root;
                Config = config;
                Carrier = carrier;
            }

            public GameObject Root { get; }
            public PlayerConfig Config { get; }
            public LootCarrier Carrier { get; }
        }
    }
}
