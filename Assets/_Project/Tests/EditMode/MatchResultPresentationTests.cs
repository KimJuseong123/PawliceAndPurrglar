using NUnit.Framework;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The result screen has to report the match that was actually played.
    ///
    /// Its previous version could not: the four labels were built at font size
    /// one, fully transparent and switched off, and the numbers a player read
    /// were painted into the background image. Every case here asserts on the
    /// text a label ends up holding.
    /// </summary>
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

        [Test]
        public void SummaryIsClearedWithTheResult()
        {
            MatchResultSession.ReportSummary(
                new MatchSummary(90f, 2, 3, 4, 500, 1000));
            MatchResultSession.TryStore(
                new MatchResult(
                    MatchWinner.Police,
                    MatchEndReason.ThiefArrested,
                    500,
                    150f));

            Assert.That(
                MatchResultSession.TryGetSummary(out MatchSummary stored),
                Is.True);
            Assert.That(stored.CatchCount, Is.EqualTo(2));

            MatchResultSession.Clear();
            Assert.That(
                MatchResultSession.TryGetSummary(out _),
                Is.False,
                "A cleared session must not hand back the previous match's "
                + "counters.");
        }

        [Test]
        public void PoliceWinReportsTheArrestsThatEndedIt()
        {
            MatchResultSession.ReportSummary(
                new MatchSummary(211f, 3, 3, 5, 820, 1000));
            MatchResultSession.TryStore(
                new MatchResult(
                    MatchWinner.Police,
                    MatchEndReason.ThiefArrested,
                    820,
                    29f));
            Fixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.ReasonText,
                Is.EqualTo("도둑을 3번 체포했습니다"));
            Assert.That(fixture.Presenter.ElapsedText, Is.EqualTo("03:31"));
            Assert.That(
                fixture.Presenter.MiddleCaptionText,
                Is.EqualTo("경찰 체포"));
            Assert.That(fixture.Presenter.MiddleValueText, Is.EqualTo("3 / 3"));
            Assert.That(
                fixture.Presenter.GoldCaptionText,
                Is.EqualTo("도둑 골드"));
            Assert.That(
                fixture.Presenter.GoldValueText,
                Is.EqualTo("820 / 1,000"));
            Assert.That(fixture.Presenter.PoliceBadgeText, Is.EqualTo("승리"));
            Assert.That(fixture.Presenter.ThiefBadgeText, Is.EqualTo("패배"));

            Object.DestroyImmediate(fixture.Root);
        }

        [Test]
        public void ThiefWinReportsTheTreasuresThatEndedIt()
        {
            MatchResultSession.ReportSummary(
                new MatchSummary(452f, 1, 3, 12, 1350, 1000));
            MatchResultSession.TryStore(
                new MatchResult(
                    MatchWinner.Thief,
                    MatchEndReason.SaleTargetReached,
                    1350,
                    0f));
            Fixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.ReasonText,
                Is.EqualTo("목표 골드를 모아 탈출에 성공했습니다"));
            Assert.That(fixture.Presenter.ElapsedText, Is.EqualTo("07:32"));
            Assert.That(
                fixture.Presenter.MiddleCaptionText,
                Is.EqualTo("훔친 보물"));
            Assert.That(fixture.Presenter.MiddleValueText, Is.EqualTo("12"));
            Assert.That(
                fixture.Presenter.GoldCaptionText,
                Is.EqualTo("획득 골드"));
            Assert.That(fixture.Presenter.PoliceBadgeText, Is.EqualTo("패배"));
            Assert.That(fixture.Presenter.ThiefBadgeText, Is.EqualTo("승리"));

            Object.DestroyImmediate(fixture.Root);
        }

        /// <summary>
        /// Opening the scene without a match is a development path. It has to
        /// say so rather than present a match of all zeroes as a real one.
        /// </summary>
        [Test]
        public void WithoutAMatchItSaysSoRatherThanReportingZeroes()
        {
            Fixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.ReasonText,
                Is.EqualTo("경기를 한 번 진행하면 결과가 표시됩니다."));
            Assert.That(fixture.Presenter.ElapsedText, Is.EqualTo("--:--"));
            Assert.That(fixture.Presenter.MiddleValueText, Is.EqualTo("-"));
            Assert.That(fixture.Presenter.GoldValueText, Is.EqualTo("-"));

            Object.DestroyImmediate(fixture.Root);
        }

        [TestCase(MatchWinner.Police, "Police Title", "Police Versus")]
        [TestCase(MatchWinner.Thief, "Thief Title", "Thief Versus")]
        public void ArtworkFollowsTheWinner(
            MatchWinner winner,
            string expectedTitle,
            string expectedVersus)
        {
            MatchEndReason reason = winner == MatchWinner.Police
                ? MatchEndReason.ThiefArrested
                : MatchEndReason.SaleTargetReached;
            MatchResultSession.ReportSummary(
                new MatchSummary(120f, 3, 3, 6, 900, 1000));
            MatchResultSession.TryStore(
                new MatchResult(winner, reason, 900, 120f));
            Fixture fixture = CreatePresenter();

            Assert.That(
                fixture.Presenter.TitleSprite.name,
                Is.EqualTo(expectedTitle));
            Assert.That(
                fixture.Presenter.VersusSprite.name,
                Is.EqualTo(expectedVersus));

            Object.DestroyImmediate(fixture.Root);
        }

        [Test]
        public void MissingReferencesFailLoudly()
        {
            var root = new GameObject("Result");
            var presenter = root.AddComponent<ResultScreenPresenter>();

            Assert.Throws<System.InvalidOperationException>(
                () => presenter.ValidateOrThrow());

            Object.DestroyImmediate(root);
        }

        private readonly struct Fixture
        {
            public Fixture(GameObject root, ResultScreenPresenter presenter)
            {
                Root = root;
                Presenter = presenter;
            }

            public GameObject Root { get; }
            public ResultScreenPresenter Presenter { get; }
        }

        /// <summary>
        /// Built by hand rather than loaded from the prefab so a failure points
        /// at the presenter's logic. The prefab's own wiring is checked
        /// separately by the result contract tests.
        /// </summary>
        private static Fixture CreatePresenter()
        {
            var root = new GameObject("Result");
            var presenter = root.AddComponent<ResultScreenPresenter>();

            presenter.ConfigureArt(
                CreateImage("Title", root.transform),
                CreateSprite("Police Title"),
                CreateSprite("Thief Title"),
                CreateImage("Versus", root.transform),
                CreateSprite("Police Versus"),
                CreateSprite("Thief Versus"));
            presenter.ConfigureBadges(
                CreateText("Police Badge", root.transform),
                CreateText("Thief Badge", root.transform));
            presenter.ConfigureStats(
                CreateText("Reason", root.transform),
                CreateText("Elapsed", root.transform),
                CreateImage("Middle Icon", root.transform),
                CreateSprite("Arrest Icon"),
                CreateSprite("Loot Icon"),
                CreateText("Middle Caption", root.transform),
                CreateText("Middle Value", root.transform),
                CreateText("Gold Caption", root.transform),
                CreateText("Gold Value", root.transform));

            presenter.Refresh();
            return new Fixture(root, presenter);
        }

        private static TMP_Text CreateText(string name, Transform parent)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            return child.GetComponent<TMP_Text>();
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var child = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f));
            sprite.name = name;
            return sprite;
        }
    }
}
