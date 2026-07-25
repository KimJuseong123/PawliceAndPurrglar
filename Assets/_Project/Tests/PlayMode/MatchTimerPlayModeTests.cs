using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class MatchTimerPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimerRunsOnlyDuringPlayingAndStopsAtZero()
        {
            MatchConfig config =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState runtime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(config, false);
            runtimeObject.SetActive(true);

            float duration = config.MatchDurationSeconds;
            runtime.Tick(10f);
            Assert.That(
                runtime.RemainingMatchSeconds,
                Is.EqualTo(duration));

            Assert.That(runtime.BeginCountdown(), Is.True);
            runtime.Tick(config.ReadyCountdownSeconds);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Playing));
            Assert.That(
                runtime.RemainingMatchSeconds,
                Is.EqualTo(duration));

            runtime.Tick(10f);
            Assert.That(
                runtime.RemainingMatchSeconds,
                Is.EqualTo(duration - 10f));
            Assert.That(runtime.ResetMatchTimer(), Is.False);

            runtime.Tick(duration);
            Assert.That(runtime.RemainingMatchSeconds, Is.EqualTo(0f));
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Ending));

            runtime.Tick(20f);
            Assert.That(runtime.RemainingMatchSeconds, Is.EqualTo(0f));
            Assert.That(runtime.ResetMatchTimer(), Is.True);
            Assert.That(
                runtime.RemainingMatchSeconds,
                Is.EqualTo(duration));

            Object.Destroy(runtimeObject);
            Object.Destroy(config);
            yield return null;
        }
    }
}
