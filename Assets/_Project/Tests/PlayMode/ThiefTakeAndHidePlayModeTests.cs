using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Taking a piece and climbing into a bin, both on E.
    /// </summary>
    public sealed class ThiefTakeAndHidePlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        private static IEnumerator LoadPlayingScene()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;
        }

        private static PlayerRoleIdentity Player(PlayerRole role)
        {
            return Object
                .FindObjectsByType<PlayerRoleIdentity>(FindObjectsSortMode.None)
                .First(identity => identity.Role == role);
        }

        /// <summary>
        /// One press of E used to do nothing at all.
        ///
        /// <c>LootPickupProgress</c> charges a theft by counting the frames the
        /// thief keeps *asking*, and a tap asks once — the attempt lapsed 0.35 s
        /// later and the piece stayed on the shelf, silently, every time. The
        /// piece is a hold interaction now, so the wait it was always supposed to
        /// impose is the wait the player actually performs.
        /// </summary>
        [UnityTest]
        public IEnumerator HoldingEOnAPiecePutsItInTheBagAndTakesItOffTheShelf()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return LoadPlayingScene();

            LootItem piece = Object
                .FindObjectsByType<LootItem>(FindObjectsSortMode.None)
                .Where(item => item.Definition != null && item.IsAvailable)
                .OrderBy(item => item.name)
                .First();

            Assert.That(
                piece.HoldDurationSeconds,
                Is.GreaterThan(0f),
                $"'{piece.name}' is taken by a tap. A tap asks the progress "
                + "component once and it needs asking every frame, so the theft "
                + "lapses and nothing says why.");

            PlayerRoleIdentity thief = Player(PlayerRole.Thief);
            var context = new PlayerInteractionContext(thief);
            Assert.That(
                piece.CanBeginHold(context),
                Is.True,
                "The thief cannot even begin to take an available piece.");

            var bag = thief.GetComponent<LootCarrier>();
            int before = bag.CarriedLoot.Count();
            Assert.That(
                piece.CompleteHold(context),
                Is.True,
                "Finishing the hold did not take the piece.");
            yield return null;

            Assert.That(
                bag.CarriedLoot.Count(),
                Is.EqualTo(before + 1),
                "The piece left the shelf and did not arrive in the bag.");
            Assert.That(
                piece.PresentationRoot.IsChildOf(thief.transform),
                Is.True,
                $"'{piece.name}' is in the bag and its model is still standing "
                + "in the room.");
        }

        /// <summary>
        /// The officer has no business with a piece of treasure, and the refusal
        /// has to be in the piece rather than only in the permission table — the
        /// table is consulted by the scanner, and the host runs interactions the
        /// scanner never ranked.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOfficerCannotTakeAPiece()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            yield return LoadPlayingScene();

            LootItem piece = Object
                .FindObjectsByType<LootItem>(FindObjectsSortMode.None)
                .Where(item => item.Definition != null && item.IsAvailable)
                .OrderBy(item => item.name)
                .First();

            var context = new PlayerInteractionContext(Player(PlayerRole.Police));
            Assert.That(piece.CanBeginHold(context), Is.False);
            Assert.That(piece.CompleteHold(context), Is.False);
            Assert.That(piece.CurrentCarrier, Is.Null);
            Assert.That(
                PlayerRolePermissions.CanInteract(
                    PlayerRole.Police,
                    PlayerInteractionType.Loot),
                Is.False,
                "The permission table lets the officer at the treasure.");
        }

        /// <summary>
        /// Hiding has to do all three things: stop the thief moving, stop them
        /// being drawn, and stop them being arrested. Any two without the third
        /// is a picture of a bug — invisible but arrestable is an officer waving
        /// at nothing; unarrestable but drawn is a thief standing in the open
        /// that nobody can touch.
        /// </summary>
        [UnityTest]
        public IEnumerator TheThiefClimbsIntoABinAndOutOfReach()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return LoadPlayingScene();

            PlayerHidingSpot[] spots = Object
                .FindObjectsByType<PlayerHidingSpot>(FindObjectsSortMode.None);
            Assert.That(
                spots.Length,
                Is.GreaterThanOrEqualTo(4),
                "The town has fewer bins to hide in than it has bins.");

            PlayerHidingSpot spot = spots
                .OrderBy(candidate => candidate.transform.position.x)
                .First();
            PlayerRoleIdentity thief = Player(PlayerRole.Thief);
            var hiding = thief.GetComponent<ThiefHidingState>();
            Assert.That(hiding, Is.Not.Null, "The thief cannot hide at all.");

            var motor = thief.GetComponent<PlayerMovementMotor>();
            Assert.That(motor.CanMove, Is.True, "The thief starts unable to move.");

            Assert.That(
                spot.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "E at an empty bin did nothing.");
            yield return null;

            Assert.That(hiding.IsHiding, Is.True);
            Assert.That(spot.IsOccupied, Is.True);
            Assert.That(
                motor.CanMove,
                Is.False,
                "A thief inside a bin can still walk around in it.");

            // Only what is actually on screen. The stun stars live on a switched
            // off object with their renderers left enabled, so counting `enabled`
            // alone reports four visible pieces of a thief nobody can see.
            Renderer[] drawn = thief
                .GetComponentsInChildren<Renderer>(true)
                .Where(piece => piece.enabled && piece.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(
                drawn,
                Is.Empty,
                $"{drawn.Length} of the hidden thief's renderers are still on, "
                + $"first '{drawn.FirstOrDefault()?.name}'.");

            // Standing on top of them is not an arrest.
            var sensor = Object.FindFirstObjectByType<ArrestRangeSensor>();
            PlayerRoleIdentity police = Player(PlayerRole.Police);
            var policeController = police.GetComponent<CharacterController>();
            if (policeController != null)
            {
                policeController.enabled = false;
            }

            police.transform.position =
                thief.transform.position + new Vector3(0.5f, 0f, 0f);
            Physics.SyncTransforms();
            sensor.Evaluate();
            Assert.That(
                sensor.IsTargetDetected,
                Is.False,
                "The officer standing on the bin has the hidden thief in reach.");

            // And out again on the same key.
            Assert.That(
                spot.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                "E did not get the thief back out of the bin.");
            yield return null;

            Assert.That(hiding.IsHiding, Is.False);
            Assert.That(spot.IsOccupied, Is.False);
            Assert.That(motor.CanMove, Is.True);
            Assert.That(
                thief.GetComponentsInChildren<Renderer>(true)
                    .Any(piece => piece.enabled
                        && piece.gameObject.activeInHierarchy),
                Is.True,
                "The thief climbed out of the bin invisible.");
        }
    }
}
