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
    /// <summary>
    /// Different treasure has to weigh differently, and taking it has to take
    /// time.
    ///
    /// Both were the same for everything before: one flat speed penalty for
    /// carrying anything at all, and no delay whatsoever for taking it. The
    /// failure mode if either quietly reverts is not an error — the thief simply
    /// steals the crown as fast as the pen and runs at the same speed with it,
    /// and the only symptom is that the game is less interesting. So these are
    /// measured rather than assumed.
    /// </summary>
    public sealed class LootCarryWeightPlayModeTests
    {
        private const float BaseSpeed = 5f;

        [UnityTest]
        public IEnumerator HeavierLootSlowsTheThiefMore()
        {
            GameObject floor = CreateFloor();
            Fixture player = CreatePlayer();

            // Every step is measured against the one before rather than against
            // a written-down number, so the test is about the ordering the
            // design promises and not about four constants it would have to be
            // edited alongside.
            float previous = float.MaxValue;
            foreach (LootCarryType weight in new[]
            {
                LootCarryType.Pocket,
                LootCarryType.OneHand,
                LootCarryType.TwoHand,
                LootCarryType.Bulky
            })
            {
                LootItem loot = CreateLoot(weight);
                Physics.SyncTransforms();

                Assert.That(
                    player.Carrier.TryAcquire(loot),
                    Is.True,
                    $"Could not pick up the {weight} piece.");

                player.Motor.Move(Vector2.right, 0.1f);
                float speed = player.Motor.LastPlanarVelocity.magnitude;

                Assert.That(
                    speed,
                    Is.LessThan(previous),
                    $"{weight} should be slower to carry than the step "
                    + "before it.");
                previous = speed;

                Assert.That(player.Carrier.TryDrop(), Is.True);
                Object.DestroyImmediate(loot.Definition);
                Object.DestroyImmediate(loot.gameObject);
            }

            Object.DestroyImmediate(player.Root);
            Object.DestroyImmediate(player.Config);
            Object.DestroyImmediate(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PocketLootCostsNoSpeedAtAll()
        {
            GameObject floor = CreateFloor();
            Fixture player = CreatePlayer();
            LootItem loot = CreateLoot(LootCarryType.Pocket);
            Physics.SyncTransforms();

            Assert.That(player.Carrier.TryAcquire(loot), Is.True);
            Assert.That(
                player.Carrier.HasLoot,
                Is.True,
                "A pocket piece is still carried.");
            player.Motor.Move(Vector2.right, 0.1f);

            // Carried and not slowed at the same time. The two used to be the
            // same fact, so this is the case that stops them silently becoming
            // one again.
            Assert.That(
                player.Motor.LastPlanarVelocity.magnitude,
                Is.EqualTo(BaseSpeed).Within(0.001f));

            Object.DestroyImmediate(loot.Definition);
            Object.DestroyImmediate(loot.gameObject);
            Object.DestroyImmediate(player.Root);
            Object.DestroyImmediate(player.Config);
            Object.DestroyImmediate(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TakingSomethingTakesTimeAndScalesWithItsSize()
        {
            GameObject floor = CreateFloor();
            Fixture player = CreatePlayer();
            LootPickupProgress progress =
                player.Root.AddComponent<LootPickupProgress>();
            progress.Configure(player.Carrier, player.Motor);

            LootItem pocket = CreateLoot(LootCarryType.Pocket);
            LootItem bulky = CreateLoot(LootCarryType.Bulky);
            Physics.SyncTransforms();

            Assert.That(
                progress.Request(pocket),
                Is.False,
                "Nothing should be in hand on the first frame of asking.");
            Assert.That(
                bulky.Definition.PickupSeconds,
                Is.GreaterThan(pocket.Definition.PickupSeconds),
                "A bulky piece should take longer to take than a pocket one.");

            // Driven a frame at a time, asking on each one, because that is
            // how a held button reaches it. Feeding the whole duration as a
            // single step would look like the player let go and asked again
            // much later, which is exactly what the attempt is meant to
            // abandon.
            Assert.That(
                Hold(progress, pocket, pocket.Definition.PickupSeconds + Frame),
                Is.True,
                "The piece should be in hand once its time has been served.");
            Assert.That(player.Carrier.HeldLoot, Is.SameAs(pocket));

            Object.DestroyImmediate(pocket.Definition);
            Object.DestroyImmediate(pocket.gameObject);
            Object.DestroyImmediate(bulky.Definition);
            Object.DestroyImmediate(bulky.gameObject);
            Object.DestroyImmediate(player.Root);
            Object.DestroyImmediate(player.Config);
            Object.DestroyImmediate(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingAwayThrowsTheTheftAwayRatherThanBankingIt()
        {
            GameObject floor = CreateFloor();
            Fixture player = CreatePlayer();
            LootPickupProgress progress =
                player.Root.AddComponent<LootPickupProgress>();
            progress.Configure(player.Carrier, player.Motor);

            LootItem loot = CreateLoot(LootCarryType.Bulky);
            Physics.SyncTransforms();

            Assert.That(
                Hold(progress, loot, loot.Definition.PickupSeconds * 0.8f),
                Is.False,
                "The piece should not be in hand yet.");
            Assert.That(
                progress.Normalized,
                Is.GreaterThan(0.5f),
                "The attempt should be most of the way through.");

            player.Root.transform.position += new Vector3(3f, 0f, 0f);
            progress.Tick(0.02f);

            Assert.That(
                progress.IsInProgress,
                Is.False,
                "Walking off should abandon the theft.");

            // And starting again starts at nothing. Banking the progress would
            // let a thief nibble at a case from safety and take it whole on the
            // one pass they can afford.
            progress.Request(loot);
            Assert.That(progress.Normalized, Is.EqualTo(0f));
            Assert.That(player.Carrier.HasLoot, Is.False);

            Object.DestroyImmediate(loot.Definition);
            Object.DestroyImmediate(loot.gameObject);
            Object.DestroyImmediate(player.Root);
            Object.DestroyImmediate(player.Config);
            Object.DestroyImmediate(floor);
            yield return null;
        }

        /// <summary>
        /// One frame at sixty a second, which is what the pickup timer is
        /// written against.
        /// </summary>
        private const float Frame = 1f / 60f;

        /// <summary>
        /// Asks for a piece every frame for the given time, the way a held
        /// button does, and reports whether it ended up in hand.
        ///
        /// Time is fed in rather than waited for, so the result does not depend
        /// on how fast the machine running the test happens to be.
        /// </summary>
        private static bool Hold(
            LootPickupProgress progress,
            LootItem loot,
            float seconds)
        {
            for (float spent = 0f; spent < seconds; spent += Frame)
            {
                if (progress.Request(loot))
                {
                    return true;
                }

                progress.Tick(Frame);
            }

            return progress.Request(loot);
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
            carryPoint.transform.localPosition =
                new Vector3(0.7f, 0.8f, 0.4f);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, new Active(), carryPoint.transform);
            LootCarryMovementPenalty penalty =
                root.AddComponent<LootCarryMovementPenalty>();
            penalty.Configure(carrier, motor);
            root.SetActive(true);
            return new Fixture(root, config, motor, carrier);
        }

        private static LootItem CreateLoot(LootCarryType carryType)
        {
            var lootObject = new GameObject($"Loot {carryType}");
            lootObject.SetActive(false);
            BoxCollider collider = lootObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.75f;
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                $"weight-test-{carryType}",
                $"Weight Test {carryType}",
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
            floor.transform.position = new Vector3(0f, -0.25f, 1.25f);
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
                PlayerMovementMotor motor,
                LootCarrier carrier)
            {
                Root = root;
                Config = config;
                Motor = motor;
                Carrier = carrier;
            }

            public GameObject Root { get; }
            public PlayerConfig Config { get; }
            public PlayerMovementMotor Motor { get; }
            public LootCarrier Carrier { get; }
        }
    }
}
