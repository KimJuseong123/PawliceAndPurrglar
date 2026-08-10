using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class RequiredAuthoredAssetValidatorTests
    {
        [Test]
        public void RequiredAuthoredAssetsHaveProductionContracts()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] requiredAssets =
            {
                "Assets/_Project/Art/Characters/police.fbx",
                "Assets/_Project/Art/Characters/dog.fbx",
                "Assets/_Project/Art/Characters/cat.fbx",
                "Assets/_Project/Art/Characters/raccoon.fbx",
                "Assets/_Project/Art/Characters/thief.fbx",
                "Assets/_Project/Art/Buildings/building_bookstore.fbx",
                "Assets/_Project/Art/Buildings/building_house_1f.fbx",
                "Assets/_Project/Art/Buildings/building_house_1f_with_interior.fbx",
                "Assets/_Project/Art/Buildings/building_police_station.fbx",
                "Assets/_Project/Art/Buildings/building_supermarket.fbx"
            };

            foreach (string assetPath in requiredAssets)
            {
                string absolutePath = Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(absolutePath), Is.True, assetPath);
                string firstLine = File.ReadLines(absolutePath).FirstOrDefault()
                    ?? string.Empty;
                Assert.That(
                    firstLine.StartsWith("version https://git-lfs.github.com/spec/v1"),
                    Is.False,
                    $"LFS pointer remains: {assetPath}");
            }
        }

        [Test]
        public void ProductionGameSceneDoesNotReferenceValidationAssets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string scenePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scenes/Game.unity");
            string sceneText = File.ReadAllText(scenePath);
            Assert.That(sceneText, Does.Not.Contain("Generated/Validation"));
            Assert.That(sceneText, Does.Contain("7df0df2c17208144480fcd43b5ce3548"));
            Assert.That(sceneText, Does.Contain("6961fc7c68093c5479876c83455dcc7e"));
            Assert.That(sceneText, Does.Contain("5f520e62575c75e488cf8b2290ad9fef"));
        }
    }
}
