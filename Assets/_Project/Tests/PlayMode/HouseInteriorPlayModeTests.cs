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
            Assert.That(
                Vector3.Distance(
                    thief.transform.position,
                    interior.EntryPosition),
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
            Assert.That(
                Vector3.Distance(
                    thief.transform.position,
                    interior.ExitPosition),
                Is.LessThan(1f),
                "Leaving has to put them back in the town.");
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
