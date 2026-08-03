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
    /// The floor of a room holds the player up.
    ///
    /// The existing interior tests check where a door puts you and check it the
    /// same frame, so they cannot see what happens next. Falling needs time, and
    /// in batch mode a frame is a fraction of a millisecond ??a hundred of them is
    /// not even one physics step. So this fixes the frame length and then simply
    /// waits, which is the only way the difference between "placed correctly" and
    /// "stays there" shows up at all.
    /// </summary>
    public sealed class InteriorFloorPlayModeTests
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

        [UnityTest]
        public IEnumerator TheThiefStandsOnTheInteriorFloorAndStaysThere()
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

            HouseInterior interior =
                Object
                    .FindObjectsByType<HouseInterior>(
                        FindObjectsSortMode.None)
                    .OrderBy(
                        interior => interior.name,
                        System.StringComparer.Ordinal)
                    .First();
            Assert.That(
                interior,
                Is.Not.Null,
                "No interiors, so this test proves nothing.");

            HouseDoorway entrance = Object
                .FindObjectsByType<HouseDoorway>(FindObjectsSortMode.None)
                .First(door => door.LeadsInside
                    && door.Interior == interior);

            // In through the real door, so the destination is whatever the game
            // actually uses rather than a spot chosen by the test.
            thief.transform.position =
                entrance.transform.position + Vector3.back * 0.5f;
            Physics.SyncTransforms();
            yield return null;

            Assert.That(
                entrance.TryInteract(
                    new PlayerInteractionContext(thief)),
                Is.True,
                "The door refused to let the thief in.");
            yield return null;

            var controller = thief.GetComponent<CharacterController>();
            float floorTop = interior.EntryPosition.y;

            // The capsule, not the pivot. The player's transform sits about 0.9 m
            // above their feet, so a pivot placed on the floor buries the whole
            // capsule inside it — which is a metre of penetration for the
            // controller to resolve, and it resolves it downward.
            float bottomOnArrival = controller.bounds.min.y;
            Assert.That(
                bottomOnArrival,
                Is.GreaterThanOrEqualTo(floorTop - 0.02f),
                $"The thief arrived with their feet at y={bottomOnArrival:0.00}, "
                + $"below the floor at y={floorTop:0.00}. They were placed "
                + "inside it, not on it.");

            // Two full seconds of gravity. A player who is going to fall through
            // has done it long before this.
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            float restingBottom = controller.bounds.min.y;
            Assert.That(
                restingBottom,
                Is.GreaterThan(floorTop - 0.1f),
                $"After two seconds the thief's feet are at "
                + $"y={restingBottom:0.00}, below the floor at "
                + $"y={floorTop:0.00}. They fell through it.");

            // And still in the room they walked into, not dropped somewhere else.
            Vector3 offset =
                thief.transform.position - interior.transform.position;
            Assert.That(
                new Vector2(offset.x, offset.z).magnitude,
                Is.LessThan(12f),
                $"The thief ended up {offset} from the middle of the room.");

            // Out again, because the way out places the player the same way and
            // would have the same bug. The street is measured rather than assumed
            // to be at zero.
            HouseDoorway exit = Object
                .FindObjectsByType<HouseDoorway>(FindObjectsSortMode.None)
                .First(door => !door.LeadsInside
                    && door.Interior == interior);
            thief.transform.position =
                exit.transform.position + Vector3.up * 1f;
            Physics.SyncTransforms();
            yield return null;

            // No press. The way out is automatic now, so being in the doorway is
            // the whole action — and pressing it afterwards would be refused,
            // because by then the thief is already outside.
            //
            // A few frames, not one. The trigger is reported by the physics step and
            // the move itself waits for LateUpdate, because doing it inside
            // CharacterController.Move gets it overwritten (ISSUE-044).
            var interiorState = thief.GetComponent<PlayerInteriorState>();
            for (int frame = 0; frame < 10 && interiorState.IsIndoors; frame++)
            {
                yield return null;
            }

            Assert.That(
                interiorState.IsIndoors,
                Is.False,
                "Standing in the doorway did not put the thief outside.");

            // The street under the doorstep. Every hit on the thief themselves is
            // skipped: they are standing in the way, and the first version of this
            // measured the top of their own capsule at y=1.86 and called it the
            // ground. Triggers are skipped for the same reason the throw code
            // skips them — the map is full of them.
            RaycastHit[] downward = Physics.RaycastAll(
                thief.transform.position + Vector3.up * 4f,
                Vector3.down,
                14f,
                ~0,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(
                downward,
                (left, right) => left.distance.CompareTo(right.distance));
            RaycastHit? street = null;
            foreach (RaycastHit hit in downward)
            {
                if (hit.collider.transform.IsChildOf(thief.transform))
                {
                    continue;
                }

                street = hit;
                break;
            }

            Assert.That(
                street.HasValue,
                Is.True,
                "There is no ground under the way out.");

            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            Assert.That(
                controller.bounds.min.y,
                Is.GreaterThan(street.Value.point.y - 0.1f),
                $"Outside, the thief's feet are at "
                + $"y={controller.bounds.min.y:0.00} against "
                + $"'{street.Value.collider.name}' at "
                + $"y={street.Value.point.y:0.00}.");
        }
    }
}
