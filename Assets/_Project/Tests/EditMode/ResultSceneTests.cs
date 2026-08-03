using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class ResultSceneTests
    {
        [Test]
        public void ResultSceneContainsSummaryAndNavigation()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Result),
                OpenSceneMode.Single);
            ResultScreenPresenter[] presenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ResultScreenPresenter>(true))
                .ToArray();
            SceneNavigationButton[] buttons = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        SceneNavigationButton>(true))
                .ToArray();
            ApplicationQuitButton[] quitButtons = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        ApplicationQuitButton>(true))
                .ToArray();

            Assert.That(presenters, Has.Length.EqualTo(1));
            Assert.DoesNotThrow(presenters[0].ValidateOrThrow);
            Assert.That(
                buttons.Select(button => button.TargetScene),
                Is.EquivalentTo(new[]
                {
                    GameSceneId.Game,
                    GameSceneId.Bootstrap
                }));
            Assert.That(quitButtons, Has.Length.EqualTo(1));
        }
    }
}
