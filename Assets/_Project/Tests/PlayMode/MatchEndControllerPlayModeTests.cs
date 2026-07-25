using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class MatchEndControllerPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResultEndsMatchAndStopsGameplayOnce()
        {
            EndFixture fixture = CreateFixture();
            int endingCount = 0;
            fixture.EndController.MatchEndingStarted += _ =>
                endingCount++;

            fixture.Progress.Tick(0.5f);
            Assert.That(fixture.Progress.ProgressSeconds, Is.GreaterThan(0f));
            float remainingBeforeEnd =
                fixture.MatchRuntime.RemainingMatchSeconds;

            fixture.MatchRuntime.Tick(
                fixture.MatchConfig.MatchDurationSeconds);
            Assert.That(
                fixture.Evaluator.EvaluatePendingRequests(),
                Is.True);

            Assert.That(
                fixture.MatchRuntime.CurrentState,
                Is.EqualTo(MatchState.Ending));
            Assert.That(fixture.EndController.HasEnded, Is.True);
            Assert.That(
                fixture.EndController.FinalResult.Winner,
                Is.EqualTo(MatchWinner.Police));
            Assert.That(endingCount, Is.EqualTo(1));
            Assert.That(
                fixture.Selector.IsGameplayInputEnabled,
                Is.False);
            Assert.That(
                fixture.PoliceKeyboard.IsLocallyControlled,
                Is.False);
            Assert.That(
                fixture.ThiefKeyboard.IsLocallyControlled,
                Is.False);
            Assert.That(
                fixture.ThiefInteraction.IsLocallyControlled,
                Is.False);
            Assert.That(
                fixture.ThiefDropInput.IsLocallyControlled,
                Is.False);
            Assert.That(fixture.Progress.ProgressSeconds, Is.Zero);

            float remainingAfterEnd =
                fixture.MatchRuntime.RemainingMatchSeconds;
            fixture.MatchRuntime.Tick(10f);
            Assert.That(
                fixture.MatchRuntime.RemainingMatchSeconds,
                Is.EqualTo(remainingAfterEnd));
            Assert.That(remainingAfterEnd, Is.LessThan(remainingBeforeEnd));

            MatchResult duplicateResult = new(
                MatchWinner.Thief,
                MatchEndReason.SaleTargetReached,
                fixture.MatchConfig.TargetSaleAmount,
                remainingAfterEnd);
            Assert.That(
                fixture.EndController.TryEndMatch(duplicateResult),
                Is.False);
            Assert.That(endingCount, Is.EqualTo(1));
            Assert.That(
                fixture.EndController.FinalResult.Winner,
                Is.EqualTo(MatchWinner.Police));

            DestroyFixture(fixture);
            yield return null;
        }

        private static EndFixture CreateFixture()
        {
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            ArrestConfig arrestConfig =
                ScriptableObject.CreateInstance<ArrestConfig>();

            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState matchRuntime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            matchRuntime.Configure(matchConfig, false);

            GameObject police =
                CreatePlayer("Police", PlayerRole.Police);
            GameObject thief =
                CreatePlayer("Thief", PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            police.SetActive(false);
            thief.SetActive(false);

            PlayerRoleIdentity policeIdentity =
                police.GetComponent<PlayerRoleIdentity>();
            PlayerRoleIdentity thiefIdentity =
                thief.GetComponent<PlayerRoleIdentity>();
            PlayerKeyboardInput policeKeyboard =
                police.AddComponent<PlayerKeyboardInput>();
            PlayerKeyboardInput thiefKeyboard =
                thief.AddComponent<PlayerKeyboardInput>();
            PlayerInteractionInput thiefInteraction =
                thief.AddComponent<PlayerInteractionInput>();
            LootDropInput thiefDropInput =
                thief.AddComponent<LootDropInput>();

            ThiefLootWallet wallet =
                thief.AddComponent<ThiefLootWallet>();
            wallet.Configure(thiefIdentity, matchConfig);

            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                policeIdentity,
                thiefIdentity,
                arrestConfig,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, arrestConfig);
            ArrestCompletionController completion =
                police.AddComponent<ArrestCompletionController>();
            completion.Configure(progress, matchRuntime);

            PlayerRoleControlBinding policeBinding = new(
                policeIdentity,
                policeKeyboard);
            PlayerRoleControlBinding thiefBinding = new(
                thiefIdentity,
                thiefKeyboard,
                null,
                thiefInteraction,
                thiefDropInput);
            var selectorObject =
                new GameObject("Local Player Role Selector");
            selectorObject.SetActive(false);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(
                new List<PlayerRoleControlBinding>
                {
                    policeBinding,
                    thiefBinding
                },
                null,
                PlayerRole.Police);
            selector.SelectRole(PlayerRole.Police);

            MatchResultEvaluator evaluator =
                runtimeObject.AddComponent<MatchResultEvaluator>();
            evaluator.Configure(matchRuntime, wallet, completion);
            MatchEndController endController =
                runtimeObject.AddComponent<MatchEndController>();
            endController.Configure(
                matchRuntime,
                evaluator,
                selector,
                progress);

            thief.SetActive(true);
            police.SetActive(true);
            selectorObject.SetActive(true);
            runtimeObject.SetActive(true);
            matchRuntime.BeginCountdown();
            matchRuntime.Tick(matchConfig.ReadyCountdownSeconds);
            sensor.Evaluate();

            return new EndFixture(
                runtimeObject,
                selectorObject,
                police,
                thief,
                matchConfig,
                arrestConfig,
                matchRuntime,
                evaluator,
                endController,
                selector,
                progress,
                policeKeyboard,
                thiefKeyboard,
                thiefInteraction,
                thiefDropInput);
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

        private static void DestroyFixture(EndFixture fixture)
        {
            Object.Destroy(fixture.MatchConfig);
            Object.Destroy(fixture.ArrestConfig);
            Object.Destroy(fixture.RuntimeObject);
            Object.Destroy(fixture.SelectorObject);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
        }

        private readonly struct EndFixture
        {
            public EndFixture(
                GameObject runtimeObject,
                GameObject selectorObject,
                GameObject police,
                GameObject thief,
                MatchConfig matchConfig,
                ArrestConfig arrestConfig,
                MatchRuntimeState matchRuntime,
                MatchResultEvaluator evaluator,
                MatchEndController endController,
                LocalPlayerRoleSelector selector,
                ArrestProgressController progress,
                PlayerKeyboardInput policeKeyboard,
                PlayerKeyboardInput thiefKeyboard,
                PlayerInteractionInput thiefInteraction,
                LootDropInput thiefDropInput)
            {
                RuntimeObject = runtimeObject;
                SelectorObject = selectorObject;
                Police = police;
                Thief = thief;
                MatchConfig = matchConfig;
                ArrestConfig = arrestConfig;
                MatchRuntime = matchRuntime;
                Evaluator = evaluator;
                EndController = endController;
                Selector = selector;
                Progress = progress;
                PoliceKeyboard = policeKeyboard;
                ThiefKeyboard = thiefKeyboard;
                ThiefInteraction = thiefInteraction;
                ThiefDropInput = thiefDropInput;
            }

            public GameObject RuntimeObject { get; }
            public GameObject SelectorObject { get; }
            public GameObject Police { get; }
            public GameObject Thief { get; }
            public MatchConfig MatchConfig { get; }
            public ArrestConfig ArrestConfig { get; }
            public MatchRuntimeState MatchRuntime { get; }
            public MatchResultEvaluator Evaluator { get; }
            public MatchEndController EndController { get; }
            public LocalPlayerRoleSelector Selector { get; }
            public ArrestProgressController Progress { get; }
            public PlayerKeyboardInput PoliceKeyboard { get; }
            public PlayerKeyboardInput ThiefKeyboard { get; }
            public PlayerInteractionInput ThiefInteraction { get; }
            public LootDropInput ThiefDropInput { get; }
        }
    }
}
