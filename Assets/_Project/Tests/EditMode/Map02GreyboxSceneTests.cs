using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class Map02GreyboxSceneTests
    {
        private const string Map02ScenePath =
            "Assets/_Project/Scenes/GameMap02.unity";
        private const string RoadModelPath =
            "Assets/_Project/Art/Environment/road section 3d model/"
            + "road+section+3d+model.fbx";
        private const string RoadTexturePath =
            "Assets/_Project/Art/Environment/road section 3d model/"
            + "road+section+3d+model.fbm/"
            + "road+section+3d+model_basecolor.jpg";
        private const string RoadDisplayMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02RoadFbx.mat";
        private const string TreeTrunkMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02TreeTrunk.mat";
        private const string TreeCanopyMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02TreeCanopy.mat";

        [Test]
        public void Map02HasRequestedBoundsAndPoliceStart()
        {
            GreyboxMapDefinition map = LoadMap02();

            Assert.That(map, Is.Not.Null);
            Assert.DoesNotThrow(() => map.ValidateOrThrow());
            Assert.That(map.EnvironmentContentCleared, Is.True);
            Assert.That(map.MapWidthMeters, Is.EqualTo(70f));
            Assert.That(map.MapDepthMeters, Is.EqualTo(65f));
            Assert.That(
                map.transform.parent.name,
                Is.EqualTo("MAP-002 Greybox Layout"));

            Transform environment =
                map.transform.parent.Find("Environment");
            Assert.That(environment, Is.Not.Null);
            AssertSurface(
                environment,
                "Ground",
                new Vector3(35f, -0.15f, 32.5f),
                new Vector3(70f, 0.3f, 65f));
            AssertSurface(
                environment,
                "North Boundary",
                new Vector3(35f, 1f, 66f),
                new Vector3(72f, 2f, 1f));
            AssertSurface(
                environment,
                "South Boundary",
                new Vector3(35f, 1f, -1f),
                new Vector3(72f, 2f, 1f));
            AssertSurface(
                environment,
                "West Boundary",
                new Vector3(-1f, 1f, 32.5f),
                new Vector3(1f, 2f, 67f));
            AssertSurface(
                environment,
                "East Boundary",
                new Vector3(71f, 1f, 32.5f),
                new Vector3(1f, 2f, 67f));

            Transform policeSpawn =
                map.GetLocation(GreyboxLocationId.PoliceSpawn);
            var expectedPoliceStart = new Vector3(27.5f, 0f, 29.5f);
            Assert.That(
                Vector3.Distance(
                    policeSpawn.position,
                    expectedPoliceStart),
                Is.LessThan(0.001f));
            PlayerRoleIdentity police = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsInactive.Include)
                .Single(identity => identity.Role == PlayerRole.Police);
            Assert.That(
                Vector3.Distance(
                    police.transform.position,
                    expectedPoliceStart + Vector3.up),
                Is.LessThan(0.001f));
        }

        [Test]
        public void Map02RoadsMatchRequestedCoordinates()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform roadRoot = map.transform.parent
                .Find("Environment/MAP-002 Roads");
            Assert.That(roadRoot, Is.Not.Null);
            Assert.That(roadRoot.childCount, Is.EqualTo(13));

            AssertRoad(
                roadRoot,
                "Top Road",
                new Vector3(33f, 0.02f, 49f),
                new Vector3(66f, 0.04f, 2f));
            AssertRoad(
                roadRoot,
                "Center Road",
                new Vector3(33f, 0.02f, 26f),
                new Vector3(66f, 0.04f, 2f));
            AssertRoad(
                roadRoot,
                "Lower Left Road",
                new Vector3(5.5f, 0.02f, 21f),
                new Vector3(11f, 0.04f, 2f));
            AssertRoad(
                roadRoot,
                "Lower Center Road",
                new Vector3(29.5f, 0.02f, 12f),
                new Vector3(37f, 0.04f, 2f));
            AssertRoad(
                roadRoot,
                "Lower Right Road",
                new Vector3(57f, 0.02f, 13f),
                new Vector3(18f, 0.04f, 2f));
            AssertRoad(
                roadRoot,
                "Top Vertical Road",
                new Vector3(26f, 0.02f, 55f),
                new Vector3(2f, 0.04f, 12f));
            AssertRoad(
                roadRoot,
                "Center Left Vertical Road",
                new Vector3(20f, 0.02f, 37.5f),
                new Vector3(2f, 0.04f, 23f));
            AssertRoad(
                roadRoot,
                "Center Vertical Road",
                new Vector3(35f, 0.02f, 37.5f),
                new Vector3(2f, 0.04f, 23f));
            AssertRoad(
                roadRoot,
                "Center Right Vertical Road",
                new Vector3(49f, 0.02f, 37.5f),
                new Vector3(2f, 0.04f, 23f));
            AssertRoad(
                roadRoot,
                "Lower Left Vertical Road",
                new Vector3(11f, 0.02f, 10.5f),
                new Vector3(2f, 0.04f, 21f));
            AssertRoad(
                roadRoot,
                "Lower Center Left Vertical Road",
                new Vector3(19f, 0.02f, 19f),
                new Vector3(2f, 0.04f, 14f));
            AssertRoad(
                roadRoot,
                "Lower Center Right Vertical Road",
                new Vector3(37f, 0.02f, 19f),
                new Vector3(2f, 0.04f, 14f));
            AssertRoad(
                roadRoot,
                "Lower Right Vertical Road",
                new Vector3(48f, 0.02f, 13f),
                new Vector3(2f, 0.04f, 26f));
        }

        [Test]
        public void Map02BuildingsMatchRequestedCoordinatesAndRotations()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform buildingRoot = map.transform.parent
                .Find("Environment/MAP-002 Buildings");
            Assert.That(buildingRoot, Is.Not.Null);
            Assert.That(buildingRoot.childCount, Is.EqualTo(16));

            AssertBuilding(
                buildingRoot,
                "Top Left Two Storey House",
                7f,
                56f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Top Left Center One Storey House",
                19f,
                55f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Top Right Center Two Storey House",
                33f,
                56f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Top Right One Storey House",
                49f,
                55f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Middle Left Upper One Storey House",
                4.5f,
                43f,
                90f);
            AssertBuilding(
                buildingRoot,
                "Middle Left Lower Two Storey House",
                5.5f,
                33f,
                90f);
            AssertBuilding(
                buildingRoot,
                "Supermarket",
                15f,
                37f,
                90f);
            AssertBuilding(
                buildingRoot,
                "Police Station",
                27.5f,
                37f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Bookstore",
                42f,
                37f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Middle Right Upper Two Storey House",
                57f,
                42.5f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Middle Right Lower One Storey House",
                56f,
                31.5f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Lower Left One Storey House",
                5.5f,
                15f,
                90f);
            AssertBuilding(
                buildingRoot,
                "Lower Right Two Storey House",
                56f,
                19.5f,
                180f);
            AssertBuilding(
                buildingRoot,
                "Bottom Left Two Storey House",
                20f,
                5.5f);
            AssertBuilding(
                buildingRoot,
                "Bottom Center One Storey House",
                32f,
                6.5f);
            AssertBuilding(
                buildingRoot,
                "Bottom Right One Storey House",
                42.5f,
                7f);
        }

        [Test]
        public void Map02HasGreenTrashBinsAtRequestedCoordinates()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform trashBinRoot = map.transform.parent
                .Find("Environment/MAP-002 Trash Bins");
            Assert.That(trashBinRoot, Is.Not.Null);
            Assert.That(trashBinRoot.childCount, Is.EqualTo(7));

            AssertTrashBin(trashBinRoot, 1, 26f, 55f);
            AssertTrashBin(trashBinRoot, 2, 4f, 48f);
            AssertTrashBin(trashBinRoot, 3, 63f, 48f);
            AssertTrashBin(trashBinRoot, 4, 50f, 38f);
            AssertTrashBin(trashBinRoot, 5, 1f, 21f);
            AssertTrashBin(trashBinRoot, 6, 64f, 22f);
            AssertTrashBin(trashBinRoot, 7, 7f, 2f);
        }

        [Test]
        public void Map02TreesAreSparseAndAvoidRoadsAndBuildings()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform environment = map.transform.parent.Find("Environment");
            Transform treeRoot = environment.Find("MAP-002 Trees");
            Transform roadRoot = environment.Find("MAP-002 Roads");
            Transform buildingRoot =
                environment.Find("MAP-002 Buildings");
            Assert.That(treeRoot, Is.Not.Null);
            Assert.That(roadRoot, Is.Not.Null);
            Assert.That(buildingRoot, Is.Not.Null);

            Vector3[] expectedPositions =
            {
                new(12.8f, 0f, 62.4f),
                new(41f, 0f, 57f),
                new(58f, 0f, 56f),
                new(66f, 0f, 62f),
                new(10.5f, 0f, 45f),
                new(65f, 0f, 43f),
                new(65f, 0f, 31f),
                new(14.5f, 0f, 16.5f),
                new(24f, 0f, 18f),
                new(30.5f, 0f, 22f),
                new(33f, 0f, 15.5f),
                new(42f, 0f, 18f),
                new(45f, 0f, 23f),
                new(4f, 0f, 6f),
                new(13.8f, 0f, 5.5f),
                new(26f, 0f, 6.5f),
                new(54f, 0f, 7f),
                new(65f, 0f, 7f)
            };

            Assert.That(treeRoot.childCount, Is.EqualTo(18));
            var treeBounds = new Bounds[expectedPositions.Length];
            for (int index = 0;
                 index < expectedPositions.Length;
                 index++)
            {
                Transform tree =
                    treeRoot.Find($"Tree {index + 1:00}");
                Assert.That(tree, Is.Not.Null);
                Assert.That(
                    Vector3.Distance(
                        tree.position,
                        expectedPositions[index]),
                    Is.LessThan(0.001f));
                Assert.That(tree.childCount, Is.EqualTo(3));
                Assert.That(
                    tree.GetComponentsInChildren<Collider>(true),
                    Has.Length.EqualTo(1));

                Renderer[] renderers =
                    tree.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(3));
                foreach (Renderer renderer in renderers)
                {
                    string expectedMaterialPath =
                        renderer.transform.name == "Trunk"
                            ? TreeTrunkMaterialPath
                            : TreeCanopyMaterialPath;
                    Assert.That(
                        AssetDatabase.GetAssetPath(
                            renderer.sharedMaterial),
                        Is.EqualTo(expectedMaterialPath));
                }

                treeBounds[index] = GetRendererBounds(tree);
                foreach (Transform road in roadRoot)
                {
                    Assert.That(
                        OverlapsOnXZ(
                            treeBounds[index],
                            GetRendererBounds(road)),
                        Is.False,
                        $"{tree.name} overlaps {road.name}.");
                }

                foreach (BoxCollider buildingCollider in
                    buildingRoot.GetComponentsInChildren<BoxCollider>(true))
                {
                    Assert.That(
                        OverlapsOnXZ(
                            treeBounds[index],
                            buildingCollider.bounds),
                        Is.False,
                        $"{tree.name} overlaps "
                        + $"{buildingCollider.transform.parent.name}.");
                }
            }

            for (int first = 0;
                 first < expectedPositions.Length;
                 first++)
            {
                for (int second = first + 1;
                     second < expectedPositions.Length;
                     second++)
                {
                    Assert.That(
                        Vector3.Distance(
                            expectedPositions[first],
                            expectedPositions[second]),
                        Is.GreaterThan(4.5f),
                        $"Tree {first + 1:00} and "
                        + $"Tree {second + 1:00} are too dense.");
                }
            }
        }

        [Test]
        public void Map02AllHousesUseSameLargerOneStoreyModel()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform buildingRoot = map.transform.parent
                .Find("Environment/MAP-002 Buildings");
            string[] houses =
            {
                "Top Left Two Storey House",
                "Top Left Center One Storey House",
                "Top Right Center Two Storey House",
                "Top Right One Storey House",
                "Middle Left Upper One Storey House",
                "Middle Left Lower Two Storey House",
                "Middle Right Upper Two Storey House",
                "Middle Right Lower One Storey House",
                "Lower Left One Storey House",
                "Lower Right Two Storey House",
                "Bottom Left Two Storey House",
                "Bottom Center One Storey House",
                "Bottom Right One Storey House"
            };

            Assert.That(houses, Has.Length.EqualTo(13));
            foreach (string houseName in houses)
            {
                Transform slot = buildingRoot.Find(houseName);
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.childCount, Is.EqualTo(1));

                Transform house = slot.GetChild(0);
                Assert.That(
                    house.name,
                    Is.EqualTo("building_house_1f Anchor"));
                Assert.That(house.childCount, Is.EqualTo(1));
                Assert.That(
                    house.GetChild(0).name,
                    Is.EqualTo("building_house_1f_Model"));

                BoxCollider collider = house.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(
                    collider.size.x,
                    Is.EqualTo(7.5f).Within(0.001f));
                Assert.That(
                    collider.size.z,
                    Is.EqualTo(7.5f).Within(0.001f));
            }
        }

        [Test]
        public void Map02CoordinateGridShowsAxesAndIntersectionCoordinates()
        {
            GreyboxMapDefinition map = LoadMap02();
            Transform gridRoot = map.transform.parent
                .Find("Environment/MAP-002 Coordinate Grid");
            Assert.That(gridRoot, Is.Not.Null);

            Transform lines = gridRoot.Find("Grid Lines");
            Transform axisLabels = gridRoot.Find("Axis Labels");
            Transform coordinateLabels =
                gridRoot.Find("Coordinate Labels");
            Assert.That(lines, Is.Not.Null);
            Assert.That(axisLabels, Is.Not.Null);
            Assert.That(coordinateLabels, Is.Not.Null);
            Assert.That(lines.childCount, Is.EqualTo(137));
            Assert.That(axisLabels.childCount, Is.EqualTo(31));
            Assert.That(coordinateLabels.childCount, Is.EqualTo(36));

            AssertGridLine(
                lines,
                "X Grid 0",
                new Vector3(0f, 0.055f, 32.5f),
                new Vector3(0.06f, 0.008f, 65f));
            AssertGridLine(
                lines,
                "X Grid 1",
                new Vector3(1f, 0.055f, 32.5f),
                new Vector3(0.018f, 0.008f, 65f));
            AssertGridLine(
                lines,
                "Z Grid 65",
                new Vector3(35f, 0.055f, 65f),
                new Vector3(70f, 0.008f, 0.06f));

            AssertGridLabel(
                axisLabels,
                "X Coordinate 70",
                "X=70");
            AssertGridLabel(
                axisLabels,
                "Z Coordinate 65",
                "Z=65");
            AssertGridLabel(
                coordinateLabels,
                "Coordinate (30, 40)",
                "(30,40)");
        }

        [Test]
        public void Map02RemainsOutsideNormalBuildOrder()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(enabledScenes, Does.Not.Contain(Map02ScenePath));
            Assert.That(
                enabledScenes,
                Is.EqualTo(
                    GameSceneCatalog.BuildOrder
                        .Select(GameSceneCatalog.GetPath)
                        .ToArray()));
        }

        private static GreyboxMapDefinition LoadMap02()
        {
            Scene scene = EditorSceneManager.OpenScene(
                Map02ScenePath,
                OpenSceneMode.Single);
            return scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<GreyboxMapDefinition>(true))
                .FirstOrDefault();
        }

        private static void AssertSurface(
            Transform environment,
            string name,
            Vector3 expectedPosition,
            Vector3 expectedScale)
        {
            Transform surface = environment.Find(name);
            Assert.That(surface, Is.Not.Null);
            Assert.That(
                Vector3.Distance(surface.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(surface.localScale, expectedScale),
                Is.LessThan(0.001f));
        }

        private static void AssertRoad(
            Transform roadRoot,
            string name,
            Vector3 expectedPosition,
            Vector3 expectedSize)
        {
            Transform road = roadRoot.Find(name);
            Assert.That(road, Is.Not.Null);
            Assert.That(
                Vector3.Distance(road.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(road.localScale, Is.EqualTo(Vector3.one));
            Assert.That(road.childCount, Is.EqualTo(1));
            Transform model = road.GetChild(0);
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    model.gameObject),
                Is.EqualTo(RoadModelPath));
            Assert.That(
                road.GetComponentsInChildren<Collider>(true),
                Is.Empty);

            foreach (Renderer renderer in
                model.GetComponentsInChildren<Renderer>(true))
            {
                Assert.That(renderer.sharedMaterials, Is.Not.Empty);
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null);
                    Assert.That(
                        AssetDatabase.GetAssetPath(material),
                        Is.EqualTo(RoadDisplayMaterialPath));
                    Assert.That(
                        material.shader.name,
                        Is.EqualTo("Universal Render Pipeline/Lit"));
                    Assert.That(
                        AssetDatabase.GetAssetPath(
                            material.GetTexture("_BaseMap")),
                        Is.EqualTo(RoadTexturePath));
                }
            }

            Bounds bounds = GetRendererBounds(model);
            Assert.That(
                Vector3.Distance(bounds.center, expectedPosition),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Distance(bounds.size, expectedSize),
                Is.LessThan(0.02f));
        }

        private static Bounds GetRendererBounds(Transform root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static bool OverlapsOnXZ(Bounds first, Bounds second)
        {
            return first.min.x < second.max.x
                && first.max.x > second.min.x
                && first.min.z < second.max.z
                && first.max.z > second.min.z;
        }

        private static void AssertGridLine(
            Transform lineRoot,
            string name,
            Vector3 expectedPosition,
            Vector3 expectedScale)
        {
            Transform line = lineRoot.Find(name);
            Assert.That(line, Is.Not.Null);
            Assert.That(
                Vector3.Distance(line.position, expectedPosition),
                Is.LessThan(0.001f));
            Assert.That(
                Vector3.Distance(line.localScale, expectedScale),
                Is.LessThan(0.001f));
            Assert.That(line.GetComponent<Collider>(), Is.Null);
        }

        private static void AssertGridLabel(
            Transform labelRoot,
            string name,
            string expectedText)
        {
            Transform label = labelRoot.Find(name);
            Assert.That(label, Is.Not.Null);
            TextMesh text = label.GetComponent<TextMesh>();
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo(expectedText));
        }

        private static void AssertTrashBin(
            Transform trashBinRoot,
            int index,
            float x,
            float z)
        {
            Transform bin =
                trashBinRoot.Find($"Green Trash Bin {index}");
            Assert.That(bin, Is.Not.Null);
            Assert.That(bin.position.x, Is.EqualTo(x).Within(0.001f));
            Assert.That(bin.position.z, Is.EqualTo(z).Within(0.001f));
            Assert.That(bin.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(bin.childCount, Is.EqualTo(1));

            Renderer[] renderers =
                bin.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Color color = renderer.sharedMaterial.color;
                Assert.That(
                    color.g,
                    Is.GreaterThan(color.r),
                    "Trash-bin material should read green.");
                Assert.That(
                    color.g,
                    Is.GreaterThan(color.b),
                    "Trash-bin material should read green.");
            }
        }

        private static void AssertBuilding(
            Transform buildingRoot,
            string name,
            float x,
            float z,
            float rotationY = 0f)
        {
            Transform slot = buildingRoot.Find(name);
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.childCount, Is.EqualTo(1));
            Transform building = slot.GetChild(0);
            Assert.That(building.position.x, Is.EqualTo(x).Within(0.001f));
            Assert.That(building.position.z, Is.EqualTo(z).Within(0.001f));
            Assert.That(
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        building.eulerAngles.y,
                        rotationY)),
                Is.LessThan(0.001f));
            Assert.That(building.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(
                building.childCount,
                Is.GreaterThan(0),
                $"{name} requires an authored model child.");
        }
    }
}
