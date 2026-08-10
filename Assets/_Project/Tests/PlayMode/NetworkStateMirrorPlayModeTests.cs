using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Arrest;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// NET-005 to NET-007. Covers the receiving half of the replication: what a
    /// non-authority machine does with the host's values.
    ///
    /// No NGO session is started. A session needs two processes and is verified
    /// by the runtime probes; what is checked here is the logic those probes
    /// exercise, so a regression shows up in a two-second test rather than only
    /// in a fifteen-second two-window run.
    /// </summary>
    public sealed class NetworkStateMirrorPlayModeTests
    {
        [UnityTest]
        public IEnumerator RemoteLootStateAdoptsCarrierAndSurvivesIllegalJumps()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            ThiefFixture thief = CreateThief(state);
            LootItem loot = CreateLoot();
            loot.SetRemoteControlled(true);

            // AVAILABLE -> CARRIED is not a legal single step. The authority
            // reached it through RESERVED, and a machine that missed that packet
            // still has to end up agreeing.
            loot.ApplyRemoteState(
                LootState.Carried,
                Vector3.zero,
                thief.Carrier);

            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(loot.CurrentCarrier, Is.SameAs(thief.Carrier));
            Assert.That(
                loot.PresentationRoot.parent,
                Is.SameAs(thief.Carrier.CarryPoint),
                "Carried loot has to be drawn in the carrier's hands.");

            var dropPoint = new Vector3(3f, 0f, 4f);
            loot.ApplyRemoteState(LootState.Dropped, dropPoint, null);

            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Dropped));
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(
                (loot.PresentationRoot.position - dropPoint).magnitude,
                Is.LessThan(0.001f));

            loot.ApplyRemoteState(LootState.Sold, dropPoint, null);

            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Sold));
            Assert.That(
                loot.PresentationRoot.gameObject.activeSelf,
                Is.False,
                "Sold loot must not stay visible on a client.");

            DestroyThief(thief, loot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteLootRefusesLocalInteraction()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            ThiefFixture thief = CreateThief(state);
            LootItem loot = CreateLoot();
            loot.SetRemoteControlled(true);

            bool interacted = loot.TryInteract(
                new PlayerInteractionContext(thief.Identity));

            Assert.That(
                interacted,
                Is.False,
                "A client picking loot up locally would let two machines "
                + "disagree about who holds it.");
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Available));

            // The same item still works once the session hands control back, so
            // the offline playtest is unaffected.
            loot.SetRemoteControlled(false);
            Assert.That(
                loot.TryInteract(
                    new PlayerInteractionContext(thief.Identity)),
                Is.True);

            DestroyThief(thief, loot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteSaleMirrorsTotalWithoutDecidingVictory()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            ThiefFixture thief = CreateThief(state);
            int changes = 0;
            int victoryChecks = 0;
            thief.Wallet.SaleAmountChanged += (_, _) => changes++;
            thief.Wallet.VictoryCheckRequested += () => victoryChecks++;

            thief.Wallet.ApplyRemoteSale(200);
            thief.Wallet.ApplyRemoteSale(200);

            Assert.That(thief.Wallet.SoldAmount, Is.EqualTo(200));
            Assert.That(
                changes,
                Is.EqualTo(1),
                "An unchanged total must not raise the HUD event again.");
            Assert.That(
                victoryChecks,
                Is.EqualTo(0),
                "Victory is the host's decision; a client must not request it "
                + "off a replicated score.");
            Assert.That(
                thief.Wallet.CreditedSaleCount,
                Is.EqualTo(0),
                "The duplicate-sale guard belongs to the host, which is where "
                + "the sale actually happens.");

            DestroyThief(thief, null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteArrestIgnoresLocalTickAndKeepsCompletion()
        {
            ArrestFixture arrest = CreateArrest(true);
            arrest.Progress.SetRemoteControlled(true);

            arrest.Progress.Tick(1f);
            Assert.That(
                arrest.Progress.ProgressSeconds,
                Is.EqualTo(0f),
                "A remote-controlled bar must not advance on its own clock.");

            arrest.Progress.ApplyRemoteProgress(1.2f, false);
            Assert.That(
                arrest.Progress.ProgressSeconds,
                Is.EqualTo(1.2f).Within(0.001f));

            // A local sensor lagging one packet behind would otherwise zero the
            // bar the host is still filling.
            arrest.Progress.Tick(1f);
            Assert.That(
                arrest.Progress.ProgressSeconds,
                Is.EqualTo(1.2f).Within(0.001f));

            arrest.Progress.ApplyRemoteProgress(
                arrest.Config.ArrestDurationSeconds,
                true);
            Assert.That(arrest.Progress.IsCompleted, Is.True);

            // A late packet must not undo a completed arrest.
            arrest.Progress.ApplyRemoteProgress(0f, false);
            Assert.That(arrest.Progress.IsCompleted, Is.True);

            DestroyArrest(arrest);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemoteArrestInterruptionReplaysOncePerHostEvent()
        {
            ArrestFixture arrest = CreateArrest(true);
            arrest.Progress.SetRemoteControlled(true);
            var reasons = new List<ArrestInterruptionReason>();
            arrest.Progress.ProgressInterrupted += reasons.Add;

            arrest.Progress.ApplyRemoteProgress(1.5f, false);
            arrest.Progress.ApplyRemoteInterruption(
                1,
                ArrestInterruptionReason.TargetNoLongerDetectable);

            Assert.That(reasons.Count, Is.EqualTo(1));
            Assert.That(arrest.Progress.ProgressSeconds, Is.EqualTo(0f));

            // The same count arriving again is the same event re-sent, not a
            // second interruption.
            arrest.Progress.ApplyRemoteInterruption(
                1,
                ArrestInterruptionReason.TargetNoLongerDetectable);
            Assert.That(reasons.Count, Is.EqualTo(1));

            arrest.Progress.ApplyRemoteInterruption(
                2,
                ArrestInterruptionReason.MatchNotPlaying);
            Assert.That(reasons.Count, Is.EqualTo(2));
            Assert.That(
                reasons[1],
                Is.EqualTo(ArrestInterruptionReason.MatchNotPlaying));

            DestroyArrest(arrest);
            yield return null;
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState =>
                IsGameplayActive ? MatchState.Playing : MatchState.Lobby;
            public bool IsGameplayActive { get; set; }
        }

        private sealed class ThiefFixture
        {
            public GameObject Root;
            public PlayerRoleIdentity Identity;
            public LootCarrier Carrier;
            public ThiefLootWallet Wallet;
            public PlayerConfig PlayerConfig;
            public MatchConfig MatchConfig;
        }

        private sealed class ArrestFixture
        {
            public GameObject Police;
            public GameObject Thief;
            public ArrestProgressController Progress;
            public ArrestConfig Config;
        }

        private static ThiefFixture CreateThief(IMatchStateReader state)
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
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            ThiefLootWallet wallet =
                root.AddComponent<ThiefLootWallet>();
            wallet.Configure(identity, matchConfig);
            root.SetActive(true);
            return new ThiefFixture
            {
                Root = root,
                Identity = identity,
                Carrier = carrier,
                Wallet = wallet,
                PlayerConfig = playerConfig,
                MatchConfig = matchConfig
            };
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
                "mirror-test-loot",
                "Mirror Test Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static ArrestFixture CreateArrest(bool gameplayActive)
        {
            GameObject police = CreatePlayer("Police", PlayerRole.Police);
            GameObject thief = CreatePlayer("Thief", PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            ArrestConfig config =
                ScriptableObject.CreateInstance<ArrestConfig>();
            var state = new MutableMatchState
            {
                IsGameplayActive = gameplayActive
            };

            police.SetActive(false);
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police.GetComponent<PlayerRoleIdentity>(),
                thief.GetComponent<PlayerRoleIdentity>(),
                config,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, state, config);
            police.SetActive(true);
            Physics.SyncTransforms();
            sensor.Evaluate();

            return new ArrestFixture
            {
                Police = police,
                Thief = thief,
                Progress = progress,
                Config = config
            };
        }

        private static GameObject CreatePlayer(
            string name,
            PlayerRole role)
        {
            var player = new GameObject(name);
            player.SetActive(false);
            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.up;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            player.SetActive(true);
            return player;
        }

        private static void DestroyThief(ThiefFixture thief, LootItem loot)
        {
            if (loot != null)
            {
                Object.Destroy(loot.Definition);
                Object.Destroy(loot.gameObject);
            }

            Object.Destroy(thief.PlayerConfig);
            Object.Destroy(thief.MatchConfig);
            Object.Destroy(thief.Root);
        }

        private static void DestroyArrest(ArrestFixture arrest)
        {
            Object.Destroy(arrest.Config);
            Object.Destroy(arrest.Police);
            Object.Destroy(arrest.Thief);
        }
    }
}
