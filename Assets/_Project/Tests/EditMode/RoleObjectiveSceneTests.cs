using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Tests.EditMode
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
