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
        public IEnumerator TheThiefGoesInTheBackAndOutTheFront()
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
                Object.FindFirstObjectByType<HouseInterior>();
            HouseDoorway backIn =
                Door(interior, true, HouseDoorSide.Back);
            HouseDoorway frontOut =
                Door(interior, false, HouseDoorSide.Front);

            Assert.That(
                backIn.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "The back door has to let somebody in.");
            yield return null;

            Assert.That(state.IsIndoors, Is.True);

            // At the back of the room, not the front. The two entry points are what
            // make the two doors different; landing at the same spot from either
            // would make the back door decoration.
            Vector3 arrived = thief.transform.position;
            float atBack = Vector3.Distance(
                arrived,
                interior.EntryPositionFor(HouseDoorSide.Back));
            float atFront = Vector3.Distance(
                arrived,
                interior.EntryPositionFor(HouseDoorSide.Front));
            Assert.That(
                atBack,
                Is.LessThan(atFront - 3f),
                $"Coming in the back put the thief {atBack:0.0} m from the back "
                + $"entry and {atFront:0.0} m from the front one — the two ends "
                + "of the room are not distinct.");

            // Standing on the floor, not buried in it.
            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThanOrEqualTo(interior.FloorHeight - 0.02f),
                "The back door placed the thief inside the floor.");

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

            Transform house = backIn.transform.parent;
            Vector3 local =
                house.InverseTransformPoint(thief.transform.position);
            Assert.That(
                local.z,
                Is.GreaterThan(0f),
                "Leaving by the front door put the thief behind the house.");
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
                Object.FindFirstObjectByType<HouseInterior>();
            Renderer wall = interior
                .GetComponentsInChildren<Renderer>(true)
                .First(r => r.name == "BD_House1F_Wall_Left");
            float wallTop = wall.bounds.max.y;

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
