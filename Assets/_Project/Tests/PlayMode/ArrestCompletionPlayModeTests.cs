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
        /// <summary>
        /// Three catches, three requests, and the arbiter is the one that
        /// decides.
        ///
        /// This used to assert that the controller sent one request, on the
        /// third catch. It reads as the careful thing to do and it broke the
        /// officer entirely: the arbiter counts the requests it is sent and
        /// needs three of them, so forwarding only the third meant three
        /// catches produced one request, and winning would have taken nine.
        /// A two-process run measured it — the thief was jailed four times, two
        /// arrests were counted, and the match was declared undecided.
        ///
        /// Counting in two places is the same shape as the bug that had the
        /// host and the client disagreeing about who had won (`ISSUE-046`).
        /// The tally here is for the screen. The tally that ends a match lives
        /// in one place, and it is not this one.
        /// </summary>
        [UnityTest]
        public IEnumerator CompletionCountsCatchesAndForwardsEveryOne()
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
            Assert.That(fixture.Completion.RequiredCatchCount, Is.EqualTo(3));

            for (int catches = 1; catches <= 3; catches++)
            {
                fixture.Progress.Tick(
                    fixture.ArrestConfig.ArrestDurationSeconds);
                Assert.That(
                    fixture.Completion.TryCompleteArrest(),
                    Is.True,
                    $"Catch {catches} never landed.");

                Assert.That(
                    fixture.Completion.CurrentCatchCount,
                    Is.EqualTo(catches));
                Assert.That(completed, Is.EqualTo(catches));
                Assert.That(
                    victoryRequests,
                    Is.EqualTo(catches),
                    "Every catch has to reach the arbiter, or it can never "
                    + "count to three.");

                // Latched after every catch, not only the last. Left unlatched
                // the officer standing on the thief would catch them again on
                // the very next frame.
                Assert.That(fixture.Completion.IsCompleted, Is.True);
                Assert.That(
                    fixture.Completion.TryCompleteArrest(),
                    Is.False,
                    "A latched catch should not land twice.");
                Assert.That(completed, Is.EqualTo(catches));
                Assert.That(victoryRequests, Is.EqualTo(catches));

                // What the jail does when it lets the thief out.
                fixture.Completion.ClearForNextArrest();
                Assert.That(fixture.Completion.IsCompleted, Is.False);
                Assert.That(fixture.Progress.IsCompleted, Is.False);
                Assert.That(fixture.Progress.ProgressNormalized, Is.Zero);

                Assert.That(
                    fixture.MatchRuntime.CurrentState,
                    Is.EqualTo(MatchState.Playing));
                fixture.Scanner.RefreshTarget();
                Assert.That(fixture.Scanner.HasTarget, Is.True);
            }

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

        /// <summary>
        /// Takes the fixture apart at once rather than at the end of the frame.
        ///
        /// `Object.Destroy` is deferred, and a test that ends right after
        /// calling it hands the next test a world that still contains this
        /// one's officer and thief. The next test builds its own pair, the
        /// range sensor finds the wrong one, and eight tests across arrest,
        /// pickups and the HUD fail in a full run while every one of them
        /// passes on its own. That is what this looked like.
        /// </summary>
        private static void DestroyFixture(ArrestFixture fixture)
        {
            Object.DestroyImmediate(fixture.MatchConfig);
            Object.DestroyImmediate(fixture.ArrestConfig);
            Object.DestroyImmediate(fixture.PlayerConfig);
            Object.DestroyImmediate(fixture.MatchRuntimeObject);
            Object.DestroyImmediate(fixture.Police);
            Object.DestroyImmediate(fixture.Thief);
            Object.DestroyImmediate(fixture.Target);
            Physics.SyncTransforms();
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
