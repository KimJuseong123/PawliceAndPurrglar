using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Loot;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// Nothing a player has to walk up to may be inside a building, and no two
    /// of them may share a spot.
    ///
    /// Both have happened and neither was noticed. Two pieces of loot sat inside
    /// the jeweller's exterior — a shell nobody can walk into, since the rooms
    /// are somewhere else entirely — and the overlap check passed them because a
    /// building's colliders are its walls, so its inside is empty space. The map
    /// built, validated, and logged that it had placed them.
    ///
    /// Measured against the drawn silhouette rather than the colliders, because
    /// what encloses a spot is the building and the building is what can be
    /// seen. And measured off the saved scene, because a generator reporting
    /// "6 loot pieces placed" has said what it tried to do.
    /// </summary>
    public sealed class PickupPlacementSceneTests
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/Game.unity";

        /// <summary>
        /// Clear of a wall by more than a pickup's own reach, so standing at one
        /// does not mean standing in a doorway or against a corner.
        /// </summary>
        private const float WallClearanceMeters = 1f;

        [Test]
        public void NoPickupStandsInsideABuilding()
        {
            OpenScene();
            List<Bounds> blocks = Blocks();
            Assert.That(
                blocks,
                Is.Not.Empty,
                "No building silhouettes found, so this test is checking "
                + "nothing. Rebuild the greybox village.");

            foreach ((string Name, Vector3 At) pickup in Pickups())
            {
                foreach (Bounds block in blocks)
                {
                    bool inside = pickup.At.x > block.min.x
                        && pickup.At.x < block.max.x
                        && pickup.At.z > block.min.z
                        && pickup.At.z < block.max.z;
                    Assert.That(
                        inside,
                        Is.False,
                        $"'{pickup.Name}' at ({pickup.At.x:0.0}, "
                        + $"{pickup.At.z:0.0}) is inside a building running "
                        + $"x {block.min.x:0.0}..{block.max.x:0.0}, "
                        + $"z {block.min.z:0.0}..{block.max.z:0.0}. The "
                        + "exteriors are shells nobody can enter.");
                }
            }
        }

        [Test]
        public void NoPickupIsPressedAgainstAWall()
        {
            OpenScene();
            List<Bounds> blocks = Blocks();

            foreach ((string Name, Vector3 At) pickup in Pickups())
            {
                foreach (Bounds block in blocks)
                {
                    // Horizontal distance to the silhouette, zero when inside.
                    float dx = Mathf.Max(
                        block.min.x - pickup.At.x,
                        pickup.At.x - block.max.x);
                    float dz = Mathf.Max(
                        block.min.z - pickup.At.z,
                        pickup.At.z - block.max.z);
                    float gap = Mathf.Max(0f, Mathf.Max(dx, dz));

                    Assert.That(
                        gap,
                        Is.GreaterThanOrEqualTo(WallClearanceMeters),
                        $"'{pickup.Name}' is {gap:0.00} m from a wall. One "
                        + "piece of loot used to sit five centimetres from the "
                        + "bookshop, where reaching it means standing in the "
                        + "wall.");
                }
            }
        }

        /// <summary>
        /// Two pickups on the same spot make one of them impossible to take: the
        /// scanner picks a winner and the loser is never offered.
        /// </summary>
        [Test]
        public void NoTwoPickupsShareASpot()
        {
            OpenScene();
            (string Name, Vector3 At)[] pickups = Pickups().ToArray();

            for (int a = 0; a < pickups.Length; a++)
            {
                for (int b = a + 1; b < pickups.Length; b++)
                {
                    float gap = Vector3.Distance(pickups[a].At, pickups[b].At);
                    Assert.That(
                        gap,
                        Is.GreaterThan(0.9f),
                        $"'{pickups[a].Name}' and '{pickups[b].Name}' are "
                        + $"{gap:0.00} m apart.");
                }
            }
        }

        /// <summary>
        /// A rock is only worth anything during a chase, so wherever the chase
        /// goes there has to be one within a short run. Clustered rocks leave
        /// most of the map a place where the thief has nothing to throw.
        /// </summary>
        [Test]
        public void TheRocksAreSpreadOverTheTown()
        {
            OpenScene();
            Vector3[] rocks = Object
                .FindObjectsByType<ThrowablePickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(pickup => pickup.name.StartsWith("Rock Pickup"))
                .Select(pickup => pickup.transform.position)
                .ToArray();

            Assert.That(rocks, Has.Length.EqualTo(5));

            // Every rock has to be a real distance from its nearest neighbour,
            // which is what "spread" means and what an average would hide.
            foreach (Vector3 rock in rocks)
            {
                float nearest = rocks
                    .Where(other => other != rock)
                    .Min(other => Vector3.Distance(rock, other));
                Assert.That(
                    nearest,
                    Is.GreaterThan(8f),
                    $"A rock at ({rock.x:0.0}, {rock.z:0.0}) has another "
                    + $"{nearest:0.0} m away. Two rocks in one place cover one "
                    + "corner of the town twice and the rest not at all.");
            }
        }

        private static void OpenScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        /// <summary>
        /// Everything a player walks up to and presses a key at.
        /// </summary>
        private static IEnumerable<(string Name, Vector3 At)> Pickups()
        {
            foreach (LootItem loot in Object.FindObjectsByType<LootItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                yield return (loot.name, loot.transform.position);
            }

            foreach (ThrowablePickup pickup in
                Object.FindObjectsByType<ThrowablePickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                yield return (pickup.name, pickup.transform.position);
            }
        }

        private static List<Bounds> Blocks()
        {
            var blocks = new List<Bounds>();
            foreach (Transform block in Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (!block.name.StartsWith("Block "))
                {
                    continue;
                }

                Renderer[] parts =
                    block.GetComponentsInChildren<Renderer>(true);
                if (parts.Length == 0)
                {
                    continue;
                }

                Bounds bounds = parts[0].bounds;
                for (int index = 1; index < parts.Length; index++)
                {
                    bounds.Encapsulate(parts[index].bounds);
                }

                blocks.Add(bounds);
            }

            return blocks;
        }
    }
}
