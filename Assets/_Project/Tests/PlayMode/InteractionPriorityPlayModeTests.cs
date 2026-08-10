using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// Standing on a treasure and pressing the key takes the treasure.
    ///
    /// The scanner picks whatever interactable is nearest, and opening every house
    /// at both ends put 38 door triggers into a town that had 9. A door that
    /// out-competes the thing the player is standing on would be a chase-losing
    /// surprise, and it is invisible from the outside: the press does something, just
    /// not the something you wanted.
    /// </summary>
    public sealed class InteractionPriorityPlayModeTests
    {
        [UnityTest]
        public IEnumerator ADoorDoesNotStealAPressMeantForATreasure()
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
            var scanner = thief.GetComponent<PlayerInteractionScanner>();
            var controller = thief.GetComponent<CharacterController>();
            Assert.That(scanner, Is.Not.Null);

            LootItem[] treasures = Object.FindObjectsByType<LootItem>(
                FindObjectsSortMode.None);
            Assert.That(
                treasures,
                Is.Not.Empty,
                "No treasures, so this proves nothing.");

            foreach (LootItem treasure in treasures)
            {
                // Beside it, exactly as the two-process probe places the thief.
                float lift = controller != null
                    ? controller.height * 0.5f - controller.center.y
                    : 0f;
                thief.transform.position =
                    treasure.transform.position
                    + new Vector3(1f, lift, 0f);
                Physics.SyncTransforms();
                yield return null;

                scanner.RefreshTarget();
                IPlayerInteractable target = scanner.CurrentTarget;
                Assert.That(
                    target,
                    Is.Not.Null,
                    $"Standing next to '{treasure.name}' the thief has nothing "
                    + "to interact with at all.");

                var stolenBy = target as HouseDoorway;
                Assert.That(
                    stolenBy,
                    Is.Null,
                    $"Standing next to '{treasure.name}', the press goes to "
                    + $"'{stolenBy?.name}' instead — a door "
                    + $"{Vector3.Distance(stolenBy != null ? stolenBy.transform.position : Vector3.zero, thief.transform.position):0.00} m "
                    + "away wins over the treasure underfoot.");

                // The treasure itself, or the glass in front of that same
                // treasure. A sealed case winning is the design — the piece
                // behind it is out of reach until the glass goes, and the press
                // has to reach the glass. What must never happen is the press
                // landing on some third thing, or on a case guarding a
                // different piece two shops away.
                var guardedBy = target as LootDisplayCase;
                if (guardedBy != null)
                {
                    Assert.That(
                        guardedBy.Contents,
                        Is.SameAs(treasure),
                        $"Standing next to '{treasure.name}' the press goes to "
                        + $"a case holding "
                        + $"'{guardedBy.Contents?.name ?? "nothing"}'.");
                    Assert.That(
                        guardedBy.IsSealed,
                        Is.True,
                        $"An open case should stop answering and let "
                        + $"'{treasure.name}' be picked up.");
                    continue;
                }

                Assert.That(
                    target,
                    Is.InstanceOf<LootItem>(),
                    $"Standing next to '{treasure.name}' the press goes to "
                    + $"'{(target as MonoBehaviour)?.name}'.");
            }
        }
    }
}
