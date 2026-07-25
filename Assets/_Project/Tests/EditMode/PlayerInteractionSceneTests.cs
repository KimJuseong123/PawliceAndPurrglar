using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
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
            ThiefHudPresenter[] thiefPresenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ThiefHudPresenter>(true))
                .ToArray();
            ArrestRangeSensor[] arrestSensors = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestRangeSensor>(true))
                .ToArray();
            ArrestProgressController[] arrestProgressControllers = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestProgressController>(true))
                .ToArray();
            ArrestCompletionController[] arrestCompletionControllers = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ArrestCompletionController>(true))
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
            Assert.That(thiefPresenters, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                thiefPresenters[0].ValidateOrThrow);
            Assert.That(arrestSensors, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(arrestSensors[0].ValidateOrThrow);
            Assert.That(
                arrestProgressControllers,
                Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                arrestProgressControllers[0].ValidateOrThrow);
            Assert.That(
                arrestCompletionControllers,
                Has.Length.EqualTo(1));
            Assert.DoesNotThrow(
                arrestCompletionControllers[0].ValidateOrThrow);
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            LootSaleZone>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            ThiefLootWallet>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
        }
    }
}
