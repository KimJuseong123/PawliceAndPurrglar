using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Loot;
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
                // Matched on what the town calls them.
                //
                // It used to look for "house" in the name, which was the model
                // stem back when this script placed the houses itself. The town
                // generator names them after the plot and what stands on it —
                // "Block 2 TwoStorey" — so the search found nothing and the
                // test passed its own emptiness off as a pass until the count
                // guard below caught it.
                .Where(box =>
                    box.name.Contains("house")
                    || box.name.Contains("OneStorey")
                    || box.name.Contains("TwoStorey"))
                // A house is a building, and this search matches a substring,
                // so anything can join it by being named after one. The houses'
                // loot is — "house1-sausage", "house2-liquor" — and a sausage
                // staged for the one-storey house duly failed this test as a
                // house crossing the west wall.
                //
                // Excluded by what it is rather than by patching the name
                // pattern, because the next thing named after a house will not
                // be spelled the way a pattern guessed. This test has already
                // been fooled once in the other direction: it looked for the
                // old model stem, found nothing, and passed its own emptiness
                // off as a pass.
                .Where(box => box.GetComponentInParent<LootItem>() == null)
                .ToArray();

            Assert.That(
                houses.Length,
                // Nine, which is what the town lays down. The number used to
                // be eleven because the old districts were built house by
                // house here; it is a guard against the search finding
                // nothing, not a statement about how many houses a town wants.
                Is.GreaterThanOrEqualTo(8),
                "The districts are built from houses; without them this test "
                + "proves nothing.");

            // Grouped by model, because the two house models are genuinely
            // different buildings. What must not vary is the same model appearing
            // at different sizes.
            // Grouped by which model it is, not by its full name. Every plot
            // name is unique — "Block 2 TwoStorey", "Block 3 TwoStorey" — so
            // grouping by name would put one house in each group and compare
            // nothing with nothing.
            foreach (var group in houses.GroupBy(box =>
                box.name.Contains("TwoStorey")
                    ? "TwoStorey"
                    : box.name.Contains("OneStorey")
                        ? "OneStorey"
                        : box.name))
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

            // Parts that draw something. The rooms also hold collider-only objects
            // named after the wall they were cut from, and those are built by the
            // setup script on purpose — an unpack is what produces loose
            // *renderers*.
            GameObject[] modelParts = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<Renderer>(true))
                .Where(r => r.name.StartsWith("BD_"))
                .Select(r => r.gameObject)
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
            //
            // The ceiling is deliberately loose. It was 3.4 MB with eight hand-built
            // greybox rooms and is 5.3 MB with nineteen furnished ones, and an
            // unpack of nineteen houses would add about 6 MB on top of that — so
            // this still catches the thing it exists for while leaving room for the
            // rooms to be real. The scene is regenerated wholesale, so whatever
            // this grows to lands in history again on every rebuild.
            var file = new System.IO.FileInfo(path);
            Assert.That(
                file.Length / (1024f * 1024f),
                Is.LessThan(6.5f),
                $"Game.unity is {file.Length / (1024f * 1024f):0.0} MB.");

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
