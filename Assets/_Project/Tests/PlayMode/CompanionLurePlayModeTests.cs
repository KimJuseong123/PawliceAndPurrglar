using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The two food props, and the only counterplay either player has against
    /// the other's animal.
    ///
    /// Until these existed a companion could be outrun but never interfered
    /// with, so the animal half of the game had nothing to answer. What has to
    /// hold is that the food calls the animal it smells like, that it overrides
    /// an order the owner already gave, and that the animal goes back to work
    /// afterwards rather than being left standing.
    /// </summary>
    public sealed class CompanionLurePlayModeTests
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

        [Test]
        public void EachFoodCallsExactlyOneAnimal()
        {
            Assert.That(
                ThrowableCatalog.GetLuredCompanion(ThrowableKind.TunaCan),
                Is.EqualTo(CompanionKind.Cat));
            Assert.That(
                ThrowableCatalog.GetLuredCompanion(ThrowableKind.DogTreat),
                Is.EqualTo(CompanionKind.Dog));

            // And nothing else is food.
            foreach (ThrowableKind kind in
                new[]
                {
                    ThrowableKind.Rock,
                    ThrowableKind.Banana,
                    ThrowableKind.GlueTrap,
                    ThrowableKind.SensorLight
                })
            {
                Assert.That(
                    ThrowableCatalog.GetLuredCompanion(kind),
                    Is.Null,
                    $"{kind} calls an animal.");
            }
        }

        /// <summary>
        /// The props stay one per side. A tuna can the thief could buy would be
        /// a way to move your own cat around, which is a different game.
        /// </summary>
        [Test]
        public void TheFoodBelongsToOppositeSides()
        {
            Assert.That(
                ThrowableCatalog.GetOwner(ThrowableKind.TunaCan),
                Is.EqualTo(PlayerRole.Police));
            Assert.That(
                ThrowableCatalog.GetOwner(ThrowableKind.DogTreat),
                Is.EqualTo(PlayerRole.Thief));
        }

        /// <summary>
        /// Food does nothing to a person. A lure that also stunned would just be
        /// a better glue trap.
        /// </summary>
        [Test]
        public void FoodDoesNotStunPlayers()
        {
            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.TunaCan),
                Is.Zero);
            Assert.That(
                ThrowableCatalog.GetStunSeconds(ThrowableKind.DogTreat),
                Is.Zero);
        }

        [UnityTest]
        public IEnumerator BothAnimalsCanBeLured()
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
                Assert.That(
                    agent.GetComponent<CompanionLure>(),
                    Is.Not.Null,
                    $"{agent.name} cannot be lured, so one player has a prop "
                    + "that does nothing.");
            }
        }

        /// <summary>
        /// The animal walks to the food and is still there when the owner is
        /// somewhere else entirely.
        ///
        /// Measured as distance closed rather than "did it enter a state",
        /// because a lure that sets a flag and never moves the dog looks exactly
        /// like a working one from the inside.
        /// </summary>
        [UnityTest]
        public IEnumerator ALuredAnimalWalksToTheFood()
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
            var lure = agent.GetComponent<CompanionLure>();

            Vector3 food = agent.transform.position
                + new Vector3(6f, 0f, 0f);
            float before = Planar(agent.transform.position, food);

            Assert.That(lure.TryLure(food, 4f), Is.True);
            Assert.That(lure.IsActive, Is.True);

            for (int frame = 0; frame < 150 && lure.IsActive; frame++)
            {
                agent.Tick(Time.deltaTime);
                yield return null;
            }

            float after = Planar(agent.transform.position, food);
            Assert.That(
                after,
                Is.LessThan(before - 1f),
                $"The animal was {before:0.0}m from the food and is now "
                + $"{after:0.0}m: it did not go.");
        }

        /// <summary>
        /// The smell wears off. An animal that never came back would take a
        /// character out of the match for one prop.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLureExpires()
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
            var lure = agent.GetComponent<CompanionLure>();

            lure.TryLure(agent.transform.position + Vector3.right, 0.3f);
            Assert.That(lure.IsActive, Is.True);

            for (int frame = 0; frame < 120 && lure.IsActive; frame++)
            {
                yield return null;
            }

            Assert.That(
                lure.IsActive,
                Is.False,
                "The animal never stopped eating.");
        }

        /// <summary>
        /// A second prop moves the animal rather than queueing behind the
        /// first: two treats across the street should send the dog to the newer
        /// one, not make it serve both.
        /// </summary>
        [UnityTest]
        public IEnumerator ASecondFoodReplacesTheFirst()
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
            var lure = agent.GetComponent<CompanionLure>();

            var first = new Vector3(10f, 0f, 10f);
            var second = new Vector3(-10f, 0f, -10f);
            lure.TryLure(first, 5f);
            lure.TryLure(second, 5f);

            Assert.That(
                Planar(lure.Point, second),
                Is.LessThan(0.01f),
                "The animal is still going to the first one.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AClientDoesNotRunItsOwnLure()
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
            var lure = agent.GetComponent<CompanionLure>();
            lure.SetAuthority(false);

            Assert.That(
                lure.TryLure(Vector3.zero, 4f),
                Is.False,
                "The client moved the animal itself, so its position and the "
                + "host's would disagree.");
            yield return null;
        }

        private static float Planar(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
