using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Sensing;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The glass case and the alarm it protects.
    ///
    /// Both are trades rather than obstacles, and a trade breaks quietly. A
    /// case that lets the treasure be taken through it is just decoration; an
    /// alarm that never speeds the officer up is a tax on the thief with
    /// nothing bought. Neither would log anything or fail to build.
    /// </summary>
    public sealed class DisplayCaseAndAlarmPlayModeTests
    {
        private const float Frame = 1f / 60f;

        [UnityTest]
        public IEnumerator TheGlassKeepsTheTreasureOutOfReachUntilItBreaks()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();
            LootItem jewel = CreateLoot(alarmed: false);
            LootDisplayCase display = CreateCase(jewel, board);
            yield return null;

            Assert.That(display.IsSealed, Is.True);
            Assert.That(
                jewel.enabled,
                Is.False,
                "A sealed case should take its contents out of reach "
                + "entirely, not merely refuse.");

            // The officer cannot break it. Their half of the game is catching
            // somebody, not robbing the shop.
            Assert.That(
                display.Request(PlayerRole.Police),
                Is.False);

            bool broke = false;
            for (float spent = 0f;
                spent < LootDisplayCase.BreakSeconds + Frame * 2f;
                spent += Frame)
            {
                if (display.Request(PlayerRole.Thief))
                {
                    broke = true;
                    break;
                }

                display.Tick(Frame);
            }

            Assert.That(broke, Is.True, "The thief should get through.");
            Assert.That(display.IsSealed, Is.False);
            Assert.That(jewel.enabled, Is.True);

            // And the street heard it.
            Assert.That(board.ReportedCount, Is.EqualTo(1));
            Assert.That(
                board.Latest.Radius,
                Is.EqualTo(LootDisplayCase.SmashRadiusMeters));

            Object.DestroyImmediate(display.gameObject);
            Object.DestroyImmediate(jewel.Definition);
            Object.DestroyImmediate(jewel.gameObject);
            Object.DestroyImmediate(boardObject);
        }

        [UnityTest]
        public IEnumerator LettingGoOfTheGlassLosesTheProgress()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();
            LootItem jewel = CreateLoot(alarmed: false);
            LootDisplayCase display = CreateCase(jewel, board);
            yield return null;

            for (float spent = 0f;
                spent < LootDisplayCase.BreakSeconds * 0.7f;
                spent += Frame)
            {
                display.Request(PlayerRole.Thief);
                display.Tick(Frame);
            }

            Assert.That(display.Normalized, Is.GreaterThan(0.5f));

            // Stopped asking. Half-broken glass that stayed half broken would
            // let a thief work at a case over several safe passes and pay the
            // noise once.
            display.Tick(1f);
            Assert.That(display.Normalized, Is.EqualTo(0f));
            Assert.That(display.IsSealed, Is.True);

            Object.DestroyImmediate(display.gameObject);
            Object.DestroyImmediate(jewel.Definition);
            Object.DestroyImmediate(jewel.gameObject);
            Object.DestroyImmediate(boardObject);
        }

        [UnityTest]
        public IEnumerator LiftingTheWatchedPieceSpeedsThePoliceUp()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();
            var alarmObject = new GameObject("Loot Alarm");
            LootAlarm alarm = alarmObject.AddComponent<LootAlarm>();
            alarm.Configure(board);

            GameObject floor = CreateFloor();
            Fixture thief = CreatePlayer(PlayerRole.Thief);
            Fixture police = CreatePlayer(PlayerRole.Police);

            // The officer's torch rule, which is where "the thief is visible"
            // is actually stored. Without it this test could only ever see half
            // the alarm — and for a long time that is all it did see.
            FlashlightVisibility watcher =
                police.Root.AddComponent<FlashlightVisibility>();
            watcher.Configure(
                police.Root.GetComponent<PlayerRoleIdentity>(),
                new Active());

            LootItem jewel = CreateLoot(alarmed: true);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(alarm.RaisedCount, Is.EqualTo(0));
            Assert.That(watcher.IsRevealed, Is.False);
            Assert.That(
                police.Motor.MovementSpeedMultiplier,
                Is.EqualTo(1f).Within(0.001f));

            Vector3 cushion = jewel.transform.position;
            Assert.That(thief.Carrier.TryAcquire(jewel), Is.True);

            Assert.That(
                alarm.RaisedCount,
                Is.EqualTo(1),
                "Lifting the watched piece should sound the alarm.");
            Assert.That(
                alarm.LastRaisedAt,
                Is.EqualTo(cushion),
                "The alarm should point at the empty cushion, not at the "
                + "thief.");

            Assert.That(
                police.Motor.BoostMultiplier,
                Is.EqualTo(LootAlarm.PoliceBoostMultiplier).Within(0.001f));
            Assert.That(
                police.Motor.MovementSpeedMultiplier,
                Is.GreaterThan(1f),
                "The officer should actually be faster, not merely flagged.");

            // The thief is slower and the officer faster at the same time,
            // which is the trade. Composed rather than replacing, so a laden
            // thief is still laden.
            Assert.That(
                thief.Motor.MovementSpeedMultiplier,
                Is.LessThan(1f));

            // The other half of the alarm, which **never happened once.**
            //
            // `LootAlarm` asked the thief for a `FlashlightVisibility`, and only
            // the officer has one — the component sits on the watcher and hides
            // the other side. The null-conditional swallowed it silently. The
            // siren sounded and the sprint landed, so from here it looked like a
            // working alarm; the four seconds of exposure that make the piece
            // worth guarding were simply absent.
            // Greater than zero rather than exactly one: an earlier test in
            // this assembly leaves the real Game scene loaded, so its officer is
            // standing here too. The distinction that matters is zero versus
            // not-zero — zero is the bug, and it was zero every time.
            Assert.That(
                alarm.LastRevealedWatchers,
                Is.GreaterThan(0),
                "Zero means the alarm exposed nobody.");
            Assert.That(
                watcher.IsRevealed,
                Is.True,
                "The thief has to be visible through the dark, which is the "
                + "entire reason the case is worth alarming.");
            Assert.That(
                watcher.RevealRemainingSeconds,
                Is.GreaterThan(LootAlarm.ThiefRevealSeconds - 0.5f),
                "Four seconds, not the sensor light's 2.5.");
            Assert.That(
                watcher.RevealSource,
                Is.EqualTo(cushion),
                "Lit from the cushion, so the mark is the place worth running "
                + "to and not one the thief has already left.");

            // And it ends. An alarm that never expires is an officer who is
            // permanently fast, which reads as the officer being broken.
            police.Motor.TickBoost(LootAlarm.PoliceBoostSeconds + 0.1f);
            Assert.That(
                police.Motor.MovementSpeedMultiplier,
                Is.EqualTo(1f).Within(0.001f));

            Object.DestroyImmediate(jewel.Definition);
            Object.DestroyImmediate(jewel.gameObject);
            Object.DestroyImmediate(thief.Root);
            Object.DestroyImmediate(thief.Config);
            Object.DestroyImmediate(police.Root);
            Object.DestroyImmediate(police.Config);
            Object.DestroyImmediate(alarmObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(floor);
        }

        [UnityTest]
        public IEnumerator AnOrdinaryPieceSoundsNoAlarm()
        {
            var boardObject = new GameObject("Noise Board");
            NoiseBoard board = boardObject.AddComponent<NoiseBoard>();
            var alarmObject = new GameObject("Loot Alarm");
            LootAlarm alarm = alarmObject.AddComponent<LootAlarm>();
            alarm.Configure(board);

            GameObject floor = CreateFloor();
            Fixture thief = CreatePlayer(PlayerRole.Thief);
            LootItem trinket = CreateLoot(alarmed: false);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(thief.Carrier.TryAcquire(trinket), Is.True);

            // An alarm on everything is an alarm on nothing. The decision only
            // exists while there are still quiet things to take.
            Assert.That(alarm.RaisedCount, Is.EqualTo(0));
            Assert.That(board.ReportedCount, Is.EqualTo(0));

            Object.DestroyImmediate(trinket.Definition);
            Object.DestroyImmediate(trinket.gameObject);
            Object.DestroyImmediate(thief.Root);
            Object.DestroyImmediate(thief.Config);
            Object.DestroyImmediate(alarmObject);
            Object.DestroyImmediate(boardObject);
            Object.DestroyImmediate(floor);
        }

        private static LootDisplayCase CreateCase(
            LootItem contents,
            NoiseBoard board)
        {
            var caseObject = new GameObject("Display Case");
            caseObject.transform.position = contents.transform.position;
            var pane = new GameObject("Glass");
            pane.transform.SetParent(caseObject.transform, false);
            LootDisplayCase display =
                caseObject.AddComponent<LootDisplayCase>();
            display.Configure(
                contents,
                pane.transform,
                new Active(),
                board);
            return display;
        }

        private static Fixture CreatePlayer(PlayerRole role)
        {
            var root = new GameObject($"{role} Player");
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
            identity.Configure(role);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(root.transform, false);
            LootCarrier carrier = root.AddComponent<LootCarrier>();
            carrier.Configure(identity, new Active(), carryPoint.transform);
            root.AddComponent<LootCarryMovementPenalty>()
                .Configure(carrier, motor);
            root.SetActive(true);
            return new Fixture(root, config, motor, carrier);
        }

        private static LootItem CreateLoot(bool alarmed)
        {
            var lootObject = new GameObject(
                alarmed ? "Crown Jewel" : "Trinket");
            lootObject.SetActive(false);
            lootObject.transform.position = new Vector3(2f, 0.5f, 0f);
            BoxCollider collider = lootObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.75f;
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                alarmed ? "alarm-test-jewel" : "alarm-test-trinket",
                alarmed ? "Alarm Test Jewel" : "Alarm Test Trinket",
                alarmed ? LootRarity.Rare : LootRarity.Common,
                alarmed ? LootCarryType.Bulky : LootCarryType.OneHand,
                alarmed);
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
