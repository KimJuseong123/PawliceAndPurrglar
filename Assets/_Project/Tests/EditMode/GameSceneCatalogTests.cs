using NUnit.Framework;
using PawsAndLoot.Core;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class GameSceneCatalogTests
    {
        [Test]
        public void BuildOrder_UsesExpectedMvpScenes()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    GameSceneId.Bootstrap,
                    GameSceneId.Game,
                    GameSceneId.Result
                },
                GameSceneCatalog.BuildOrder);
        }

        [TestCase(GameSceneId.Bootstrap, "Bootstrap")]
        [TestCase(GameSceneId.Game, "Game")]
        [TestCase(GameSceneId.Result, "Result")]
        [TestCase(GameSceneId.TechnicalTest, "TechnicalTest")]
        public void GetName_ReturnsCentralizedSceneName(
            GameSceneId sceneId,
            string expectedName)
        {
            Assert.That(GameSceneCatalog.GetName(sceneId), Is.EqualTo(expectedName));
        }
    }
}
