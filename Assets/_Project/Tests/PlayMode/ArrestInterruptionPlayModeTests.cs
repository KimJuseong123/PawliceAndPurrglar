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
    public sealed class ArrestInterruptionPlayModeTests
    {
        [UnityTest]
        public IEnumerator RangeExitAndObstacleResetProgressOnce()
        {
            ArrestFixture fixture = CreateFixture();
            int interruptionCount = 0;
            ArrestInterruptionReason lastReason = default;
            fixture.Progress.ProgressInterrupted += reason =>
            {
                interruptionCount++;
                lastReason = reason;
            };

            fixture.Progress.Tick(0.5f);
            fixture.Thief.transform.position = new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);
            Assert.That(interruptionCount, Is.EqualTo(1));
            Assert.That(
                lastReason,
                Is.EqualTo(
                    ArrestInterruptionReason.TargetNoLongerDetectable));

            fixture.Sensor.Evaluate();
            fixture.Progress.Tick(0.5f);
            Assert.That(interruptionCount, Is.EqualTo(1));

            fixture.Thief.transform.position = new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Progress.Tick(0.5f);

            GameObject wall = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.5f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.2f, 3f, 2f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);
            Assert.That(interruptionCount, Is.EqualTo(2));

            Object.Destroy(wall);
            DestroyFixture(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InactiveParticipantResetsProgress()
        {
            ArrestFixture fixture = CreateFixture();
            ArrestInterruptionReason? reason = null;
            fixture.Progress.ProgressInterrupted += value => reason = value;
            fixture.Progress.Tick(0.5f);

            fixture.Thief.SetActive(false);
            fixture.Sensor.Evaluate();

            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);
            Assert.That(
                reason,
                Is.EqualTo(
                    ArrestInterruptionReason.ParticipantUnavailable));

            DestroyFixture(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MatchChangeAndPoliceDisableResetProgress()
        {
            ArrestFixture fixture = CreateFixture();
            int interruptionCount = 0;
            ArrestInterruptionReason lastReason = default;
            fixture.Progress.ProgressInterrupted += value =>
            {
                interruptionCount++;
                lastReason = value;
            };

            fixture.Progress.Tick(0.5f);
            fixture.MatchState.IsGameplayActive = false;
            fixture.Progress.Tick(0f);
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);
            Assert.That(
                lastReason,
                Is.EqualTo(ArrestInterruptionReason.MatchNotPlaying));

            fixture.MatchState.IsGameplayActive = true;
            fixture.Progress.Tick(0.5f);
            fixture.Police.SetActive(false);
            Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);
            Assert.That(interruptionCount, Is.EqualTo(2));
            Assert.That(
                lastReason,
                Is.EqualTo(
                    ArrestInterruptionReason.ParticipantUnavailable));

            DestroyFixture(fixture);
            yield return null;
        }

        private static ArrestFixture CreateFixture()
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
                IsGameplayActive = true
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
