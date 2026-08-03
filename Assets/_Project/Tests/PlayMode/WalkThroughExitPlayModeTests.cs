using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Getting out of a room is walking out of it.
    ///
    /// The doorway in a room's wall is a real hole, and the room is a floor slab with
    /// nothing beyond it, so walking through the opening dropped the player off the
    /// edge of the world. Pressing a key at the same spot worked, which is what made
    /// it look like a control problem rather than a hole.
    ///
    /// The press is also wanted for something else indoors ??wardrobes and drawers ??
    /// so the way out is a trigger and takes no prompt at all.
    /// </summary>
    public sealed class WalkThroughExitPlayModeTests
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
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        /// <summary>
        /// A room you can actually walk out of, picked the same way every run.
        ///
        /// <c>FindFirstObjectByType</c> hands back whatever instance ID came
        /// first, which is not a property of the map: the scene has fourteen
        /// interiors and only thirteen pairs of doorways, because the jail cell
        /// is a room nobody walks into. Regenerating the scene reshuffles the IDs,
        /// and the run that lands on the cell fails looking for a door that was
        /// never meant to exist.
        ///
        /// Ordered by name, and required to have the doorways this fixture is
        /// about, so the choice is the map's and not the serialiser's.
        /// </summary>
        private static HouseInterior InteriorWithDoors()
        {
            HouseDoorway[] doors = Object
                .FindObjectsByType<HouseDoorway>(FindObjectsSortMode.None);
            HouseInterior interior = Object
                .FindObjectsByType<HouseInterior>(FindObjectsSortMode.None)
                .OrderBy(candidate => candidate.name)
                .FirstOrDefault(candidate => doors.Any(door =>
                        door.Interior == candidate
                        && door.LeadsInside
                        && door.Side == HouseDoorSide.Front)
                    && doors.Any(door =>
                        door.Interior == candidate
                        && !door.LeadsInside
                        && door.Side == HouseDoorSide.Front));

            Assert.That(
                interior,
                Is.Not.Null,
                "No interior in the scene has a front doorway in and out, so "
                + "there is nothing for this fixture to walk through.");
            return interior;
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
        public IEnumerator WalkingIntoTheInsideDoorwayPutsYouOutside()
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

            HouseInterior interior = InteriorWithDoors();
            HouseDoorway wayIn =
                Door(interior, true, HouseDoorSide.Front);
            HouseDoorway wayOut =
                Door(interior, false, HouseDoorSide.Front);

            Assert.That(
                wayOut.IsAutomatic,
                Is.True,
                "The door inside the room still has to be pressed.");
            Assert.That(
                wayIn.IsAutomatic,
                Is.False,
                "The street door became automatic, which would suck players "
                + "into a house every time they ran past a porch.");

            Assert.That(
                wayIn.TryInteract(new PlayerInteractionContext(thief)),
                Is.True);
            yield return null;
            Assert.That(state.IsIndoors, Is.True);

            // Now simply walk into it, with no press at all. Placed inside the room
            // and stepped toward the opening the way a player would.
            //
            // The rooms are built unrotated, so the front wall is at +Z and inside is
            // the lower z: standing beyond the door and walking away from it proved
            // nothing, which the first version of this test did.
            Vector3 doorway = wayOut.transform.position;
            float lift = controller.height * 0.5f - controller.center.y;
            controller.enabled = false;
            thief.transform.position = new Vector3(
                doorway.x,
                interior.FloorHeight + lift,
                doorway.z - 3f);
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return null;

            // Driven through the controller rather than the motor, because the motor
            // steers relative to whatever camera is live and the interior one has
            // just taken over. What is under test is the doorway, not the steering.
            for (int frame = 0; frame < 90 && state.IsIndoors; frame++)
            {
                controller.Move(new Vector3(0f, -0.05f, 0.12f));
                yield return null;
            }

            Assert.That(
                state.IsIndoors,
                Is.False,
                "Walking into the doorway did nothing, so the player would have "
                + "carried on through the hole and off the edge of the room.");

            // And landed on something, rather than falling.
            var motor = thief.GetComponent<PlayerMovementMotor>();
            for (int frame = 0; frame < 120; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(-1f),
                $"The thief is at y={controller.bounds.min.y:0.00} after leaving, "
                + "which means they are still falling.");
        }

        /// <summary>
        /// The press never lands on the way out.
        ///
        /// It is wanted for the wardrobes and drawers a thief is in the room to open,
        /// and a doorway that wins the nearest-target contest against the furniture
        /// beside it would take the press back.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWayOutIsNotSomethingThePressCanFind()
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
            var scanner = thief.GetComponent<PlayerInteractionScanner>();

            HouseInterior interior = InteriorWithDoors();
            HouseDoorway wayOut =
                Door(interior, false, HouseDoorSide.Front);

            Assert.That(
                thief.CanInteract(wayOut.InteractionType),
                Is.False,
                "The thief can still press the way out.");
            Assert.That(
                wayOut.Prompt,
                Is.Empty,
                "An automatic door should not offer a prompt.");

            // Standing right on it, the scanner must not pick it.
            var controller = thief.GetComponent<CharacterController>();
            controller.enabled = false;
            thief.transform.position = wayOut.transform.position;
            controller.enabled = true;
            Physics.SyncTransforms();
            yield return null;

            scanner.RefreshTarget();
            Assert.That(
                scanner.CurrentTarget as HouseDoorway,
                Is.Null,
                "Standing in the doorway, the press goes to the door instead of "
                + "to whatever is worth opening.");
        }
    }
}
