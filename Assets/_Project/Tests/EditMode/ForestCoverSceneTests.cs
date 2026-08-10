using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// The forest is cover you run into. The lake is not.
    ///
    /// Both arrived through the same path as a building — raised until their
    /// lowest point sat on the road, then wrapped in a box the size of the plot.
    /// For a house that is right. For a patch of landscape it makes a solid cube
    /// with trees printed on it, standing on a plinth of its own earth: the
    /// forest floor was 1.3 m above the grass and you could see the step from
    /// across the town.
    ///
    /// Trees are the only cover here that hides without blocking. The officer's
    /// one angle is straight down, and a canopy takes that away — which is worth
    /// more to the thief than any wall. That only works if the trees can be stood
    /// among.
    ///
    /// The lake keeps its box on purpose, and this says so, because "make the
    /// landscape walk-through" applied to water would let both players stroll
    /// across it.
    /// </summary>
    public sealed class ForestCoverSceneTests
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/Game.unity";

        [Test]
        public void TheForestCanBeWalkedInto()
        {
            Transform forest = Find("Forest");
            Assert.That(
                forest.GetComponentsInChildren<Collider>(true),
                Is.Empty,
                "The forest has a collider, so it is a solid block. It is welded "
                + "into one mesh with its own ground, so there is nothing to "
                + "wrap that is only the trunks — a box is a cube and a mesh "
                + "collider makes every trunk a wall to catch on.");
        }

        [Test]
        public void TheForestFloorMeetsTheGrass()
        {
            Transform forest = Find("Forest");
            Bounds bounds = Silhouette(forest);

            // Sunk, which is what having its own earth buried looks like. The
            // measurement that decides how far is taken from the mesh at build
            // time and reported; this pins that it happened at all.
            Assert.That(
                bounds.min.y,
                Is.LessThan(-0.3f),
                $"The forest's base sits at y={bounds.min.y:0.00}. Above about "
                + "-0.3 its own slab of earth is still above the street and "
                + "there is a step at the treeline.");

            // And not buried so far that the trees are shrubs.
            Assert.That(
                bounds.max.y,
                Is.GreaterThan(2f),
                $"The forest tops out at y={bounds.max.y:0.00}. Sunk that far "
                + "there is no canopy left to hide under.");
        }

        [Test]
        public void TheLakeIsStillSomethingYouWalkAround()
        {
            Transform lake = Find("Lake Garden");
            Assert.That(
                lake.GetComponentsInChildren<Collider>(true),
                Is.Not.Empty,
                "The lake lost its collider. Landscape being walk-through is a "
                + "decision per landmark — applied to water it lets both players "
                + "walk across it.");
            Assert.That(
                Silhouette(lake).min.y,
                Is.GreaterThan(-0.3f),
                "The lake was sunk. Its surface is the water, so burying it puts "
                + "the water under the grass.");
        }

        private static Transform Find(string name)
        {
            EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);

            Transform found = Object
                .FindObjectsByType<Transform>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name);

            Assert.That(
                found,
                Is.Not.Null,
                $"No '{name}' in the scene. Rebuild the greybox village.");
            return found;
        }

        private static Bounds Silhouette(Transform root)
        {
            Renderer[] parts = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(parts, Is.Not.Empty, $"{root.name} draws nothing.");

            Bounds bounds = parts[0].bounds;
            foreach (Renderer part in parts.Skip(1))
            {
                bounds.Encapsulate(part.bounds);
            }

            return bounds;
        }
    }
}
