using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The houses are one size, and every one of them is inside the town.
    ///
    /// Both halves came from the same mistake. Each house used to be stretched to
    /// fill whatever lot it stood in, so the same two models read as half a dozen
    /// different buildings — and the moment they were all given one size, the far
    /// north row no longer fitted the band it was in and six of them stood through
    /// the wall.
    ///
    /// Neither is visible from the play camera and neither breaks a test that only
    /// checks routes, which is why this exists.
    /// </summary>
    public sealed class HouseLayoutSceneTests
    {
        // The town, as the builder defines it.
        private const float MinX = -28f;
        private const float MaxX = 52f;
        private const float MinZ = -22f;
        private const float MaxZ = 50f;

        [Test]
        public void EveryHouseIsTheSameSizeAndInsideTheTown()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            BoxCollider[] houses = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<BoxCollider>(true))
                .Where(box => box.name.Contains("house"))
                .ToArray();

            Assert.That(
                houses.Length,
                Is.GreaterThan(10),
                "The districts are built from houses; without them this test "
                + "proves nothing.");

            // Grouped by model, because the two house models are genuinely
            // different buildings. What must not vary is the same model appearing
            // at different sizes.
            foreach (var group in houses.GroupBy(box => box.name))
            {
                float[] depths = group
                    .Select(box => box.bounds.size.z)
                    .ToArray();
                Assert.That(
                    depths.Max() - depths.Min(),
                    Is.LessThan(0.05f),
                    $"'{group.Key}' appears at depths from "
                    + $"{depths.Min():0.00} to {depths.Max():0.00}. One model "
                    + "has to be one size, or the town looks like it was "
                    + "assembled from mismatched kits.");
            }

            foreach (BoxCollider house in houses)
            {
                Bounds bounds = house.bounds;
                Assert.That(
                    bounds.min.x,
                    Is.GreaterThanOrEqualTo(MinX),
                    $"{house.name} at {bounds.center} crosses the west wall.");
                Assert.That(
                    bounds.max.x,
                    Is.LessThanOrEqualTo(MaxX),
                    $"{house.name} at {bounds.center} crosses the east wall.");
                Assert.That(
                    bounds.min.z,
                    Is.GreaterThanOrEqualTo(MinZ),
                    $"{house.name} at {bounds.center} crosses the south wall.");
                Assert.That(
                    bounds.max.z,
                    Is.LessThanOrEqualTo(MaxZ),
                    $"{house.name} at {bounds.center} crosses the north wall — "
                    + "the exact thing that happened when they were all made "
                    + "one size and the band was too shallow for it.");
            }
        }
    }
}
