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
                thief.transform.position
                - interior.EntryPositionFor(wayIn.Side);
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
                thief.transform.position
                - interior.ExitPositionFor(wayOut.Side);
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
        /// Each door is on the side it says it is, and puts you back out there.
        ///
        /// Measured against the model rather than assumed: the porch floor sits at
        /// z = +3.5 and the front door leaf at z = +3.1, so the front is +Z, and the
        /// back step at -3.3 is the back. The first version had one door, used -Z
        /// for it, and quietly put both the way in and the way out at the back
        /// (ISSUE-034). Now that there are two, what has to hold is that they do not
        /// swap.
        /// </summary>
        [UnityTest]
        public IEnumerator EachDoorIsOnTheSideItClaims()
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
            Assert.That(
                entrances.Any(d => d.Side == HouseDoorSide.Front),
                Is.True,
                "No front doors at all.");
            Assert.That(
                entrances.Any(d => d.Side == HouseDoorSide.Back),
                Is.True,
                "No back doors, so a house is still a dead end.");

            foreach (HouseDoorway entrance in entrances)
            {
                float wanted =
                    entrance.Side == HouseDoorSide.Back ? -1f : 1f;

                // The trigger hangs off the house, so its local z says which wall
                // it is on.
                Assert.That(
                    entrance.transform.localPosition.z * wanted,
                    Is.GreaterThan(0f),
                    $"{entrance.name} says {entrance.Side} but sits at local z "
                    + $"{entrance.transform.localPosition.z:0.00}.");

                // And the exit puts you back out on the same side. Measured in the
                // house's own space, because a building turned to face the other
                // way has its front at world -Z.
                Transform house = entrance.transform.parent;
                Vector3 outside = house.InverseTransformPoint(
                    entrance.Interior.ExitPositionFor(entrance.Side));
                Assert.That(
                    outside.z * wanted,
                    Is.GreaterThan(0f),
                    $"Going in the {entrance.Side} door of {house.name} and "
                    + "coming out the other side is disorienting in a chase.");
            }
        }

        /// <summary>
        /// Every house has an inside, roofed or not, reachable from both ends.
        ///
        /// Only the roofless variant used to get a door, on the reasoning that a
        /// door on a solid house promises a room that is not there. But the room is
        /// built somewhere else entirely, so the roof decides nothing except whether
        /// the inside is visible from the street — and a town where half the houses
        /// are solid is a town where the thief learns which half to run to.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryHouseHasAnInsideWithBothDoors()
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

            // Grouped by the building they hang off, so a house with two front
            // doors and none at the back cannot pass on count alone.
            var byHouse = entrances
                .GroupBy(door => door.transform.parent)
                .ToArray();
            Assert.That(
                byHouse.Length,
                Is.GreaterThan(10),
                "Not enough houses have doors for this to mean anything.");

            foreach (var group in byHouse)
            {
                Assert.That(
                    group.Select(door => door.Side).Distinct().Count(),
                    Is.EqualTo(2),
                    $"{group.Key.name} does not have both a front and a back "
                    + "door, so it is a dead end.");
            }

            // One room per house, and no two houses sharing one.
            HouseInterior[] rooms = Object
                .FindObjectsByType<HouseInterior>(
                    FindObjectsSortMode.None);
            Assert.That(
                rooms.Length,
                Is.EqualTo(byHouse.Length),
                "Every house needs its own room: sharing one would teleport two "
                + "players into the same space from different streets.");
            Assert.That(
                entrances.Select(door => door.Interior).Distinct().Count(),
                Is.EqualTo(rooms.Length),
                "Two houses lead to the same room.");
        }

        /// <summary>
        /// The room is the model's own furnished interior, not a box.
        ///
        /// The rooms were hand-built greybox until the model was measured and turned
        /// out to already contain a bathroom, a bedroom, a kitchen, a living room and
        /// a dining room behind partition walls. What has to hold is that the
        /// furniture is really there and really solid — a room full of props you walk
        /// straight through is a room with no cover in it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheRoomIsTheModelsFurnishedInterior()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            HouseInterior room =
                Object.FindFirstObjectByType<HouseInterior>();
            Assert.That(room, Is.Not.Null);

            string[] names = room
                .GetComponentsInChildren<Renderer>(true)
                .Select(r => r.name)
                .ToArray();
            foreach (string wanted in new[]
            {
                "IN_Bedroom_Bed_Frame",
                "IN_Kitchen_Fridge",
                "IN_LivingRoom_Sofa_Base",
                "IN_Bathroom_Bathtub",
                "IN_House1F_Wall_Bathroom_Back"
            })
            {
                Assert.That(
                    names,
                    Has.Member(wanted),
                    $"The room has no {wanted}, so it is not the model's "
                    + "interior.");
            }

            // Solid, and in the right proportion. A partition wall gets a mesh
            // collider so its doorway stays a doorway; furniture gets one box per
            // piece rather than one per slat.
            int walls = room
                .GetComponentsInChildren<MeshCollider>(true)
                .Length;
            int furniture = room
                .GetComponentsInChildren<BoxCollider>(true)
                .Count(box => box.name.StartsWith("IN_"));
            // Four exterior walls and the foundation. The partitions are not among
            // them any more: they are rebuilt as 2 m greybox with their own boxes, so
            // that the camera can see over every one of them at once.
            Assert.That(
                walls,
                Is.GreaterThanOrEqualTo(4),
                "The shell of the room is not solid.");

            BoxCollider[] lowWalls = room
                .GetComponentsInChildren<BoxCollider>(true)
                .Where(box => box.name.EndsWith(" Low"))
                .ToArray();
            Assert.That(
                lowWalls.Length,
                Is.GreaterThanOrEqualTo(4),
                "The partitions were not rebuilt, so the rooms are not divided.");
            foreach (BoxCollider lowWall in lowWalls)
            {
                Assert.That(
                    lowWall.bounds.size.y,
                    Is.LessThan(2.6f),
                    $"{lowWall.name} is {lowWall.bounds.size.y:0.0} m tall. Over "
                    + "about two it starts blocking the camera again, which is "
                    + "the whole thing this was for.");
            }

            // And what you walk into is what you see: the original full-height
            // partition must be switched off, or there is an invisible wall above
            // the visible one for throws to hit.
            Renderer tallPartition = room
                .GetComponentsInChildren<Renderer>(true)
                .First(r => r.name == "IN_House1F_Wall_Bathroom_Back");
            Assert.That(
                tallPartition.enabled,
                Is.False,
                "The full-height partition is still drawn.");
            Assert.That(
                furniture,
                Is.InRange(10, 45),
                $"{furniture} furniture colliders. Under ten means the room has "
                + "no cover in it; over forty-five means it is boxing every "
                + "chair slat again.");
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
            IHoldInteractable holdInteraction = piece;
            Assert.That(holdInteraction.HoldDurationSeconds, Is.GreaterThan(0f));
            Assert.That(
                holdInteraction.CanBeginHold(new PlayerInteractionContext(thief)),
                Is.True);

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
                holdInteraction.CompleteHold(new PlayerInteractionContext(thief)),
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
