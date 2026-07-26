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
    /// <summary>
    /// ARREST-006. However many times an arrest completion is requested, and by
    /// however many callers, the match result must be decided once.
    ///
    /// These are rule-layer properties, so they hold regardless of whether the
    /// requests arrive from one local Update loop or from several networked
    /// clients. Proving them here is what lets NET-007 forward duplicate
    /// requests without special casing.
    /// </summary>
    public sealed class ArrestIdempotencyPlayModeTests
    {
        [Test]
        public void ArbiterDecidesOnceNoMatterHowManyArrestRequestsArrive()
        {
            var arbiter = new MatchResultArbiter();

            // Ten callers all claim the arrest completed.
            int accepted = 0;
            for (int index = 0; index < 10; index++)
            {
                if (arbiter.RequestArrest())
                {
                    accepted++;
                }
            }

            Assert.That(
                arbiter.TryResolve(0, 1000, 42f, out MatchResult first),
                Is.True);
            Assert.That(first.Winner, Is.EqualTo(MatchWinner.Police));
            Assert.That(
                first.Reason,
                Is.EqualTo(MatchEndReason.ThiefArrested));

            // Every later resolve returns the same stored result and reports
            // that nothing new was decided.
            for (int index = 0; index < 5; index++)
            {
                Assert.That(
                    arbiter.TryResolve(
                        5000,
                        1000,
                        10f,
                        out MatchResult again),
                    Is.False);
                Assert.That(again.Winner, Is.EqualTo(first.Winner));
                Assert.That(again.Reason, Is.EqualTo(first.Reason));
            }

            Assert.That(accepted, Is.GreaterThan(0));
        }

        [Test]
        public void SimultaneousArrestAndSaleResolveToASingleResult()
        {
            var arbiter = new MatchResultArbiter();

            // Both conditions land in the same evaluation window and the sale
            // has already met the target.
            arbiter.RequestArrest();
            arbiter.RequestSaleCheck();
            arbiter.RequestTimeout();

            Assert.That(
                arbiter.TryResolve(1500, 1000, 0f, out MatchResult result),
                Is.True);
            Assert.That(
                result.Winner,
                Is.EqualTo(MatchWinner.Police),
                "DEC-025 puts arrest ahead of a target sale.");
            Assert.That(
                arbiter.TryResolve(1500, 1000, 0f, out _),
                Is.False);
        }

        [UnityTest]
        public IEnumerator CompletionControllerFiresVictoryExactlyOnce()
        {
            var state = new GameObject("Match Runtime");
            state.SetActive(false);
            MatchConfig matchConfig =
                ScriptableObject.CreateInstance<MatchConfig>();
            MatchRuntimeState runtime =
                state.AddComponent<MatchRuntimeState>();
            runtime.Configure(matchConfig, false);
            state.SetActive(true);
            Assert.That(runtime.BeginCountdown(), Is.True);
            runtime.Tick(matchConfig.ReadyCountdownSeconds);
            Assert.That(
                runtime.CurrentState,
                Is.EqualTo(MatchState.Playing));

            ArrestConfig arrestConfig =
                ScriptableObject.CreateInstance<ArrestConfig>();
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0.4f, 0f, 0f));

            var sensorObject = new GameObject("Sensor");
            sensorObject.SetActive(false);
            ArrestRangeSensor sensor =
                sensorObject.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police,
                thief,
                arrestConfig,
                Physics.AllLayers);
            sensorObject.SetActive(true);
            sensor.Evaluate();
            Assert.That(sensor.IsTargetDetected, Is.True);

            var progressObject = new GameObject("Progress");
            progressObject.SetActive(false);
            ArrestProgressController progress =
                progressObject.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, runtime, arrestConfig);
            progressObject.SetActive(true);
            progress.Tick(arrestConfig.ArrestDurationSeconds + 0.5f);
            Assert.That(progress.IsReadyToComplete, Is.True);

            var completionObject = new GameObject("Completion");
            completionObject.SetActive(false);
            ArrestCompletionController completion =
                completionObject.AddComponent<
                    ArrestCompletionController>();
            completion.Configure(progress, runtime);
            completionObject.SetActive(true);

            int victoryRequests = 0;
            completion.PoliceVictoryRequested += () => victoryRequests++;

            // Many callers, one effect.
            int succeeded = 0;
            for (int index = 0; index < 8; index++)
            {
                if (completion.TryCompleteArrest())
                {
                    succeeded++;
                }
            }

            Assert.That(succeeded, Is.EqualTo(1));
            Assert.That(victoryRequests, Is.EqualTo(1));
            Assert.That(completion.IsCompleted, Is.True);

            Object.DestroyImmediate(completionObject);
            Object.DestroyImmediate(progressObject);
            Object.DestroyImmediate(sensorObject);
            Object.DestroyImmediate(police.gameObject);
            Object.DestroyImmediate(thief.gameObject);
            Object.DestroyImmediate(state);
            Object.DestroyImmediate(arrestConfig);
            Object.DestroyImmediate(matchConfig);
            yield return null;
        }

        [Test]
        public void ResultSessionKeepsOnlyTheFirstStoredResult()
        {
            MatchResultSession.Clear();
            var police = new MatchResult(
                MatchWinner.Police,
                MatchEndReason.ThiefArrested,
                0,
                30f);
            var thief = new MatchResult(
                MatchWinner.Thief,
                MatchEndReason.SaleTargetReached,
                1000,
                5f);

            Assert.That(MatchResultSession.TryStore(police), Is.True);
            // A late duplicate, such as a second client reporting the same
            // arrest, must not overwrite the decided result.
            Assert.That(MatchResultSession.TryStore(thief), Is.False);
            Assert.That(MatchResultSession.TryStore(police), Is.False);

            Assert.That(
                MatchResultSession.TryGet(out MatchResult stored),
                Is.True);
            Assert.That(stored.Winner, Is.EqualTo(MatchWinner.Police));
            MatchResultSession.Clear();
        }

        private static PlayerRoleIdentity CreatePlayer(
            PlayerRole role,
            Vector3 position)
        {
            var player = new GameObject($"{role}");
            player.transform.position = position;
            CapsuleCollider collider =
                player.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.45f;
            collider.center = new Vector3(0f, 1f, 0f);
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            return identity;
        }
    }
}
