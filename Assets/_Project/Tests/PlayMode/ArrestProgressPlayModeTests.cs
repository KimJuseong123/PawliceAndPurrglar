using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class ArrestProgressPlayModeTests
    {
        [UnityTest]
        public IEnumerator ProgressUsesDeltaTimeAndClampsToOne()
        {
            ArrestFixture fixture = CreateFixture(true);

            fixture.Progress.Tick(0.25f);
            Assert.That(
                fixture.Progress.ProgressSeconds,
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                fixture.Progress.ProgressNormalized,
                Is.EqualTo(1f / 6f).Within(0.0001f));
            Assert.That(fixture.Progress.IsProgressing, Is.True);

            fixture.Progress.Tick(5f);
            Assert.That(
                fixture.Progress.ProgressSeconds,
                Is.EqualTo(fixture.Config.ArrestDurationSeconds));
            Assert.That(fixture.Progress.ProgressNormalized, Is.EqualTo(1f));
            Assert.That(fixture.Progress.IsReadyToComplete, Is.True);
            Assert.That(fixture.Progress.IsProgressing, Is.False);

            fixture.Progress.Tick(1f);
            Assert.That(
                fixture.Progress.ProgressSeconds,
                Is.EqualTo(fixture.Config.ArrestDurationSeconds));

            DestroyFixture(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProgressRequiresPlayingAndDetectedTarget()
        {
            ArrestFixture fixture = CreateFixture(false);

            fixture.Progress.Tick(1f);
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);

            fixture.MatchState.IsGameplayActive = true;
            fixture.Thief.transform.position = new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Progress.Tick(1f);
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);

            fixture.Thief.transform.position = new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Progress.Tick(-1f);
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);

            fixture.Progress.Tick(0.75f);
            Assert.That(
                fixture.Progress.ProgressNormalized,
                Is.EqualTo(0.5f).Within(0.0001f));

            DestroyFixture(fixture);
            yield return null;
        }

        private static ArrestFixture CreateFixture(
            bool gameplayActive)
        {
            GameObject police = CreatePlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreatePlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            ArrestConfig config =
                ScriptableObject.CreateInstance<ArrestConfig>();
            var state = new FakeMatchState
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

            return new ArrestFixture(
                police,
                thief,
                config,
                state,
                sensor,
                progress);
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

        private static void DestroyFixture(ArrestFixture fixture)
        {
            Object.Destroy(fixture.Config);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
        }

        private sealed class FakeMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }

        private readonly struct ArrestFixture
        {
            public ArrestFixture(
                GameObject police,
                GameObject thief,
                ArrestConfig config,
                FakeMatchState matchState,
                ArrestRangeSensor sensor,
                ArrestProgressController progress)
            {
                Police = police;
                Thief = thief;
                Config = config;
                MatchState = matchState;
                Sensor = sensor;
                Progress = progress;
            }

            public GameObject Police { get; }
            public GameObject Thief { get; }
            public ArrestConfig Config { get; }
            public FakeMatchState MatchState { get; }
            public ArrestRangeSensor Sensor { get; }
            public ArrestProgressController Progress { get; }
        }
    }
}
