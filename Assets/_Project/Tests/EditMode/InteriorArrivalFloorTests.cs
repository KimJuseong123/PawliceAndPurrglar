using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// Every interior's entry point stands on that interior's floor.
    ///
    /// This is the check that was missing when the collision meshes were
    /// imported without normals. <c>MeasureFloorTop</c> reads those normals to
    /// find which faces point up; with none, its loop ran zero times and it
    /// returned its fallback — **the underside of the model**. Every entry point
    /// dropped roughly a floor-thickness, and the player walked through the door
    /// already buried to the waist.
    ///
    /// Nothing threw. The scene saved, the generator logged success, and the
    /// whole suite passed. The only way to find it was to walk into a building.
    ///
    /// So this asserts the relationship the generator is supposed to produce,
    /// against the saved scene: an entry point is on the floor under it, not
    /// under that floor. It is deliberately a geometry question and not a
    /// "did the component get configured" question — the components were all
    /// configured perfectly the whole time.
    /// </summary>
    public sealed class InteriorArrivalFloorTests
    {
        /// <summary>
        /// How far above the collision surface an entry point may sit.
        ///
        /// Generous upward: the generator lifts entry points clear of thin rugs
        /// and thresholds, and standing a little high is corrected by gravity on
        /// the first frame. Tight downward, because that direction is the bug —
        /// below the surface is inside the floor, and the controller resolves
        /// that by pushing further down.
        /// </summary>
        private const float AllowedAbove = 1.5f;

        private const float AllowedBelow = 0.05f;

        [Test]
        public void EveryInteriorEntryPointStandsOnItsFloor()
        {
            EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);

            HouseInterior[] rooms = Object
                .FindObjectsByType<HouseInterior>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .OrderBy(room => room.InteriorId)
                .ToArray();

            Assert.That(
                rooms.Length,
                Is.GreaterThan(0),
                "No interiors in the scene, so this test proves nothing.");

            var buried = new List<string>();
            var offsets = new List<float>();
            var checkedRooms = 0;

            foreach (HouseInterior room in rooms)
            {
                foreach (HouseDoorSide side in
                    new[] { HouseDoorSide.Front, HouseDoorSide.Back })
                {
                    Vector3 entry = room.EntryPositionFor(side);
                    if (!TryFloorUnder(room, entry, out float floor))
                    {
                        continue;
                    }

                    checkedRooms++;
                    float above = entry.y - floor;
                    offsets.Add(above);
                    if (above < -AllowedBelow || above > AllowedAbove)
                    {
                        buried.Add(
                            $"interior {room.InteriorId} ({room.name}) {side}: "
                            + $"entry y={entry.y:0.00}, floor y={floor:0.00}, "
                            + $"offset {above:+0.00;-0.00}m");
                    }
                }
            }

            // Printed, not just asserted. "It passed" and "it measured
            // something real" are different claims, and the second one is what
            // makes the first worth anything.
            Debug.Log(
                $"[INTERIOR-FLOOR] measured {checkedRooms} entry point(s) across "
                + $"{rooms.Length} interiors; offsets "
                + $"{(offsets.Count > 0 ? offsets.Min() : 0f):0.00}m to "
                + $"{(offsets.Count > 0 ? offsets.Max() : 0f):0.00}m above floor.");

            Assert.That(
                checkedRooms,
                Is.GreaterThan(0),
                "No entry point had any floor under it at all, so the "
                + "measurement below never ran.");

            Assert.That(
                buried,
                Is.Empty,
                "An entry point that sits below the floor buries the player the "
                + "moment they walk through the door:\n  "
                + string.Join("\n  ", buried));
        }

        /// <summary>
        /// The collision surface directly beneath a point, searched from above.
        ///
        /// Raycast rather than bounds, because the question is "what would the
        /// character's feet rest on", and that is the collider the physics
        /// engine will find.
        /// </summary>
        private static bool TryFloorUnder(
            HouseInterior room,
            Vector3 at,
            out float floorY)
        {
            floorY = 0f;
            Collider[] colliders =
                room.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                return false;
            }

            bool found = false;
            float best = float.MinValue;
            var from = new Vector3(at.x, at.y + 4f, at.z);

            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger)
                {
                    continue;
                }

                if (!collider.Raycast(
                        new Ray(from, Vector3.down),
                        out RaycastHit hit,
                        12f))
                {
                    continue;
                }

                // The highest surface under the point is the one stood on.
                if (hit.point.y > best)
                {
                    best = hit.point.y;
                    found = true;
                }
            }

            floorY = best;
            return found;
        }
    }
}
