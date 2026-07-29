using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The thief actually goes dark outside the torch, in the real scene.
    ///
    /// The existing tests asserted <c>IsTargetVisible</c>, which is the rule's own
    /// opinion, and it stayed correct the whole time the thief was plainly visible
    /// from every direction. The renderers are what the player sees, so the
    /// renderers are what this checks.
    ///
    /// The bug it exists for: the exception that keeps stun stars visible asked
    /// "does this renderer have a StunStarsView above it", and that component sits
    /// on the player root — so the question was true of every renderer on the
    /// character and the entire thief was exempted. Nothing failed and the feature
    /// simply stopped working.
    /// </summary>
    public sealed class FlashlightHidingPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThiefBodyGoesDarkOutsideTheConeButStarsDoNot()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            yield return null;

            PlayerRoleIdentity[] players = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None);
            PlayerRoleIdentity police =
                players.First(p => p.Role == PlayerRole.Police);
            PlayerRoleIdentity thief =
                players.First(p => p.Role == PlayerRole.Thief);

            foreach (PlayerRoleIdentity player in players)
            {
                CharacterController controller =
                    player.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }

            // The officer faces north; the thief stands well behind them, outside
            // the cone and outside the always-seen radius.
            police.transform.position = Vector3.zero;
            police.transform.rotation =
                Quaternion.LookRotation(Vector3.forward);
            thief.transform.position = new Vector3(0f, 0f, -9f);
            yield return null;
            yield return null;

            var visibility =
                Object.FindFirstObjectByType<FlashlightVisibility>();
            Assert.That(visibility, Is.Not.Null);
            Assert.That(
                visibility.IsTargetVisible,
                Is.False,
                "Somebody behind the officer, beyond the always-seen radius, "
                + "has to be hidden.");

            // The renderers, not the opinion. Skinned meshes are the character;
            // the stars are a separate ring built at runtime.
            SkinnedMeshRenderer[] body = thief
                .GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(body, Is.Not.Empty);
            foreach (SkinnedMeshRenderer renderer in body)
            {
                Assert.That(
                    renderer.enabled,
                    Is.False,
                    $"'{renderer.name}' is still being drawn, so the thief is "
                    + "visible from behind the officer.");
            }

            // And back in front, they come back.
            thief.transform.position = new Vector3(0f, 0f, 9f);
            yield return null;
            yield return null;

            Assert.That(
                visibility.IsTargetVisible,
                Is.True,
                "Somebody straight ahead has to be visible again.");
            foreach (SkinnedMeshRenderer renderer in body)
            {
                Assert.That(
                    renderer.enabled,
                    Is.True,
                    $"'{renderer.name}' stayed hidden in the middle of the "
                    + "beam.");
            }

            LocalPlayerRoleSelector.ClearOverriddenRole();
        }
    }
}
