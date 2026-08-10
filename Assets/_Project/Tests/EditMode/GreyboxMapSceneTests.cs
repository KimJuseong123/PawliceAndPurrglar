using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Map;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Tests.EditMode
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
            // No rooftops asserted. They were flat plates laid over the old
            // greybox shops; the town's buildings are scanned models with
            // pitched roofs and a plate over one reads as a lid. The ladders
            // stayed — a way up is the part that mattered — and where they
            // arrive is the model's own ridge.
            Assert.That(map.Rooftops, Has.No.Null);
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

        // PlazaInteractionMarkerDoesNotBlockCrossingRoute lived here. It
        // checked that the white "Prototype Plaza Point" cube stood clear of the
        // crossing route. That cube was a prototype marker which counted presses
        // and did nothing else, and it was removed on 2026-08-09 — from the game
        // it read as a pane of glass hanging over the square. With no marker
        // there is no clearance to defend.


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

        private static Vector2 ToPlanar(Vector3 position)
        {
            return new Vector2(position.x, position.z);
        }

        private static float DistanceToSegment(
            Vector2 point,
            Vector2 start,
            Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float ratio = Mathf.Clamp01(
                Vector2.Dot(point - start, segment)
                / segment.sqrMagnitude);
            return Vector2.Distance(
                point,
                start + segment * ratio);
        }
    }
}
