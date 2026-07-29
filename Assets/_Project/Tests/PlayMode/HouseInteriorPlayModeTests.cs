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
    /// Walking into a house and out again, in the real scene.
    ///
    /// The interiors are the first part of the map that is not where it looks like
    /// it is — the rooms sit far south of the town — so the properties worth
    /// pinning down are the ones that would leave a player stranded: a door that
    /// moves you nowhere, an exit that drops you outside the world, or a room
    /// small enough that the whole point of building it is lost.
    /// </summary>
    public sealed class HouseInteriorPlayModeTests
    {
        [UnityTest]
        public IEnumerator AThiefCanGoInsideAndComeBackOut()
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
            Assert.That(
                state,
                Is.Not.Null,
                "A player with no interior state can never be indoors.");
            Assert.That(state.IsIndoors, Is.False);

            HouseDoorway wayIn = Object
                .FindObjectsByType<HouseDoorway>(
                    FindObjectsSortMode.None)
                .First(door => door.LeadsInside);
            HouseInterior interior = wayIn.Interior;
            Assert.That(interior, Is.Not.Null);

            Vector3 streetPosition = thief.transform.position;
            Assert.That(
                wayIn.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "The street door has to let somebody in.");

            Assert.That(
                state.CurrentInteriorId,
                Is.EqualTo(interior.InteriorId),
                "Going in has to be recorded, or the camera and the dog have "
                + "nothing to read.");
            // Horizontally. The door deliberately puts the player a metre higher
            // than the entry point, because the entry point is the floor and a
            // character's transform sits about that far above their feet — placing
            // the pivot on the floor buried the capsule in it.
            Vector3 arrival =
                thief.transform.position - interior.EntryPosition;
            Assert.That(
                new Vector2(arrival.x, arrival.z).magnitude,
                Is.LessThan(1f),
                "The thief has to actually arrive inside the room.");
            Assert.That(
                Vector3.Distance(
                    thief.transform.position,
                    streetPosition),
                Is.GreaterThan(20f),
                "The room is somewhere else entirely — that separation is what "
                + "stops the officer arresting through a wall.");

            // The street door must refuse a second press rather than teleporting
            // somebody from a room into the same room.
            Assert.That(
                wayIn.TryInteract(new PlayerInteractionContext(thief)),
                Is.False,
                "Pressing the way in while already inside would read as the "
                + "press doing nothing.");

            HouseDoorway wayOut = Object
                .FindObjectsByType<HouseDoorway>(
                    FindObjectsSortMode.None)
                .First(door =>
                    !door.LeadsInside
                    && door.Interior == interior);
            Assert.That(
                wayOut.TryInteract(new PlayerInteractionContext(thief)),
                Is.True);
            Assert.That(state.IsIndoors, Is.False);
            // Horizontally, for the same reason as going in: the exit point is
            // the ground and the player stands about a metre above it.
            Vector3 departure =
                thief.transform.position - interior.ExitPosition;
            Assert.That(
                new Vector2(departure.x, departure.z).magnitude,
                Is.LessThan(1f),
                "Leaving has to put them back in the town.");
        }

        /// <summary>
        /// Going in and out repeatedly must not drop anybody through the floor.
        ///
        /// It did. Gravity accumulates every frame the controller is not grounded,
        /// a teleport leaves it briefly airborne, and nothing cleared the speed —
        /// so a few doors in the capsule was falling fast enough to cross the
        /// floor between two frames. The fix is two things at once: the fall speed
        /// goes with the old position, and the floor is a slab rather than a sheet.
        /// </summary>
        [UnityTest]
        public IEnumerator GoingThroughADoorRepeatedlyKeepsYouOnTheFloor()
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

            HouseDoorway wayIn = Object
                .FindObjectsByType<HouseDoorway>(
                    FindObjectsSortMode.None)
                .First(door => door.LeadsInside);
            HouseDoorway wayOut = Object
                .FindObjectsByType<HouseDoorway>(
                    FindObjectsSortMode.None)
                .First(door =>
                    !door.LeadsInside
                    && door.Interior == wayIn.Interior);

            // Six round trips, with frames in between so gravity has every chance
            // to accumulate the way it did in the game.
            for (int trip = 0; trip < 6; trip++)
            {
                Assert.That(
                    wayIn.TryInteract(new PlayerInteractionContext(thief)),
                    Is.True,
                    $"Trip {trip + 1} could not get in.");
                yield return null;
                yield return null;

                Assert.That(
                    thief.transform.position.y,
                    Is.GreaterThan(-1f),
                    $"Trip {trip + 1}: the thief is at "
                    + $"y={thief.transform.position.y:0.00} inside the room. "
                    + "Anything below the floor means they fell through it.");

                Assert.That(
                    wayOut.TryInteract(new PlayerInteractionContext(thief)),
                    Is.True,
                    $"Trip {trip + 1} could not get out.");
                yield return null;
                yield return null;

                Assert.That(
                    thief.transform.position.y,
                    Is.GreaterThan(-1f),
                    $"Trip {trip + 1}: the thief is at "
                    + $"y={thief.transform.position.y:0.00} in the street.");
            }
        }

        /// <summary>
        /// The way in and the way out are both at the front door.
        ///
        /// Measured against the model rather than assumed: on this house the porch
        /// floor sits at z = +3.5 and the front door leaf at z = +3.1, so the front
        /// is +Z. The first version used -Z and quietly put the entrance and the
        /// exit at the back door instead.
        /// </summary>
        [UnityTest]
        public IEnumerator DoorsAreOnTheFrontOfTheHouse()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            HouseDoorway[] entrances = Object
                .FindObjectsByType<HouseDoorway>(
                    FindObjectsSortMode.None)
                .Where(door => door.LeadsInside)
                .ToArray();
            Assert.That(entrances, Is.Not.Empty);

            foreach (HouseDoorway entrance in entrances)
            {
                // The trigger hangs off the house, so its local z says which wall
                // it is on.
                Assert.That(
                    entrance.transform.localPosition.z,
                    Is.GreaterThan(0f),
                    $"{entrance.name} is on the -Z side, which is the back "
                    + "door. The porch is at +Z.");

                // And the exit puts you back out on the same side.
                Vector3 house = entrance.transform.parent.position;
                Assert.That(
                    entrance.Interior.ExitPosition.z - house.z,
                    Is.GreaterThan(0f),
                    "Coming out at the back of a house you walked into at the "
                    + "front is disorienting in a chase.");
            }
        }

        /// <summary>
        /// Pocketing a valuable pays the thief, once, and only the thief.
        /// </summary>
        [UnityTest]
        public IEnumerator PocketingAValuablePaysTheThiefExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            // The scatter waits for the match to be running, then rolls once.
            yield return null;
            yield return null;

            InteriorValuablePickup[] pickups = Object
                .FindObjectsByType<InteriorValuablePickup>(
                    FindObjectsSortMode.None);
            Assert.That(
                pickups,
                Is.Not.Empty,
                "The host scatters valuables once the match starts; without "
                + "them there is nothing to pick up.");

            PlayerRoleIdentity[] players = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None);
            PlayerRoleIdentity thief =
                players.First(p => p.Role == PlayerRole.Thief);
            PlayerRoleIdentity police =
                players.First(p => p.Role == PlayerRole.Police);
            var wallet =
                thief.GetComponent<PawsAndLoot.Gameplay.Loot.ThiefLootWallet>();

            InteriorValuablePickup piece = pickups[0];
            int before = wallet.SoldAmount;

            // The officer cannot loot the place. Loot is thief-only by type, and
            // an officer who could pocket valuables would be playing the thief's
            // game with the thief's rewards.
            Assert.That(
                PawsAndLoot.Gameplay.Players.PlayerRolePermissions.CanInteract(
                    PlayerRole.Police,
                    piece.InteractionType),
                Is.False,
                "A valuable the officer can take is not the thief's loot.");

            Assert.That(
                piece.TryInteract(new PlayerInteractionContext(thief)),
                Is.True);
            Assert.That(
                wallet.SoldAmount,
                Is.EqualTo(before + piece.Value),
                "Pocketing has to move the number that decides the match — "
                + "there is no merchant trip for small valuables.");
            Assert.That(piece.IsTaken, Is.True);

            // A second press must pay nothing. The interact key can be pressed
            // faster than the world hides what it just gave away.
            Assert.That(
                piece.TryInteract(new PlayerInteractionContext(thief)),
                Is.False);
            Assert.That(
                wallet.SoldAmount,
                Is.EqualTo(before + piece.Value),
                "One shelf, one payment.");

            // Every room together must not be a way around the game.
            int everything = pickups.Sum(p => p.Value);
            Assert.That(
                everything,
                Is.LessThan(wallet.TargetAmount),
                $"Emptying every house pays {everything} against a target of "
                + $"{wallet.TargetAmount}. If the rooms alone could win, "
                + "nobody would ever carry a treasure across town again.");

            Assert.That(police, Is.Not.Null);
        }

        /// <summary>
        /// The room has to be big enough to be worth the trip. A character is
        /// 0.9 m across, and the reason the rooms exist at all is that the 8 m
        /// houses are four character-widths — too small to chase anybody through.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryInteriorIsRoomierThanTheHouseItBelongsTo()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            HouseInterior[] interiors = Object
                .FindObjectsByType<HouseInterior>(
                    FindObjectsSortMode.None);
            Assert.That(
                interiors,
                Is.Not.Empty,
                "Without interiors the doors lead nowhere.");

            foreach (HouseInterior interior in interiors)
            {
                // The real houses are 7.95 x 8.00 m, so half of one is 4 m.
                Assert.That(
                    interior.FloorHalfExtents.x,
                    Is.GreaterThan(5.5f),
                    $"Interior {interior.InteriorId} is no roomier than the "
                    + "house it replaces, which was the entire reason for "
                    + "building it elsewhere.");
                Assert.That(
                    interior.EntryPosition.z,
                    Is.LessThan(-40f),
                    "The rooms live well outside the town; one inside it would "
                    + "put two players in the same space at once.");
            }

            // Ids are how a doorway, the dog and the loot scatter agree on which
            // room they mean, so two rooms sharing one would be a real defect.
            Assert.That(
                interiors.Select(i => i.InteriorId).Distinct().Count(),
                Is.EqualTo(interiors.Length),
                "Interior ids have to be unique.");
        }
    }
}
