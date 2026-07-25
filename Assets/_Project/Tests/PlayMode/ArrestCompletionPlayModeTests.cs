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
    public sealed class ArrestCompletionPlayModeTests
    {
        [UnityTest]
        public IEnumerator CompletionFiresOnceAndRequestsVictory()
        {
            ArrestFixture fixture = CreateFixture();
            int completed = 0;
            int victoryRequests = 0;
            fixture.Completion.ArrestCompleted += () => completed++;
            fixture.Completion.PoliceVictoryRequested +=
                () => victoryRequests++;

            fixture.Scanner.RefreshTarget();
            Assert.That(fixture.Scanner.HasTarget, Is.True);
            Assert.That(fixture.Movement.CanMove, Is.True);

            fixture.Progress.Tick(
                fixture.ArrestConfig.ArrestDurationSeconds);
            Assert.That(fixture.Completion.TryCompleteArrest(), Is.True);

            Assert.That(fixture.Completion.IsCompleted, Is.True);
            Assert.That(fixture.Progress.IsCompleted, Is.True);
            Assert.That(fixture.Progress.ProgressNormalized, Is.EqualTo(1f));
            Assert.That(
                fixture.MatchRuntime.CurrentState,
                Is.EqualTo(MatchState.Playing));
            Assert.That(fixture.MatchRuntime.IsGameplayActive, Is.True);
            Assert.That(fixture.Movement.CanMove, Is.True);
            fixture.Scanner.RefreshTarget();
            Assert.That(fixture.Scanner.HasTarget, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(victoryRequests, Is.EqualTo(1));

            Assert.That(fixture.Completion.TryCompleteArrest(), Is.False);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(victoryRequests, Is.EqualTo(1));

            DestroyFixture(fixture);
            yield return null;
        }

        private static ArrestFixture CreateFixture()
        {
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState matchRuntime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            matchRuntime.Configure(matchConfig, false);
            runtimeObject.SetActive(true);
            Assert.That(matchRuntime.BeginCountdown(), Is.True);
            matchRuntime.Tick(matchConfig.ReadyCountdownSeconds);

            GameObject police = CreatePlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreatePlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(1f, 0f, 0f);
            ArrestConfig arrestConfig =
                ScriptableObject.CreateInstance<ArrestConfig>();
            PlayerConfig playerConfig =
                ScriptableObject.CreateInstance<PlayerConfig>();

            police.SetActive(false);
            PlayerRoleIdentity policeIdentity =
                police.GetComponent<PlayerRoleIdentity>();
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                policeIdentity,
                thief.GetComponent<PlayerRoleIdentity>(),
                arrestConfig,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, arrestConfig);
            ArrestCompletionController completion =
                police.AddComponent<ArrestCompletionController>();
            completion.Configure(progress, matchRuntime);
            PlayerMovementMotor movement =
                police.AddComponent<PlayerMovementMotor>();
            movement.Configure(
                police.GetComponent<CharacterController>(),
                playerConfig,
                matchRuntime,
                null);
            PlayerInteractionScanner scanner =
                police.AddComponent<PlayerInteractionScanner>();
            scanner.Configure(policeIdentity, playerConfig, matchRuntime);
            police.SetActive(true);

            GameObject targetObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "Police Interaction Target";
            targetObject.transform.position = new Vector3(0f, 0f, 1f);
            PrototypeInteractable target =
                targetObject.AddComponent<PrototypeInteractable>();
            target.Configure(
                PlayerInteractionType.Generic,
                "Inspect");
            Physics.SyncTransforms();
            sensor.Evaluate();

            return new ArrestFixture(
                runtimeObject,
                police,
                thief,
                targetObject,
                matchConfig,
                arrestConfig,
                playerConfig,
                matchRuntime,
                progress,
                completion,
                movement,
                scanner);
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
            Object.Destroy(fixture.MatchConfig);
            Object.Destroy(fixture.ArrestConfig);
            Object.Destroy(fixture.PlayerConfig);
            Object.Destroy(fixture.MatchRuntimeObject);
            Object.Destroy(fixture.Police);
            Object.Destroy(fixture.Thief);
            Object.Destroy(fixture.Target);
        }

        private readonly struct ArrestFixture
        {
            public ArrestFixture(
                GameObject matchRuntimeObject,
                GameObject police,
                GameObject thief,
                GameObject target,
                MatchConfig matchConfig,
                ArrestConfig arrestConfig,
                PlayerConfig playerConfig,
                MatchRuntimeState matchRuntime,
                ArrestProgressController progress,
                ArrestCompletionController completion,
                PlayerMovementMotor movement,
                PlayerInteractionScanner scanner)
            {
                MatchRuntimeObject = matchRuntimeObject;
                Police = police;
                Thief = thief;
                Target = target;
                MatchConfig = matchConfig;
                ArrestConfig = arrestConfig;
                PlayerConfig = playerConfig;
                MatchRuntime = matchRuntime;
                Progress = progress;
                Completion = completion;
                Movement = movement;
                Scanner = scanner;
            }

            public GameObject MatchRuntimeObject { get; }
            public GameObject Police { get; }
            public GameObject Thief { get; }
            public GameObject Target { get; }
            public MatchConfig MatchConfig { get; }
            public ArrestConfig ArrestConfig { get; }
            public PlayerConfig PlayerConfig { get; }
            public MatchRuntimeState MatchRuntime { get; }
            public ArrestProgressController Progress { get; }
            public ArrestCompletionController Completion { get; }
            public PlayerMovementMotor Movement { get; }
            public PlayerInteractionScanner Scanner { get; }
        }
    }
}
