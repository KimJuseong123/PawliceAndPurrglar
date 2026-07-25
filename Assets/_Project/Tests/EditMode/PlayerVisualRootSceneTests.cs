using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerVisualRootSceneTests
    {
        [Test]
        public void GamePlayersKeepGameplayOnRootAndVisualUnderVisualRoot()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            PlayerVisualRoot[] players = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<PlayerVisualRoot>(true))
                .ToArray();

            Assert.That(players, Has.Length.EqualTo(2));
            foreach (PlayerVisualRoot player in players)
            {
                Assert.DoesNotThrow(player.ValidateOrThrow);
                Assert.That(
                    player.GetComponent<CharacterController>(),
                    Is.Not.Null);
                Assert.That(
                    player.GetComponent<PlayerMovementMotor>(),
                    Is.Not.Null);
                Assert.That(
                    player.GetComponent<PlayerInteractionScanner>(),
                    Is.Not.Null);
                Assert.That(
                    player.GetComponent<NetworkObject>(),
                    Is.Not.Null);
                Assert.That(
                    player.VisualRoot.parent,
                    Is.EqualTo(player.transform));
                Assert.That(
                    player.CurrentVisual.transform.parent,
                    Is.EqualTo(player.VisualRoot));
            }
        }
    }
}
