using System.Collections;
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
    public sealed class MatchResultEvaluatorPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimerEventDecidesOnePoliceResult()
        {
            ResultFixture fixture = CreateFixture();
            int resultCount = 0;
            MatchResult decidedResult = default;
            fixture.Evaluator.ResultDecided += result =>
            {
                resultCount++;
                decidedResult = result;
            };

            fixture.MatchRuntime.Tick(
                fixture.MatchConfig.MatchDurationSeconds);
            Assert.That(
                fixture.Evaluator.EvaluatePendingRequests(),
                Is.True);
            Assert.That(resultCount, Is.EqualTo(1));
            Assert.That(
                decidedResult.Winner,
                Is.EqualTo(MatchWinner.Police));
            Assert.That(
                decidedResult.Reason,
                Is.EqualTo(
                    MatchEndReason.TimeExpiredBelowTarget));
            Assert.That(
                fixture.MatchRuntime.CurrentState,
                Is.EqualTo(MatchState.Playing));

            Assert.That(
                fixture.Evaluator.EvaluatePendingRequests(),
                Is.False);
            fixture.MatchRuntime.Tick(10f);
            Assert.That(resultCount, Is.EqualTo(1));

            DestroyFixture(fixture);
            yield return null;
        }

        /// <summary>
        /// The counters the result screen reports have to come from the real
        /// wallet, arrest controller and clock, and they have to be read at the
        /// instant the match ends. A moment later the match scene unloads and
        /// every one of them reads zero — which is how the old screen ended up
        /// showing numbers painted into a picture instead.
        /// </summary>
        [UnityTest]
        public IEnumerator DecidingReportsTheCountersTheResultScreenNeeds()
        {
            MatchResultSession.Clear();
            ResultFixture fixture = CreateFixture();

            fixture.MatchRuntime.Tick(
                fixture.MatchConfig.MatchDurationSeconds);
            Assert.That(
                fixture.Evaluator.EvaluatePendingRequests(),
                Is.True);
            MatchResultSession.TryStore(fixture.Evaluator.CurrentResult);

            Assert.That(
                MatchResultSession.TryGetSummary(out MatchSummary summary),
                Is.True);
            Assert.That(
                summary.IsReported,
                Is.True,
                "The evaluator decided without reporting any counters, so the "
                + "result screen would have nothing to show.");
            Assert.That(
                summary.ElapsedSeconds,
                Is.EqualTo(fixture.MatchConfig.MatchDurationSeconds)
                    .Within(0.01f),
                "Elapsed time must be how long the match ran, not how long "
                + "was left.");
            Assert.That(
                summary.TargetAmount,
                Is.GreaterThan(0),
                "The gold target came from the wallet, so it cannot be zero.");
            Assert.That(
                summary.RequiredCatchCount,
                Is.GreaterThan(0),
                "The arrest requirement came from the arrest controller, so "
                + "it cannot be zero.");

            DestroyFixture(fixture);
            MatchResultSession.Clear();
            yield return null;
        }

        private static ResultFixture CreateFixture()
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

            GameObject police = CreatePlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreatePlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            thief.SetActive(false);
            ThiefLootWallet wallet =
                thief.AddComponent<ThiefLootWallet>();
            wallet.Configure(
                thief.GetComponent<PlayerRoleIdentity>(),
                matchConfig);

            police.SetActive(false);
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police.GetComponent<PlayerRoleIdentity>(),
                thief.GetComponent<PlayerRoleIdentity>(),
                arrestConfig,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, arrestConfig);
            ArrestCompletionController completion =
                police.AddComponent<ArrestCompletionController>();
            completion.Configure(progress, matchRuntime);

            MatchResultEvaluator evaluator =
                runtimeObject.AddComponent<MatchResultEvaluator>();
            evaluator.Configure(matchRuntime, wallet, completion);
            thief.SetActive(true);
            police.SetActive(true);
            runtimeObject.SetActive(true);
            matchRuntime.BeginCountdown();
            matchRuntime.Tick(matchConfig.ReadyCountdownSeconds);

            return new ResultFixture(
                runtimeObject,
                police,
                thief,
                matchConfig,
                arrestConfig,
                matchRuntime,
                evaluator);
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

        private static void DestroyFixture(ResultFixture fixture)
        {
            Object.Destroy(fixture.MatchConfig);
            Object.Destroy(fixture.ArrestConfig);
            Object.Destroy(fixture.MatchRuntimeObject);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
        }

        private readonly struct ResultFixture
        {
            public ResultFixture(
                GameObject matchRuntimeObject,
                GameObject police,
                GameObject thief,
                MatchConfig matchConfig,
                ArrestConfig arrestConfig,
                MatchRuntimeState matchRuntime,
                MatchResultEvaluator evaluator)
            {
                MatchRuntimeObject = matchRuntimeObject;
                Police = police;
                Thief = thief;
                MatchConfig = matchConfig;
                ArrestConfig = arrestConfig;
                MatchRuntime = matchRuntime;
                Evaluator = evaluator;
            }

            public GameObject MatchRuntimeObject { get; }
            public GameObject Police { get; }
            public GameObject Thief { get; }
            public MatchConfig MatchConfig { get; }
            public ArrestConfig ArrestConfig { get; }
            public MatchRuntimeState MatchRuntime { get; }
            public MatchResultEvaluator Evaluator { get; }
        }
    }
}
