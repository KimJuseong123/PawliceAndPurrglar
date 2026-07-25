using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
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

        [Test]
        public void PlazaInteractionMarkerDoesNotBlockCrossingRoute()
        {
            GreyboxMapDefinition map = LoadMap();
            GreyboxRouteReference route = map.GetRoute(
                GreyboxMapDefinition.CrossingRouteId);
            PrototypeInteractable marker = Object
                .FindObjectsByType<PrototypeInteractable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(candidate =>
                    candidate.name == "Prototype Plaza Point");
            Vector2 markerPosition = ToPlanar(
                marker.transform.position);
            float closestDistance = float.PositiveInfinity;

            for (int index = 1;
                 index < route.Waypoints.Count;
                 index++)
            {
                closestDistance = Mathf.Min(
                    closestDistance,
                    DistanceToSegment(
                        markerPosition,
                        ToPlanar(route.Waypoints[index - 1].position),
                        ToPlanar(route.Waypoints[index].position)));
            }

            Assert.That(
                closestDistance,
                Is.GreaterThan(1f),
                "Prototype Plaza Point blocks the MAP-001 traversal probe.");
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
