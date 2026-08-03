using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Animation;
using PawsAndLoot.Companions;
using PawsAndLoot.Core;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The animals show what they are doing.
    ///
    /// This is the only thing in the game that says "it understood you" in the
    /// frame it happens. Everything else makes the player infer it from the
    /// animal eventually moving, which is slow to read and impossible to see in
    /// a recording.
    ///
    /// Checks that the icons exist in the built scene and that raising an
    /// outcome puts one on screen — not that a component believes it is
    /// showing something. A mesh that is active, enabled and facing away is a
    /// mesh nobody sees, and this project has shipped exactly that before.
    /// </summary>
    public sealed class CompanionExpressionPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
        }

        /// <summary>
        /// Every outcome the resolver can report lands on a face.
        ///
        /// A new outcome added later with no mapping falls through to None and
        /// shows nothing, which is silent and looks like the animal ignoring
        /// you. Enumerating the enum is what makes that a failure rather than a
        /// gap nobody notices.
        /// </summary>
        [Test]
        public void EveryOutcomeMapsToAFace()
        {
            CompanionCommandOutcome[] outcomes = Enum
                .GetValues(typeof(CompanionCommandOutcome))
                .Cast<CompanionCommandOutcome>()
                .Where(o => o != CompanionCommandOutcome.None)
                .ToArray();

            Assert.That(outcomes, Is.Not.Empty);

            foreach (CompanionCommandOutcome outcome in outcomes)
            {
                Assert.That(
                    CompanionExpressionPresenter.FaceFor(outcome),
                    Is.Not.EqualTo(CompanionExpression.None),
                    $"{outcome} shows nothing, so the animal looks like it "
                    + "ignored the order.");
            }
        }

        /// <summary>
        /// The three "nothing there" outcomes read as confusion, not success.
        ///
        /// A dog that trots off after a trail that does not exist looks exactly
        /// like a dog that found one. That is the single most misleading thing
        /// the animals do and it is the reason these four icons are worth
        /// building.
        /// </summary>
        [Test]
        public void FindingNothingDoesNotLookLikeSuccess()
        {
            foreach (CompanionCommandOutcome empty in
                new[]
                {
                    CompanionCommandOutcome.TrailMissing,
                    CompanionCommandOutcome.BarkFoundNobody,
                    CompanionCommandOutcome.ScoutFoundNothing,
                    CompanionCommandOutcome.StealNoLoot
                })
            {
                Assert.That(
                    CompanionExpressionPresenter.FaceFor(empty),
                    Is.EqualTo(CompanionExpression.Confused),
                    $"{empty} reads as success.");
            }

            Assert.That(
                CompanionExpressionPresenter.FaceFor(
                    CompanionCommandOutcome.TrailFound),
                Is.EqualTo(CompanionExpression.Alert));
        }

        [UnityTest]
        public IEnumerator BothAnimalsCarryAllFourIcons()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            CompanionAgent[] agents = UnityEngine.Object
                .FindObjectsByType<CompanionAgent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(agents.Length, Is.GreaterThanOrEqualTo(2));

            foreach (CompanionAgent agent in agents)
            {
                var view = agent.GetComponent<CompanionExpressionView>();
                Assert.That(
                    view,
                    Is.Not.Null,
                    $"{agent.name} has no expression view.");
                Assert.That(
                    agent.GetComponent<CompanionExpressionPresenter>(),
                    Is.Not.Null,
                    $"{agent.name} has no expression presenter.");

                Transform anchor = agent.transform.Find("Expression");
                Assert.That(
                    anchor,
                    Is.Not.Null,
                    $"{agent.name} has no icon anchor.");

                foreach (CompanionExpression face in
                    new[]
                    {
                        CompanionExpression.Alert,
                        CompanionExpression.Thinking,
                        CompanionExpression.Happy,
                        CompanionExpression.Confused
                    })
                {
                    Transform slot = anchor.Find(face.ToString());
                    Assert.That(
                        slot,
                        Is.Not.Null,
                        $"{agent.name} is missing the {face} icon.");
                    Assert.That(
                        slot.GetComponentsInChildren<Renderer>(true),
                        Is.Not.Empty,
                        $"{agent.name}'s {face} icon has nothing to draw.");
                }

                // Above the animal, or it is inside its own head.
                Assert.That(
                    anchor.localPosition.y,
                    Is.GreaterThan(1f),
                    $"{agent.name}'s icons sit inside the model.");
            }
        }

        /// <summary>
        /// Showing a face puts a renderer on screen and takes it away again.
        ///
        /// Measured from the renderers rather than from
        /// <c>view.IsShowing</c>: a component's opinion of itself is what
        /// passed while four stun stars were being back-face culled every frame
        /// and never drawn.
        /// </summary>
        [UnityTest]
        public IEnumerator ShowingAFacePutsExactlyOneIconOnScreen()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            UnityEngine.Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            CompanionAgent agent = UnityEngine.Object
                .FindObjectsByType<CompanionAgent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .First();
            var view = agent.GetComponent<CompanionExpressionView>();
            Transform anchor = agent.transform.Find("Expression");

            Assert.That(
                ActiveIconCount(anchor),
                Is.Zero,
                "An animal that has been asked nothing is showing something.");

            view.Show(CompanionExpression.Alert);
            yield return null;

            Assert.That(
                ActiveIconCount(anchor),
                Is.EqualTo(1),
                "Showing one face left more than one icon on.");
            Assert.That(
                anchor.Find("Alert").gameObject.activeInHierarchy,
                Is.True);

            // And it goes away on its own rather than staying up forever.
            for (float waited = 0f; waited < 4f && view.IsShowing;
                 waited += Time.deltaTime)
            {
                yield return null;
            }

            Assert.That(
                view.IsShowing,
                Is.False,
                "The icon never expired, so the animal wears the last thing "
                + "it thought for the rest of the match.");
            Assert.That(ActiveIconCount(anchor), Is.Zero);
        }

        /// <summary>
        /// One face replaces another rather than stacking.
        /// </summary>
        [UnityTest]
        public IEnumerator ASecondFaceReplacesTheFirst()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            CompanionAgent agent = UnityEngine.Object
                .FindObjectsByType<CompanionAgent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(a => a.name, StringComparer.Ordinal)
                .First();
            var view = agent.GetComponent<CompanionExpressionView>();
            Transform anchor = agent.transform.Find("Expression");

            view.Show(CompanionExpression.Thinking);
            yield return null;
            view.Show(CompanionExpression.Happy);
            yield return null;

            Assert.That(ActiveIconCount(anchor), Is.EqualTo(1));
            Assert.That(view.Current, Is.EqualTo(CompanionExpression.Happy));
            Assert.That(
                anchor.Find("Thinking").gameObject.activeSelf,
                Is.False);
        }

        private static int ActiveIconCount(Transform anchor)
        {
            int count = 0;
            foreach (Transform slot in anchor)
            {
                if (slot.gameObject.activeInHierarchy
                    && slot.GetComponentsInChildren<Renderer>(false)
                        .Any(r => r.enabled))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
