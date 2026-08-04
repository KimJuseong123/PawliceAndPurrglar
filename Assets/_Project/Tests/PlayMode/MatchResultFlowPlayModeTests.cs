using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class MatchResultFlowPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        [UnityTest]
        public IEnumerator CompletedMatchOpensPopulatedResultScene()
        {
            MatchResultSession.Clear();

            // Whose screen the result will be. Set before the match so the
            // result screen reads it the same way a lobby-committed role would,
            // and stated here because this test asserts a viewpoint.
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
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
            // The winner is announced by artwork, so the badges are what say
            // which side won in text.
            Assert.That(presenter.PoliceBadgeText, Is.EqualTo("승리"));
            Assert.That(presenter.ThiefBadgeText, Is.EqualTo("패배"));
            Assert.That(
                presenter.IsShowingWinTitle,
                Is.True,
                "The police won and the local role is the police, so the title "
                + "has to be the winning one.");
            // No summary was reported, so the screen shows the gold the verdict
            // carries and declines to invent the rest.
            Assert.That(presenter.GoldValueText, Is.EqualTo("350"));
            Assert.That(presenter.ElapsedText, Is.EqualTo("--:--"));

            MatchResultSession.Clear();
        }
    }
}
