using System.Collections;
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
                Assert.That(carrier.HeldKind, Is.EqualTo(pickup.Kind));

                // Empty the slot so the next prop is not refused for the only
                // legitimate reason a pickup can fail.
                Assert.That(carrier.TryConsume(out _), Is.True);

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
    }
}
