using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class GreyboxMapSceneTests
    {
        [Test]
        public void GameSceneContainsValidGreyboxMap()
        {
            GreyboxMapDefinition map = LoadMap();

            Assert.That(map, Is.Not.Null);
            Assert.DoesNotThrow(() => map.ValidateOrThrow());
            Assert.That(map.Locations.Count, Is.EqualTo(7));
            Assert.That(map.Rooftops.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(map.Ladders.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(map.TrashBins.Count, Is.GreaterThanOrEqualTo(4));
        }

        [TestCase(GreyboxLocationId.PoliceSpawn, GreyboxLocationId.JewelryStore)]
        [TestCase(GreyboxLocationId.ThiefSpawn, GreyboxLocationId.JewelryStore)]
        [TestCase(GreyboxLocationId.JewelryStore, GreyboxLocationId.RaccoonMarket)]
        [TestCase(GreyboxLocationId.Supermarket, GreyboxLocationId.Bookstore)]
        public void MajorLocationsHaveTwoRoutes(
            GreyboxLocationId first,
            GreyboxLocationId second)
        {
            GreyboxMapDefinition map = LoadMap();

            Assert.That(map, Is.Not.Null);
            Assert.That(
                map.CountRoutesBetween(first, second),
                Is.GreaterThanOrEqualTo(2));
        }

        private static GreyboxMapDefinition LoadMap()
        {
            Scene scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
            return scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<GreyboxMapDefinition>(true))
                .FirstOrDefault();
        }
    }
}
