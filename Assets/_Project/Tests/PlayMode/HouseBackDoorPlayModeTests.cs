using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Camera;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// A house is a route, not a dead end.
    ///
    /// One door meant the officer only had to stand on the porch: the thief who went
    /// in had nowhere to go. Two doors are what make a room worth entering under
    /// pressure, so the property to pin down is that the two are genuinely different
    /// ends — you can go in at the back and come out at the front, and the room does
    /// not quietly send you back where you came from.
    /// </summary>
    public sealed class HouseBackDoorPlayModeTests
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

        private static HouseDoorway Door(
            HouseInterior interior,
            bool leadsInside,
            HouseDoorSide side)
        {
            return Object
                .FindObjectsByType<HouseDoorway>(FindObjectsSortMode.None)
                .First(door =>
                    door.Interior == interior
                    && door.LeadsInside == leadsInside
                    && door.Side == side);
        }

        [UnityTest]
        public IEnumerator TheThiefGoesInAndOutByTheFrontDoor()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(p => p.Role == PlayerRole.Thief);
            var state = thief.GetComponent<PlayerInteriorState>();
            var controller = thief.GetComponent<CharacterController>();

            HouseInterior interior =
                Object
                .FindObjectsByType<HouseInterior>(FindObjectsSortMode.None)
                .First(room => !room.IsJail);
            // In and out by the same door.
            //
            // This test used to go in the back and out the front, which was the
            // point of having two. Rooms are authored models now and most are
            // drawn with one opening; a second door meant a prompt on a solid
            // wall that led into a bookcase. What still has to hold is that the
            // one door works both ways and puts the player on the floor.
            HouseDoorway frontIn =
                Door(interior, true, HouseDoorSide.Front);
            HouseDoorway frontOut =
                Door(interior, false, HouseDoorSide.Front);

            Assert.That(
                frontIn.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "The front door has to let somebody in.");
            yield return null;

            Assert.That(state.IsIndoors, Is.True);

            // Where the room says, and clear of the way out.
            //
            // Arriving inside the exit trigger is the failure this guards: it
            // fires on the frame the player lands and throws them straight back
            // into the street, which reads as the door not working at all.
            Vector3 arrived = thief.transform.position;
            Assert.That(
                Vector3.Distance(
                    arrived,
                    interior.EntryPositionFor(HouseDoorSide.Front)),
                Is.LessThan(1.5f),
                "The front door did not put the thief at its own entry point.");

            // Standing on the floor, not buried in it.
            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThanOrEqualTo(interior.FloorHeight - 0.02f),
                "The front door placed the thief inside the floor.");

            for (int frame = 0; frame < 60; frame++)
            {
                yield return null;
            }

            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(interior.FloorHeight - 0.1f),
                "The thief fell through the floor after coming in the back.");

            // And out the other end, which is the whole point of two doors.
            Assert.That(
                frontOut.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "The front door has to let somebody out.");
            yield return null;

            Assert.That(state.IsIndoors, Is.False);

            // Out where the room says its exit is, rather than behind the
            // house. Measured against the exit point rather than in the
            // building's local space: the doorways hang above the buildings
            // now, so `parent` is the town's Buildings node and its local Z
            // means nothing.
            Assert.That(
                Vector3.Distance(
                    thief.transform.position,
                    interior.ExitPositionFor(HouseDoorSide.Front)),
                Is.LessThan(2.5f),
                "Leaving by the front door did not put the thief at its step.");
        }

        /// <summary>
        /// The camera cannot see over the walls of the room it is in.
        ///
        /// Three numbers decide this and they live in different files: the room's
        /// scale in the setup script, and the camera's distance and pitch cap. The
        /// first time they disagreed, every neighbouring room was visible at once
        /// (ISSUE-035), and neither the scene builder nor any test noticed. Asserting
        /// the relationship rather than the numbers means either can be changed as
        /// long as the other keeps up.
        /// </summary>
        [UnityTest]
        public IEnumerator TheInteriorCameraStaysBelowTheWalls()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            var camera = Object.FindFirstObjectByType<InteriorOrbitCamera>();
            Assert.That(
                camera,
                Is.Not.Null,
                "No interior camera, so this proves nothing.");

            HouseInterior interior =
                Object
                .FindObjectsByType<HouseInterior>(FindObjectsSortMode.None)
                .First(room => !room.IsJail);
            // The top of the room, measured rather than looked up by name.
            //
            // It used to ask for "BD_House1F_Wall_Left", which was a part of
            // the one hand-built interior this was written against. Every room
            // since is a single welded mesh with no named parts, so the lookup
            // threw and took the test's cleanup with it — which then leaked its
            // player into the raccoon tests next door.
            Bounds shell = default;
            bool measured = false;
            foreach (Renderer part in
                interior.GetComponentsInChildren<Renderer>(true))
            {
                if (!measured)
                {
                    shell = part.bounds;
                    measured = true;
                }
                else
                {
                    shell.Encapsulate(part.bounds);
                }
            }

            Assert.That(measured, Is.True, "The room draws nothing.");
            float wallTop = shell.max.y;

            // The highest the camera can ever be above the player's feet.
            float highest = camera.LookOffset.y
                + camera.Distance
                    * Mathf.Sin(camera.MaxPitch * Mathf.Deg2Rad);
            float cameraTop = interior.FloorHeight + highest;

            Assert.That(
                cameraTop,
                Is.LessThan(wallTop - 0.2f),
                $"At its highest the camera reaches y={cameraTop:0.00} against "
                + $"walls topping out at y={wallTop:0.00}. It would look over "
                + "them into the next room.");

            // And not so low that it is inside the floor.
            Assert.That(
                highest,
                Is.GreaterThan(2f),
                "The camera is pinned so low it cannot see past the player.");
        }
    }
}
