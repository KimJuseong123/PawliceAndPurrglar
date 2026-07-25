using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class RoleObjectiveSceneTests
    {
        [Test]
        public void GameSceneContainsOneRoleObjectivePresenter()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            RoleObjectivePresenter[] presenters = scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<
                        RoleObjectivePresenter>(true))
                .ToArray();

            Assert.That(presenters, Has.Length.EqualTo(1));
        }
    }
}
