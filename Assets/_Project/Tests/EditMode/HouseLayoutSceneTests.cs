using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using UnityEditor;
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

        /// <summary>
        /// The houses are still prefab references, not loose parts in the scene.
        ///
        /// Making the front door swing used to need the prefab unpacked, which
        /// writes all 186 parts of a house into the scene file as real objects.
        /// Eight houses took this scene from 3.2 MB to 9.2 MB and, because it is
        /// regenerated wholesale, a fresh copy landed in history on every rebuild.
        /// Nothing about the game looked different, so only a measurement catches
        /// it.
        ///
        /// The count has to be of prefab membership, not of transforms. Opening the
        /// scene instantiates every prefab, so all 4,715 parts are present in the
        /// hierarchy either way — that is what a prefab reference means. The
        /// question is whether they belong to an instance or were written out
        /// individually.
        /// </summary>
        [Test]
        public void HousesAreNotUnpackedIntoTheScene()
        {
            string path = GameSceneCatalog.GetPath(GameSceneId.Game);
            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);

            GameObject[] modelParts = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("BD_"))
                .Select(t => t.gameObject)
                .ToArray();

            Assert.That(
                modelParts.Length,
                Is.GreaterThan(100),
                "No building parts at all, so this test proves nothing.");

            GameObject[] loose = modelParts
                .Where(part =>
                    !PrefabUtility.IsPartOfPrefabInstance(part))
                .ToArray();
            Assert.That(
                loose.Length,
                Is.Zero,
                $"{loose.Length} building parts do not belong to a prefab "
                + "instance, which means a prefab was unpacked. Each one is "
                + $"written into the scene file: e.g. '{loose.FirstOrDefault()?.name}'.");

            // The consequence, measured directly. An unpack does not announce
            // itself, but the file size does.
            var file = new System.IO.FileInfo(path);
            Assert.That(
                file.Length / (1024f * 1024f),
                Is.LessThan(5f),
                $"Game.unity is {file.Length / (1024f * 1024f):0.0} MB. It is "
                + "regenerated wholesale, so every rebuild commits another copy "
                + "of whatever this grows to.");

            // And the doors still exist, so this cannot be passed by having no
            // openable houses at all.
            int doors = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        PawsAndLoot.Animation.HouseDoorLeaf>(true))
                .Count();
            Assert.That(
                doors,
                Is.GreaterThan(0),
                "No front doors, so the counts above prove nothing.");
        }
    }
}
