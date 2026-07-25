using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class PlayerInteractionSceneTests
    {
        [Test]
        public void GameSceneContainsSharedInteractionInfrastructure()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            PlayerInteractionScanner[] scanners = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        PlayerInteractionScanner>(true))
                .ToArray();
            IPlayerInteractable[] targets = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        MonoBehaviour>(true))
                .OfType<IPlayerInteractable>()
                .ToArray();
            CommonHudPresenter[] presenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        CommonHudPresenter>(true))
                .ToArray();

            Assert.That(scanners, Has.Length.EqualTo(2));
            Assert.That(
                targets.Select(target => target.InteractionType),
                Is.EquivalentTo(new[]
                {
                    PlayerInteractionType.Loot,
                    PlayerInteractionType.Sale,
                    PlayerInteractionType.Traversal,
                    PlayerInteractionType.Generic
                }));
            Assert.That(presenters, Has.Length.EqualTo(1));
        }
    }
}
