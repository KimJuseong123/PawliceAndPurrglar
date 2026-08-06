using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The officer has to be able to tell an empty room from a robbed one.
    ///
    /// They cannot pick any of it up — loot is thief-only and stays that way —
    /// so the only thing a room tells them is whether the thief has been through
    /// it, and from the doorway "nothing here" and "nothing left" look exactly
    /// the same. Every marked interior stocks itself now, which makes the
    /// distinction meaningful: a bare shelf is evidence rather than the normal
    /// state of eight houses out of thirteen.
    /// </summary>
    public sealed class InteriorLootTallyPlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            // The local role is a static that outlives a scene load, so a test
            // that sets it and walks away runs the *next* test as that role
            // (ISSUE-054).
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        [UnityTest]
        public IEnumerator TheOfficerIsToldHowMuchOfARoomIsLeft()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            // Every stocked room, not one: the failure this guards against is a
            // town where most interiors are empty because one room per shop kind
            // claimed the whole supply.
            LootSpotDraw[] stocked = Object
                .FindObjectsByType<LootSpotDraw>(FindObjectsSortMode.None)
                .Where(draw => draw.PieceCount > 0)
                .ToArray();
            Assert.That(
                stocked.Length,
                Is.GreaterThanOrEqualTo(13),
                $"Only {stocked.Length} interiors hold anything. Every marked "
                + "room is supposed to stock itself.");

            foreach (LootSpotDraw draw in stocked)
            {
                var room = draw.GetComponent<HouseInterior>();
                Assert.That(
                    draw.PieceCount,
                    Is.EqualTo(Mathf.Max(1, draw.SpotCount - 3)),
                    $"Interior {room.InteriorId} deals {draw.PieceCount} pieces "
                    + $"over {draw.SpotCount} places. Three places have to stay "
                    + "empty or the room falls the same way every match.");
            }

            HouseInterior stockedRoom = stocked[0].GetComponent<HouseInterior>();
            PlayerInteriorState police = Object
                .FindObjectsByType<PlayerInteriorState>(FindObjectsSortMode.None)
                .First(state =>
                    state.GetComponent<PlayerRoleIdentity>().Role
                        == PlayerRole.Police);

            var tally = Object.FindFirstObjectByType<InteriorLootTallyPresenter>();
            Assert.That(
                tally,
                Is.Not.Null,
                "No room tally on the HUD at all.");

            yield return null;
            Assert.That(
                tally.IsShowing,
                Is.False,
                "The room tally is on screen while the officer is in the street.");

            police.SetInterior(stockedRoom.InteriorId);
            yield return null;
            yield return null;

            Assert.That(
                tally.IsShowing,
                Is.True,
                $"The officer walked into interior {stockedRoom.InteriorId}, "
                + $"which holds {stocked[0].PieceCount} pieces, and the screen "
                + "said nothing about them.");
            Assert.That(
                tally.CurrentText,
                Does.Contain(stocked[0].PieceCount.ToString()),
                $"The tally reads '{tally.CurrentText}' for a room holding "
                + $"{stocked[0].PieceCount} pieces.");

            police.SetInterior(PlayerInteriorState.Outside);
            yield return null;
        }
    }
}
