using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerSharedMovementSceneTests
    {
        [TestCase("Police", PlayerRole.Police)]
        [TestCase("thief", PlayerRole.Thief)]
        [TestCase("invalid", PlayerRole.Police)]
        public void CommandLineRoleParsingUsesFallbackForInvalidValues(
            string argument,
            PlayerRole expected)
        {
            Assert.That(
                LocalPlayerRoleSelector.ResolveRole(
                    new[] { "-playerRole", argument },
                    PlayerRole.Police),
                Is.EqualTo(expected));
        }

        [Test]
        public void GameSceneUsesOneSharedMovementTypeForBothRoles()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            PlayerRoleIdentity[] players = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<PlayerRoleIdentity>(true))
                .ToArray();
            LocalPlayerRoleSelector selector = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<LocalPlayerRoleSelector>(true))
                .Single();

            Assert.That(players, Has.Length.EqualTo(2));
            Assert.That(
                players.Select(player =>
                    player.GetComponent<PlayerMovementMotor>()?.GetType()),
                Is.All.EqualTo(typeof(PlayerMovementMotor)));
            Assert.That(
                players.Select(player =>
                    player.GetComponent<UnityEngine.CharacterController>() != null),
                Is.All.True);
            Assert.That(selector.Bindings, Has.Count.EqualTo(2));
            Assert.That(
                selector.Bindings.Count(binding =>
                    binding.KeyboardInput.IsLocallyControlled),
                Is.EqualTo(1));
        }
    }
}
