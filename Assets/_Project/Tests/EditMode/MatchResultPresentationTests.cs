using NUnit.Framework;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class MatchResultPresentationTests
    {
        [SetUp]
        public void SetUp()
        {
            MatchResultSession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            MatchResultSession.Clear();
        }

        [Test]
        public void SessionKeepsFirstResultUntilCleared()
        {
            MatchResult first = new(
                MatchWinner.Police,
                MatchEndReason.ThiefArrested,
                350,
                65.1f);
            MatchResult second = new(
                MatchWinner.Thief,
                MatchEndReason.SaleTargetReached,
                1000,
                10f);

            Assert.That(MatchResultSession.TryStore(first), Is.True);
            Assert.That(MatchResultSession.TryStore(second), Is.False);
            Assert.That(
                MatchResultSession.CurrentResult.Winner,
                Is.EqualTo(MatchWinner.Police));

            MatchResultSession.Clear();
            Assert.That(MatchResultSession.HasResult, Is.False);
            Assert.Throws<System.InvalidOperationException>(
                () => _ = MatchResultSession.CurrentResult);
        }

        [TestCase(
            MatchWinner.Police,
            MatchEndReason.ThiefArrested,
            "POLICE WIN",
            "THIEF ARRESTED")]
        [TestCase(
            MatchWinner.Police,
            MatchEndReason.TimeExpiredBelowTarget,
            "POLICE WIN",
            "TIME EXPIRED / TARGET NOT REACHED")]
        [TestCase(
            MatchWinner.Thief,
            MatchEndReason.SaleTargetReached,
            "THIEF WIN",
            "SALE TARGET REACHED")]
        public void PresenterDisplaysCompletedResult(
            MatchWinner winner,
            MatchEndReason reason,
            string expectedWinner,
            string expectedReason)
        {
            MatchResultSession.TryStore(
                new MatchResult(winner, reason, 350, 65.1f));
            PresenterFixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.WinnerText,
                Is.EqualTo(expectedWinner));
            Assert.That(
                fixture.Presenter.ReasonText,
                Is.EqualTo(expectedReason));
            Assert.That(
                fixture.Presenter.SoldAmountText,
                Is.EqualTo("SOLD 350 GOLD"));
            Assert.That(
                fixture.Presenter.RemainingTimeText,
                Is.EqualTo("TIME 01:06"));

            Object.DestroyImmediate(fixture.Root);
        }

        [Test]
        public void PresenterUsesSafeFallbackWithoutResult()
        {
            PresenterFixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.WinnerText,
                Is.EqualTo("NO MATCH RESULT"));
            Assert.That(
                fixture.Presenter.ReasonText,
                Is.EqualTo("PLAY A MATCH TO VIEW THE RESULT"));

            Object.DestroyImmediate(fixture.Root);
        }

        private static PresenterFixture CreatePresenter()
        {
            var root = new GameObject("Result Presenter");
            root.SetActive(false);
            ResultScreenPresenter presenter =
                root.AddComponent<ResultScreenPresenter>();
            Text winner = CreateText("Winner", root.transform);
            Text reason = CreateText("Reason", root.transform);
            Text sold = CreateText("Sold", root.transform);
            Text time = CreateText("Time", root.transform);
            presenter.Configure(winner, reason, sold, time);
            return new PresenterFixture(root, presenter);
        }

        private static Text CreateText(
            string name,
            Transform parent)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Text));
            child.transform.SetParent(parent);
            return child.GetComponent<Text>();
        }

        private readonly struct PresenterFixture
        {
            public PresenterFixture(
                GameObject root,
                ResultScreenPresenter presenter)
            {
                Root = root;
                Presenter = presenter;
            }

            public GameObject Root { get; }
            public ResultScreenPresenter Presenter { get; }
        }
    }
}
