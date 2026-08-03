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
            // No back door is asserted any more.
            //
            // Rooms are authored models now and most of them are drawn with one
            // opening. Punching a second hole in a solid wall gave a prompt
            // that led into the middle of a bookcase, so the generator puts a
            // door only where the model has one. A house being a dead end is a
            // decision the design took knowingly, and the emergency exit is
            // what stops a dead end becoming a trap.

            // Where a doorway sits is no longer read off its parent.
            //
            // These used to hang inside the building, so a local Z told you
            // which wall they were on. They hang above it now — a local offset
            // on a model scaled twelvefold put every one of them fifty metres
            // out — so the parent is the town's Buildings node and its local Z
            // says nothing about anything.
            //
            // What still has to hold is that going in and coming out land in
            // the same place, which is checked against the room's own points.
            foreach (HouseDoorway entrance in entrances)
            {
                Assert.That(
                    entrance.Interior,
                    Is.Not.Null,
                    $"{entrance.name} leads nowhere.");
                Assert.That(
                    Vector3.Distance(
                        entrance.transform.position,
                        entrance.Interior.ExitPositionFor(entrance.Side)),
                    Is.LessThan(8f),
                    $"{entrance.name} and the step it puts you back on are at "
                    + "opposite ends of the town.");
            }

            // One room per house, and no two houses sharing one.
            HouseInterior[] rooms = Object
                .FindObjectsByType<HouseInterior>(
                    FindObjectsSortMode.None)
                .Where(room => !room.IsJail)
                .ToArray();
            // As many rooms as there are doors leading in, counting the cell
            // out of both. Sharing a room would teleport two players into the
            // same space from different streets.
            Assert.That(
                rooms.Length,
                Is.EqualTo(
                    entrances.Select(door => door.Interior).Distinct().Count()),
                "Every house needs its own room.");
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
                Object
                .FindObjectsByType<HouseInterior>(FindObjectsSortMode.None)
                .First(room => !room.IsJail);
            Assert.That(room, Is.Not.Null);

            string[] names = room
                .GetComponentsInChildren<Renderer>(true)
                .Select(r => r.name)
                .ToArray();
            // Checked by weight rather than by part name.
            //
            // The list below was the hand-built interior's own object names,
            // and it worked for exactly that one model. Every room since is a
            // single welded mesh with no named parts at all, so the check asked
            // whether the new rooms were the old room and always said no.
            //
            // What it was really guarding is that a room is a furnished
            // interior rather than an empty box, and that is a question about
            // how much geometry is in it.
            // Measured by how much space the drawn geometry fills, not by
            // reading its triangles. The build marks these meshes unreadable to
            // keep them out of memory twice, and asking for indices on one
            // throws.
            Bounds drawn = default;
            bool any = false;
            foreach (Renderer part in
                room.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    drawn = part.bounds;
                    any = true;
                }
                else
                {
                    drawn.Encapsulate(part.bounds);
                }
            }

            Assert.That(any, Is.True, "The room draws nothing at all.");
            Assert.That(
                drawn.size.x * drawn.size.z,
                Is.GreaterThan(100f),
                "A room smaller than ten metres square is a cupboard, not an "
                + "interior.");

            foreach (string wanted in System.Array.Empty<string>())
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
                // One is enough now. The shell used to be four wall colliders
                // built from named parts; a room is a single welded mesh and
                // gets a single mesh collider cut from a coarse copy of itself.
                // What matters is that something is solid, not how many pieces
                // it comes in.
                Is.GreaterThanOrEqualTo(1),
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
                    FindObjectsSortMode.None)
                .Where(room => !room.IsJail)
                .ToArray();
            Assert.That(
                interiors,
                Is.Not.Empty,
                "Without interiors the doors lead nowhere.");

            foreach (HouseInterior interior in interiors)
            {
                // The real houses are 7.95 x 8.00 m, so half of one is 4 m.
                //
                // Five and a half was the old fixed room. Rooms are scaled to a
                // common wall height now and their plans follow the model, so
                // the narrow ones are narrower than that and still twice the
                // house they stand in. The jail is deliberately small.
                Assert.That(
                    interior.FloorHalfExtents.x,
                    Is.GreaterThan(2.5f),
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
