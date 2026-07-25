using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class MatchResultFlowPlayModeTests
    {
        [UnityTest]
        public IEnumerator CompletedMatchOpensPopulatedResultScene()
        {
            MatchResultSession.Clear();
            SceneManager.LoadScene(
                GameSceneCatalog.GetName(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            MatchRuntimeState runtime =
                Object.FindFirstObjectByType<MatchRuntimeState>();
            MatchEndController endController =
                Object.FindFirstObjectByType<MatchEndController>();
            MatchResultFlowController resultFlow =
                Object.FindFirstObjectByType<
                    MatchResultFlowController>();
            Assert.That(runtime, Is.Not.Null);
            Assert.That(endController, Is.Not.Null);
            Assert.That(resultFlow, Is.Not.Null);

            runtime.Tick(5f);
            Assert.That(
                runtime.CurrentState,
                Is.EqualTo(MatchState.Playing));
            MatchResult result = new(
                MatchWinner.Police,
                MatchEndReason.ThiefArrested,
                350,
                42f);

            Assert.That(endController.TryEndMatch(result), Is.True);
            yield return null;

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(
                    GameSceneCatalog.GetName(GameSceneId.Result)));
            ResultScreenPresenter presenter =
                Object.FindFirstObjectByType<
                    ResultScreenPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.WinnerText, Is.EqualTo("POLICE WIN"));
            Assert.That(
                presenter.ReasonText,
                Is.EqualTo("THIEF ARRESTED"));
            Assert.That(
                presenter.SoldAmountText,
                Is.EqualTo("SOLD 350 GOLD"));
            Assert.That(
                presenter.RemainingTimeText,
                Is.EqualTo("TIME 00:42"));

            MatchResultSession.Clear();
        }
    }
}
