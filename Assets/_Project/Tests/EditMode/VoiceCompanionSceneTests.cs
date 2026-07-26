using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Core;
using PawsAndLoot.UI;
using PawsAndLoot.Voice;
using Unity.AI.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class VoiceCompanionSceneTests
    {
        [Test]
        public void GameSceneContainsBakedVoiceCompanionSystems()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            CompanionAgent[] companions = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<CompanionAgent>(true))
                .ToArray();
            NavMeshSurface surface = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<NavMeshSurface>(true))
                .Single();

            Assert.That(companions, Has.Length.EqualTo(2));
            Assert.That(
                companions.Select(agent => agent.Kind),
                Is.EquivalentTo(
                    new[] { CompanionKind.Dog, CompanionKind.Cat }));
            Assert.That(surface.navMeshData, Is.Not.Null);
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            CompanionCommandDispatcher>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            VoiceCommandController>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(
                scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<
                            VoiceCommandHudPresenter>(true))
                    .ToArray(),
                Has.Length.EqualTo(1));
        }
    }
}
