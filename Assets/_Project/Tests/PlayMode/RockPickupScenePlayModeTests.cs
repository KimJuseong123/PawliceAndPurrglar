using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Walk up to every pickup in the real Game scene and take it.
    ///
    /// Every earlier test asserted a piece of this: the pickup reports the right
    /// interaction type, the permissions let both roles touch it, the spot is not
    /// inside a wall. All of them passed while the rock could not actually be
    /// picked up in the built game, because none of them ran the whole path —
    /// scanner finds it, permission allows it, carrier accepts it — against the
    /// scene as shipped.
    ///
    /// So this one runs exactly what a player does and nothing else.
    /// </summary>
    public sealed class RockPickupScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator EveryPickupInTheSceneCanBeTakenByItsOwner()
        {
            yield return LoadGameScene();

            MatchRuntimeState runtime =
                Object.FindFirstObjectByType<MatchRuntimeState>();
            Assert.That(runtime, Is.Not.Null);
            // Straight to Playing: interaction is gated on the match running and
            // the countdown is not what is under test here.
            runtime.TryTransitionTo(MatchState.Playing);
            Assert.That(runtime.IsGameplayActive, Is.True);

            ThrowablePickup[] pickups = Object
                .FindObjectsByType<ThrowablePickup>(
                    FindObjectsSortMode.None)
                .OrderBy(pickup => pickup.name)
                .ToArray();
            Assert.That(
                pickups.Length,
                Is.GreaterThan(0),
                "The scene has to contain rocks for this to mean anything.");

            PlayerRoleIdentity[] players = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None);

            // Wait for the thief to be dropped at its starting corner before
            // standing anybody anywhere.
            //
            // The thief's start is drawn once, on the frame the match becomes
            // playable, and it teleports them to one of five outskirt corners.
            // A scene test loads with the match already playing, so the draw
            // landed *after* this test had placed the thief on a pickup and
            // moved them thirty-two metres away — where the only thing in reach
            // was the ground, or on an unlucky run their own cat. The test then
            // reported that a shop shelf could not be picked up, which was true
            // and had nothing to do with the shelf.
            foreach (ThiefStartSpawn start in Object
                .FindObjectsByType<ThiefStartSpawn>(FindObjectsSortMode.None))
            {
                for (int frame = 0; frame < 240 && !start.HasPlaced; frame++)
                {
                    yield return null;
                }

                Assert.That(
                    start.HasPlaced,
                    Is.True,
                    "The thief never reached a starting corner, so anywhere "
                    + "this test puts them is liable to be overwritten.");
            }

            foreach (ThrowablePickup pickup in pickups)
            {
                // Whoever the prop belongs to. A shared rock is tried with the
                // thief; the officer's props are tried with the officer, and the
                // wrong role is checked below to be refused.
                PlayerRole owner = pickup.IsRoleRestricted
                    ? pickup.RestrictedTo
                    : PlayerRole.Thief;
                PlayerRoleIdentity player = players
                    .First(identity => identity.Role == owner);
                var scanner =
                    player.GetComponent<PlayerInteractionScanner>();
                var carrier = player.GetComponent<ToolCarrier>();
                CharacterController controller =
                    player.GetComponent<CharacterController>();
                Assert.That(scanner, Is.Not.Null);
                Assert.That(carrier, Is.Not.Null);

                // Stand on it. The controller has to be switched off to be
                // teleported, or it fights the move and lands somewhere else.
                if (controller != null)
                {
                    controller.enabled = false;
                }

                player.transform.position = pickup.transform.position;
                Physics.SyncTransforms();
                yield return null;

                scanner.RefreshTarget();
                Assert.That(
                    scanner.HasTarget,
                    Is.True,
                    $"Standing on {pickup.name} and the scanner sees nothing "
                    + "to interact with.");
                Assert.That(
                    scanner.CurrentTarget,
                    Is.SameAs(pickup),
                    $"Standing on {pickup.name}, the nearest thing to "
                    + $"interact with is '{scanner.CurrentPrompt}' instead. "
                    + "A rock underfoot that loses to something else is a "
                    + "rock the player cannot pick up.");

                Assert.That(
                    scanner.TryInteractCurrent(),
                    Is.True,
                    $"{pickup.name} refused the interact press.");
                Assert.That(
                    carrier.HasTool,
                    Is.True,
                    $"{pickup.name} was interacted with but nothing ended up "
                    + "in hand.");
                // Whichever slot it landed in, not whichever slot is selected.
                //
                // The quick slots stay where the player put them, on purpose: an
                // item that moves the selection back to slot one gets used again
                // by the next press. So a prop is stored in the first free slot
                // and the selection may still be pointing at another one — and
                // five rocks stacking in slot one meant the shop banana went to
                // slot two while HeldKind still read Rock.
                Assert.That(
                    Held(carrier),
                    Does.Contain(pickup.Kind),
                    $"{pickup.name} was taken but no slot holds a "
                    + $"{pickup.Kind}. Slots: "
                    + string.Join(", ", Held(carrier)));

                // Emptied completely, so the next prop is not refused for the
                // only legitimate reason a pickup can fail. Every slot, because
                // props of the same kind stack and the loop takes five rocks.
                Drain(carrier);
                Assert.That(
                    carrier.HasAnyTool,
                    Is.False,
                    "The carrier would not empty, so every pickup after this "
                    + "one is tested against a full bag.");

                // And the other side must not be able to take it.
                if (!pickup.IsRoleRestricted)
                {
                    continue;
                }

                PlayerRoleIdentity intruder = players
                    .First(identity => identity.Role != owner);

                // Emptied first, like the owner's slot above. Otherwise this
                // asks "is the intruder holding anything" and gets an answer
                // about a pickup earlier in the loop rather than about this
                // one — which is what it started reporting the moment the
                // shelf grew past two items.
                ToolCarrier intruderCarrier =
                    intruder.GetComponent<ToolCarrier>();
                while (intruderCarrier.HasTool)
                {
                    Assert.That(intruderCarrier.TryConsume(out _), Is.True);
                }

                Assert.That(
                    pickup.TryInteract(
                        new PlayerInteractionContext(intruder)),
                    Is.False,
                    $"{pickup.name} belongs to {owner} but "
                    + $"{intruder.Role} could take it. That collapses the "
                    + "two kits into one.");
                Assert.That(
                    intruderCarrier.HasTool,
                    Is.False,
                    $"{intruder.Role} came away from {pickup.name} holding "
                    + "something.");
            }
        }

        private static IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
        }

        /// <summary>
        /// Every kind the carrier is holding, across all four slots.
        /// </summary>
        private static List<ThrowableKind> Held(ToolCarrier carrier)
        {
            var kinds = new List<ThrowableKind>();
            int wasSelected = carrier.SelectedSlot;
            for (int slot = 0; slot < 4; slot++)
            {
                if (carrier.SelectSlot(slot) && carrier.HasTool)
                {
                    kinds.Add(carrier.HeldKind);
                }
            }

            carrier.SelectSlot(wasSelected);
            return kinds;
        }

        private static void Drain(ToolCarrier carrier)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                carrier.SelectSlot(slot);
                // Bounded, so a slot that refuses to empty ends the loop rather
                // than the test run.
                for (int guard = 0; guard < 32 && carrier.HasTool; guard++)
                {
                    carrier.TryConsume(out _);
                }
            }
        }
    }
}
