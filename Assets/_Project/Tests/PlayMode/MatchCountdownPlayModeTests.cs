using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class MatchCountdownPlayModeTests
    {
        [UnityTest]
        public IEnumerator CountdownBlocksGameplayAndStartsExactlyOnce()
        {
            MatchConfig config =
                ScriptableObject.CreateInstance<MatchConfig>();
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.SetActive(false);
            MatchRuntimeState runtime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(config, false);
            runtimeObject.SetActive(true);

            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Lobby));
            Assert.That(runtime.BeginCountdown(), Is.True);
            Assert.That(runtime.BeginCountdown(), Is.False);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Ready));
            Assert.That(runtime.IsGameplayActive, Is.False);
            Assert.That(
                runtime.ReadyCountdownRemainingSeconds,
                Is.EqualTo(config.ReadyCountdownSeconds));

            runtime.Tick(config.ReadyCountdownSeconds - 0.1f);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Ready));
            Assert.That(runtime.IsGameplayActive, Is.False);

            runtime.Tick(0.1f);
            Assert.That(runtime.CurrentState, Is.EqualTo(MatchState.Playing));
            Assert.That(runtime.IsGameplayActive, Is.True);
            Assert.That(runtime.IsCountdownActive, Is.False);
            Assert.That(runtime.BeginCountdown(), Is.False);

            Object.Destroy(runtimeObject);
            Object.Destroy(config);
            yield return null;
        }
    }
}
