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
    /// Walk up to a rock in the real Game scene and pick it up.
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
        public IEnumerator EveryRockInTheSceneCanBePickedUp()
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

            // The thief, because a rock is meant to be usable by either side and
            // the thief is the one the loot rules restrict.
            PlayerRoleIdentity player = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(identity => identity.Role == PlayerRole.Thief);
            var scanner = player.GetComponent<PlayerInteractionScanner>();
            var carrier = player.GetComponent<ToolCarrier>();
            CharacterController controller =
                player.GetComponent<CharacterController>();
            Assert.That(scanner, Is.Not.Null);
            Assert.That(carrier, Is.Not.Null);

            foreach (ThrowablePickup pickup in pickups)
            {
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

                // Empty the slot so the next rock is not refused for the only
                // legitimate reason a pickup can fail.
                Assert.That(carrier.TryConsume(out _), Is.True);
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
