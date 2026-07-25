using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    public static class GreyboxMapSetup
    {
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/PawsAndLootMapGreybox.exe";

        private const string MaterialRoot =
            "Assets/_Project/Materials/Greybox";
        private const string PlayerConfigPath =
            "Assets/_Project/Settings/Configs/PlayerConfig.asset";

        private static readonly Color GroundColor =
            new(0.28f, 0.34f, 0.31f);
        private static readonly Color RoadColor =
            new(0.16f, 0.19f, 0.22f);
        private static readonly Color PlazaColor =
            new(0.48f, 0.49f, 0.45f);
        private static readonly Color PoliceBlue =
            new(0.08f, 0.34f, 0.88f);
        private static readonly Color ThiefRed =
            new(0.9f, 0.17f, 0.1f);
        private static readonly Color MarketGold =
            new(0.95f, 0.62f, 0.12f);

        [MenuItem("Paws & Loot/Setup/Rebuild MAP-001 Greybox Village")]
        public static void CreateGameScene()
        {
            EnsureMaterialFolder();
            Material ground = LoadOrCreateMaterial(
                "Ground",
                GroundColor);
            Material road = LoadOrCreateMaterial(
                "Road",
                RoadColor);
            Material plaza = LoadOrCreateMaterial(
                "Plaza",
                PlazaColor);
            Material wall = LoadOrCreateMaterial(
                "Boundary",
                new Color(0.22f, 0.24f, 0.27f));
            Material supermarket = LoadOrCreateMaterial(
                "Supermarket",
                new Color(0.2f, 0.58f, 0.29f));
            Material bookstore = LoadOrCreateMaterial(
                "Bookstore",
                new Color(0.23f, 0.42f, 0.78f));
            Material jewelry = LoadOrCreateMaterial(
                "JewelryStore",
                new Color(0.67f, 0.34f, 0.72f));
            Material roof = LoadOrCreateMaterial(
                "Roof",
                new Color(0.18f, 0.2f, 0.24f));
            Material ladder = LoadOrCreateMaterial(
                "Ladder",
                new Color(0.9f, 0.68f, 0.18f));
            Material trash = LoadOrCreateMaterial(
                "TrashBin",
                new Color(0.15f, 0.42f, 0.35f));
            Material route = LoadOrCreateMaterial(
                "ValidationRoute",
                new Color(0.1f, 0.95f, 0.95f),
                "Universal Render Pipeline/Unlit");
            Material traversalProbe = LoadOrCreateMaterial(
                "TraversalProbe",
                PoliceBlue);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            CreateCameraAndLighting();

            var villageRoot = new GameObject("MAP-001 Greybox Village");
            Transform environmentRoot =
                CreateChild("Environment", villageRoot.transform);
            Transform roadsRoot =
                CreateChild("Roads and Alleys", villageRoot.transform);
            Transform buildingsRoot =
                CreateChild("Buildings", villageRoot.transform);
            Transform featuresRoot =
                CreateChild("Traversal Features", villageRoot.transform);
            Transform locationsRoot =
                CreateChild("Location Anchors", villageRoot.transform);
            Transform routesRoot =
                CreateChild("Route Network", villageRoot.transform);

            CreateGroundAndBoundaries(
                environmentRoot,
                ground,
                wall);
            CreateRoadNetwork(roadsRoot, road, plaza);

            var rooftops = new List<Transform>();
            var ladders = new List<Transform>();
            CreateStore(
                "Supermarket",
                new Vector3(-9f, 0f, 6f),
                supermarket,
                roof,
                ladder,
                buildingsRoot,
                featuresRoot,
                LadderSide.West,
                rooftops,
                ladders);
            CreateStore(
                "Bookstore",
                new Vector3(9f, 0f, 6f),
                bookstore,
                roof,
                ladder,
                buildingsRoot,
                featuresRoot,
                LadderSide.East,
                rooftops,
                ladders);
            CreateStore(
                "Jewelry Store",
                new Vector3(9f, 0f, -6f),
                jewelry,
                roof,
                ladder,
                buildingsRoot,
                featuresRoot,
                LadderSide.East,
                rooftops,
                ladders);
            CreateRaccoonMarket(
                buildingsRoot,
                MarketGold,
                wall);
            CreateCentralPlaza(featuresRoot, plaza, wall);

            List<Transform> trashBins = CreateTrashBins(
                featuresRoot,
                trash);
            Dictionary<GreyboxLocationId, Transform> locations =
                CreateLocationAnchors(
                    locationsRoot,
                    PoliceBlue,
                    ThiefRed,
                    MarketGold);
            List<GreyboxRouteReference> routes =
                CreateRoutes(routesRoot, locations);

            var mapObject = new GameObject("Greybox Map Definition");
            mapObject.transform.SetParent(villageRoot.transform);
            GreyboxMapDefinition map =
                mapObject.AddComponent<GreyboxMapDefinition>();
            map.Configure(
                56f,
                44f,
                CreateLocationReferences(locations),
                routes,
                rooftops,
                ladders,
                trashBins);
            CreateRolePreviewMarkers(
                map,
                locationsRoot,
                PoliceBlue,
                ThiefRed);

            CreateRouteLine(
                map.GetRoute(GreyboxMapDefinition.CrossingRouteId),
                route,
                routesRoot);
            CreateTraversalProbe(
                map,
                LoadPlayerMoveSpeed(),
                traversalProbe,
                villageRoot.transform);
            CreateSceneInterface();

            string scenePath = GameSceneCatalog.GetPath(GameSceneId.Game);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save MAP-001 scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
            Debug.Log("MAP-001 greybox village created and validated.");
        }

        [MenuItem("Paws & Loot/Setup/Validate MAP-001 Greybox Village")]
        public static void ValidateScene()
        {
            string scenePath = GameSceneCatalog.GetPath(GameSceneId.Game);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    $"MAP-001 Game scene is missing: {scenePath}",
                    scenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            GreyboxMapDefinition map =
                FindInScene<GreyboxMapDefinition>(scene);
            GreyboxTraversalProbe probe =
                FindInScene<GreyboxTraversalProbe>(scene);
            Camera camera = FindInScene<Camera>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            EventSystem eventSystem = FindInScene<EventSystem>(scene);
            SceneNavigationButton navigation =
                FindInScene<SceneNavigationButton>(scene);

            if (map == null
                || probe == null
                || camera == null
                || camera.orthographic
                || canvas == null
                || eventSystem == null
                || navigation == null
                || navigation.TargetScene != GameSceneId.Result)
            {
                throw new InvalidOperationException(
                    "MAP-001 scene requires its map, traversal probe, perspective camera, UI, and Result navigation.");
            }

            map.ValidateOrThrow();
            if (map.Locations.Count != 7
                || map.Rooftops.Count < 3
                || map.Ladders.Count < 3
                || map.TrashBins.Count < 4
                || map.Routes.Count < 9)
            {
                throw new InvalidOperationException(
                    "MAP-001 scene is missing required locations, routes, rooftops, ladders, or trash bins.");
            }
        }

        [MenuItem("Paws & Loot/Technical Validation/Build Windows MAP-001")]
        public static void BuildWindows()
        {
            CreateGameScene();

            string absoluteBuildPath = Path.GetFullPath(WindowsBuildPath);
            string directory = Path.GetDirectoryName(absoluteBuildPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Could not resolve build directory for '{absoluteBuildPath}'.");
            }

            Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[]
                    {
                        GameSceneCatalog.GetPath(GameSceneId.Game)
                    },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"MAP-001 Windows build failed with {report.summary.totalErrors} errors.");
            }
        }

        private static void CreateCameraAndLighting()
        {
            var cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 39f, -36f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.22f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;

            var lightObject = new GameObject(
                "Directional Light",
                typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;

            var fillObject = new GameObject("Fill Light", typeof(Light));
            fillObject.transform.position = new Vector3(0f, 16f, -8f);
            Light fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 50f;
            fill.intensity = 20f;
            fill.color = new Color(0.7f, 0.82f, 1f);
        }

        private static void CreateGroundAndBoundaries(
            Transform parent,
            Material ground,
            Material boundary)
        {
            CreateCube(
                "Ground",
                new Vector3(0f, -0.15f, 0f),
                new Vector3(56f, 0.3f, 44f),
                ground,
                parent,
                false);

            CreateCube(
                "North Boundary",
                new Vector3(0f, 1f, 22f),
                new Vector3(56f, 2f, 1f),
                boundary,
                parent,
                true);
            CreateCube(
                "South Boundary",
                new Vector3(0f, 1f, -22f),
                new Vector3(56f, 2f, 1f),
                boundary,
                parent,
                true);
            CreateCube(
                "West Boundary",
                new Vector3(-28f, 1f, 0f),
                new Vector3(1f, 2f, 44f),
                boundary,
                parent,
                true);
            CreateCube(
                "East Boundary",
                new Vector3(28f, 1f, 0f),
                new Vector3(1f, 2f, 44f),
                boundary,
                parent,
                true);
        }

        private static void CreateRoadNetwork(
            Transform parent,
            Material road,
            Material plaza)
        {
            CreateFlatTile(
                "Central East-West Road",
                new Vector3(0f, 0.02f, 0f),
                new Vector3(56f, 0.04f, 4f),
                road,
                parent);
            CreateFlatTile(
                "North Loop Road",
                new Vector3(0f, 0.025f, 12f),
                new Vector3(50f, 0.05f, 4f),
                road,
                parent);
            CreateFlatTile(
                "South Loop Road",
                new Vector3(0f, 0.025f, -12f),
                new Vector3(50f, 0.05f, 4f),
                road,
                parent);
            CreateFlatTile(
                "North Outer Alley",
                new Vector3(0f, 0.03f, 18f),
                new Vector3(50f, 0.06f, 3f),
                road,
                parent);
            CreateFlatTile(
                "South Outer Alley",
                new Vector3(0f, 0.03f, -18f),
                new Vector3(50f, 0.06f, 3f),
                road,
                parent);

            foreach (float x in new[] { -24f, -18f, 0f, 18f, 24f })
            {
                CreateFlatTile(
                    $"Vertical Route {x:0}",
                    new Vector3(x, 0.035f, 0f),
                    new Vector3(x == 0f || Mathf.Abs(x) == 18f ? 4f : 3f, 0.07f, 40f),
                    road,
                    parent);
            }

            CreateFlatTile(
                "Central Plaza",
                new Vector3(0f, 0.08f, 0f),
                new Vector3(8f, 0.12f, 8f),
                plaza,
                parent);
        }

        private static void CreateStore(
            string name,
            Vector3 center,
            Material bodyMaterial,
            Material roofMaterial,
            Material ladderMaterial,
            Transform buildingsRoot,
            Transform featuresRoot,
            LadderSide ladderSide,
            ICollection<Transform> rooftops,
            ICollection<Transform> ladders)
        {
            Transform root = CreateChild(name, buildingsRoot);
            CreateCube(
                $"{name} Body",
                center + Vector3.up * 1.8f,
                new Vector3(12f, 3.6f, 8f),
                bodyMaterial,
                root,
                true);
            GameObject roof = CreateCube(
                $"{name} Rooftop",
                center + Vector3.up * 3.9f,
                new Vector3(12.8f, 0.6f, 8.8f),
                roofMaterial,
                root,
                true);
            rooftops.Add(roof.transform);

            Vector3 ladderPosition = ladderSide == LadderSide.West
                ? center + new Vector3(-6.45f, 0f, 0f)
                : center + new Vector3(6.45f, 0f, 0f);
            Transform ladder = CreateLadder(
                $"{name} Ladder",
                ladderPosition,
                ladderMaterial,
                featuresRoot);
            ladders.Add(ladder);

            Vector3 labelPosition =
                center + new Vector3(0f, 4.8f, -4.55f);
            CreateWorldLabel(
                $"{name} Label",
                name.ToUpperInvariant(),
                labelPosition,
                root);
        }

        private static void CreateRaccoonMarket(
            Transform parent,
            Color accentColor,
            Material boundaryMaterial)
        {
            Transform marketRoot =
                CreateChild("Raccoon Trading Yard", parent);
            Material accent = LoadOrCreateMaterial(
                "RaccoonMarket",
                accentColor);

            CreateFlatTile(
                "Trading Yard Floor",
                new Vector3(-9f, 0.08f, -6f),
                new Vector3(11f, 0.12f, 7f),
                accent,
                marketRoot);
            CreateCube(
                "West Yard Fence",
                new Vector3(-15f, 0.7f, -6f),
                new Vector3(0.35f, 1.4f, 8f),
                boundaryMaterial,
                marketRoot,
                true);
            CreateCube(
                "East Yard Fence",
                new Vector3(-4f, 0.7f, -6f),
                new Vector3(0.35f, 1.4f, 8f),
                boundaryMaterial,
                marketRoot,
                true);
            CreateCube(
                "North Yard Fence",
                new Vector3(-9f, 0.7f, -2f),
                new Vector3(12f, 1.4f, 0.35f),
                boundaryMaterial,
                marketRoot,
                true);

            CreateWorldLabel(
                "Raccoon Market Label",
                "RACCOON MARKET",
                new Vector3(-9f, 2.2f, -9.7f),
                marketRoot);
        }

        private static void CreateCentralPlaza(
            Transform parent,
            Material plazaMaterial,
            Material obstacleMaterial)
        {
            GameObject fountain = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            fountain.name = "Plaza Fountain";
            fountain.transform.SetParent(parent);
            fountain.transform.position = new Vector3(0f, 0.45f, 0f);
            fountain.transform.localScale = new Vector3(1.25f, 0.45f, 1.25f);
            fountain.GetComponent<Renderer>().sharedMaterial = plazaMaterial;
            fountain.AddComponent<GreyboxObstacle>();

            GameObject centerpiece = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            centerpiece.name = "Fountain Centerpiece";
            centerpiece.transform.SetParent(parent);
            centerpiece.transform.position = new Vector3(0f, 1.4f, 0f);
            centerpiece.transform.localScale = Vector3.one * 0.7f;
            centerpiece.GetComponent<Renderer>().sharedMaterial =
                obstacleMaterial;
            UnityEngine.Object.DestroyImmediate(
                centerpiece.GetComponent<Collider>());
        }

        private static List<Transform> CreateTrashBins(
            Transform parent,
            Material material)
        {
            var result = new List<Transform>();
            Vector3[] positions =
            {
                new(-20f, 0f, 8f),
                new(20f, 0f, 8f),
                new(-20f, 0f, -8f),
                new(20f, 0f, -8f)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                GameObject bin = CreateCube(
                    $"Trash Bin {index + 1}",
                    positions[index] + Vector3.up * 0.65f,
                    new Vector3(1f, 1.3f, 1f),
                    material,
                    parent,
                    true);
                result.Add(bin.transform);
            }

            return result;
        }

        private static Dictionary<GreyboxLocationId, Transform>
            CreateLocationAnchors(
                Transform parent,
                Color policeColor,
                Color thiefColor,
                Color marketColor)
        {
            var result = new Dictionary<GreyboxLocationId, Transform>();
            AddLocation(
                result,
                parent,
                GreyboxLocationId.PoliceSpawn,
                new Vector3(-24f, 0f, 0f),
                policeColor);
            AddLocation(
                result,
                parent,
                GreyboxLocationId.ThiefSpawn,
                new Vector3(24f, 0f, -18f),
                thiefColor);
            AddLocation(
                result,
                parent,
                GreyboxLocationId.Supermarket,
                new Vector3(-9f, 0f, 0f),
                new Color(0.2f, 0.75f, 0.35f));
            AddLocation(
                result,
                parent,
                GreyboxLocationId.Bookstore,
                new Vector3(9f, 0f, 0f),
                new Color(0.25f, 0.55f, 1f));
            AddLocation(
                result,
                parent,
                GreyboxLocationId.JewelryStore,
                new Vector3(9f, 0f, -12f),
                new Color(0.85f, 0.35f, 0.9f));
            AddLocation(
                result,
                parent,
                GreyboxLocationId.RaccoonMarket,
                new Vector3(-9f, 0f, -12f),
                marketColor);
            AddLocation(
                result,
                parent,
                GreyboxLocationId.CentralPlaza,
                new Vector3(0f, 0f, -3f),
                Color.white);
            return result;
        }

        private static List<GreyboxRouteReference> CreateRoutes(
            Transform parent,
            IReadOnlyDictionary<GreyboxLocationId, Transform> locations)
        {
            var routes = new List<GreyboxRouteReference>
            {
                CreateRoute(
                    parent,
                    "PoliceToJewelry_Plaza",
                    GreyboxLocationId.PoliceSpawn,
                    GreyboxLocationId.JewelryStore,
                    3f,
                    locations,
                    new Vector3(-24f, 0f, 0f),
                    new Vector3(-18f, 0f, 0f),
                    new Vector3(-1.5f, 0f, 0f),
                    new Vector3(-1.5f, 0f, -3f),
                    new Vector3(0f, 0f, -3f),
                    new Vector3(0f, 0f, -12f),
                    new Vector3(9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "PoliceToJewelry_SouthLoop",
                    GreyboxLocationId.PoliceSpawn,
                    GreyboxLocationId.JewelryStore,
                    3f,
                    locations,
                    new Vector3(-24f, 0f, 0f),
                    new Vector3(-24f, 0f, -18f),
                    new Vector3(18f, 0f, -18f),
                    new Vector3(18f, 0f, -12f),
                    new Vector3(9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "ThiefToJewelry_East",
                    GreyboxLocationId.ThiefSpawn,
                    GreyboxLocationId.JewelryStore,
                    3f,
                    locations,
                    new Vector3(24f, 0f, -18f),
                    new Vector3(18f, 0f, -18f),
                    new Vector3(18f, 0f, -12f),
                    new Vector3(9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "ThiefToJewelry_Central",
                    GreyboxLocationId.ThiefSpawn,
                    GreyboxLocationId.JewelryStore,
                    3f,
                    locations,
                    new Vector3(24f, 0f, -18f),
                    new Vector3(24f, 0f, 0f),
                    new Vector3(18f, 0f, 0f),
                    new Vector3(18f, 0f, -12f),
                    new Vector3(9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "JewelryToMarket_SouthRoad",
                    GreyboxLocationId.JewelryStore,
                    GreyboxLocationId.RaccoonMarket,
                    4f,
                    locations,
                    new Vector3(9f, 0f, -12f),
                    new Vector3(0f, 0f, -12f),
                    new Vector3(-9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "JewelryToMarket_PlazaLoop",
                    GreyboxLocationId.JewelryStore,
                    GreyboxLocationId.RaccoonMarket,
                    3f,
                    locations,
                    new Vector3(9f, 0f, -12f),
                    new Vector3(18f, 0f, -12f),
                    new Vector3(18f, 0f, 0f),
                    new Vector3(1.5f, 0f, 0f),
                    new Vector3(1.5f, 0f, -3f),
                    new Vector3(-1.5f, 0f, -3f),
                    new Vector3(-1.5f, 0f, 0f),
                    new Vector3(-18f, 0f, 0f),
                    new Vector3(-18f, 0f, -12f),
                    new Vector3(-9f, 0f, -12f)),
                CreateRoute(
                    parent,
                    "SupermarketToBookstore_Front",
                    GreyboxLocationId.Supermarket,
                    GreyboxLocationId.Bookstore,
                    3f,
                    locations,
                    new Vector3(-9f, 0f, 0f),
                    new Vector3(-1.5f, 0f, 0f),
                    new Vector3(-1.5f, 0f, -3f),
                    new Vector3(1.5f, 0f, -3f),
                    new Vector3(1.5f, 0f, 0f),
                    new Vector3(9f, 0f, 0f)),
                CreateRoute(
                    parent,
                    "SupermarketToBookstore_NorthLoop",
                    GreyboxLocationId.Supermarket,
                    GreyboxLocationId.Bookstore,
                    3f,
                    locations,
                    new Vector3(-9f, 0f, 0f),
                    new Vector3(-18f, 0f, 0f),
                    new Vector3(-18f, 0f, 12f),
                    new Vector3(18f, 0f, 12f),
                    new Vector3(18f, 0f, 0f),
                    new Vector3(9f, 0f, 0f)),
                CreateRoute(
                    parent,
                    GreyboxMapDefinition.CrossingRouteId,
                    GreyboxLocationId.PoliceSpawn,
                    GreyboxLocationId.ThiefSpawn,
                    3f,
                    locations,
                    new Vector3(-24f, 0f, 0f),
                    new Vector3(-18f, 0f, 0f),
                    new Vector3(-1.5f, 0f, 0f),
                    new Vector3(-1.5f, 0f, -3f),
                    new Vector3(1.5f, 0f, -3f),
                    new Vector3(1.5f, 0f, 0f),
                    new Vector3(18f, 0f, 0f),
                    new Vector3(18f, 0f, -18f),
                    new Vector3(24f, 0f, -18f))
            };
            return routes;
        }

        private static GreyboxRouteReference CreateRoute(
            Transform parent,
            string routeId,
            GreyboxLocationId from,
            GreyboxLocationId to,
            float minimumClearWidth,
            IReadOnlyDictionary<GreyboxLocationId, Transform> locations,
            params Vector3[] positions)
        {
            if (positions.Length < 2)
            {
                throw new ArgumentException(
                    "A greybox route requires at least two positions.",
                    nameof(positions));
            }

            positions[0] = locations[from].position;
            positions[positions.Length - 1] = locations[to].position;
            Transform routeRoot = CreateChild(routeId, parent);
            var waypoints = new List<Transform>();
            for (int index = 0; index < positions.Length; index++)
            {
                Transform waypoint = CreateChild(
                    $"Waypoint {index:00}",
                    routeRoot);
                waypoint.position = positions[index];
                waypoints.Add(waypoint);
            }

            return new GreyboxRouteReference(
                routeId,
                from,
                to,
                minimumClearWidth,
                waypoints);
        }

        private static IEnumerable<GreyboxLocationReference>
            CreateLocationReferences(
                IReadOnlyDictionary<GreyboxLocationId, Transform> locations)
        {
            foreach (KeyValuePair<GreyboxLocationId, Transform> pair
                     in locations)
            {
                yield return new GreyboxLocationReference(
                    pair.Key,
                    pair.Value);
            }
        }

        private static void CreateRolePreviewMarkers(
            GreyboxMapDefinition map,
            Transform parent,
            Color policeColor,
            Color thiefColor)
        {
            CreateRolePreviewMarker(
                map,
                parent,
                PlayerRole.Police,
                policeColor);
            CreateRolePreviewMarker(
                map,
                parent,
                PlayerRole.Thief,
                thiefColor);
        }

        private static void CreateRolePreviewMarker(
            GreyboxMapDefinition map,
            Transform parent,
            PlayerRole role,
            Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            marker.name = $"{role} Role Preview";
            marker.transform.SetParent(parent);
            marker.transform.position =
                PlayerRoleSpawnResolver.Resolve(map, role).position
                + Vector3.up;
            marker.transform.localScale =
                new Vector3(0.85f, 1f, 0.85f);
            marker.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial($"Role_{role}", color);

            PlayerRoleIdentity identity =
                marker.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
        }

        private static void AddLocation(
            IDictionary<GreyboxLocationId, Transform> result,
            Transform parent,
            GreyboxLocationId id,
            Vector3 position,
            Color color)
        {
            Transform anchor = CreateChild(id.ToString(), parent);
            anchor.position = position;

            GameObject marker = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            marker.name = "Marker";
            marker.transform.SetParent(anchor);
            marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            marker.transform.localScale = new Vector3(1.25f, 0.08f, 1.25f);
            marker.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial($"Marker_{id}", color);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());

            CreateWorldLabel(
                "Label",
                id.ToString().ToUpperInvariant(),
                position + new Vector3(0f, 0.35f, -0.9f),
                anchor);
            result.Add(id, anchor);
        }

        private static Transform CreateLadder(
            string name,
            Vector3 position,
            Material material,
            Transform parent)
        {
            Transform root = CreateChild(name, parent);
            root.position = position;

            CreateDecorativeCube(
                "Left Rail",
                new Vector3(0f, 2f, -0.45f),
                new Vector3(0.12f, 4f, 0.12f),
                material,
                root);
            CreateDecorativeCube(
                "Right Rail",
                new Vector3(0f, 2f, 0.45f),
                new Vector3(0.12f, 4f, 0.12f),
                material,
                root);

            for (int index = 0; index < 9; index++)
            {
                CreateDecorativeCube(
                    $"Rung {index + 1}",
                    new Vector3(0f, 0.35f + index * 0.42f, 0f),
                    new Vector3(0.14f, 0.08f, 0.9f),
                    material,
                    root);
            }

            return root;
        }

        private static void CreateRouteLine(
            GreyboxRouteReference route,
            Material material,
            Transform parent)
        {
            var lineObject = new GameObject(
                "Measured Crossing Route",
                typeof(LineRenderer));
            lineObject.transform.SetParent(parent);
            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startWidth = 0.18f;
            line.endWidth = 0.18f;
            line.positionCount = route.Waypoints.Count;
            line.useWorldSpace = true;
            line.numCornerVertices = 3;

            for (int index = 0; index < route.Waypoints.Count; index++)
            {
                line.SetPosition(
                    index,
                    route.Waypoints[index].position + Vector3.up * 0.18f);
            }
        }

        private static void CreateTraversalProbe(
            GreyboxMapDefinition map,
            float moveSpeed,
            Material markerMaterial,
            Transform parent)
        {
            var probeRoot = new GameObject(
                "Map Traversal Probe",
                typeof(CharacterController),
                typeof(GreyboxTraversalProbe));
            probeRoot.transform.SetParent(parent);

            CharacterController controller =
                probeRoot.GetComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.3f;

            GameObject visual = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            visual.name = "Probe Visual";
            visual.transform.SetParent(probeRoot.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            visual.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());

            GreyboxTraversalProbe probe =
                probeRoot.GetComponent<GreyboxTraversalProbe>();
            probe.Map = map;
            probe.CharacterController = controller;
            probe.RouteId = GreyboxMapDefinition.CrossingRouteId;
            probe.MoveSpeedMetersPerSecond = moveSpeed;
        }

        private static void CreateSceneInterface()
        {
            var canvasObject = new GameObject(
                "Scene UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform buttonRect = CreateRect(
                "SHOW RESULT Button",
                canvasObject.transform);
            buttonRect.anchorMin = Vector2.one;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.pivot = Vector2.one;
            buttonRect.anchoredPosition = new Vector2(-28f, -28f);
            buttonRect.sizeDelta = new Vector2(190f, 54f);

            Image image = buttonRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.55f, 0.12f, 0.09f, 0.92f);
            buttonRect.gameObject.AddComponent<Button>();
            SceneNavigationButton navigation =
                buttonRect.gameObject.AddComponent<SceneNavigationButton>();
            navigation.TargetScene = GameSceneId.Result;

            RectTransform labelRect =
                CreateRect("Label", buttonRect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text text = labelRect.gameObject.AddComponent<Text>();
            text.text = "RESULT";
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static float LoadPlayerMoveSpeed()
        {
            PlayerConfig config =
                AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"MAP-001 requires PlayerConfig at '{PlayerConfigPath}'.");
            }

            config.ValidateOrThrow();
            return config.MoveSpeed;
        }

        private static GameObject CreateCube(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool obstacle)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (obstacle)
            {
                cube.AddComponent<GreyboxObstacle>();
            }

            return cube;
        }

        private static void CreateFlatTile(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject tile = CreateCube(
                name,
                position,
                scale,
                material,
                parent,
                false);
            UnityEngine.Object.DestroyImmediate(tile.GetComponent<Collider>());
        }

        private static void CreateDecorativeCube(
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }

        private static void CreateWorldLabel(
            string name,
            string value,
            Vector3 position,
            Transform parent)
        {
            var labelObject = new GameObject(name, typeof(TextMesh));
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            TextMesh text = labelObject.GetComponent<TextMesh>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 46;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Transform CreateChild(
            string name,
            Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            return child.transform;
        }

        private static Material LoadOrCreateMaterial(
            string assetName,
            Color color,
            string shaderName = "Universal Render Pipeline/Lit")
        {
            string path = $"{MaterialRoot}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        $"Required shader '{shaderName}' is unavailable.");
                }

                material = new Material(shader)
                {
                    name = assetName
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialRoot))
            {
                AssetDatabase.CreateFolder(
                    "Assets/_Project/Materials",
                    "Greybox");
            }
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private enum LadderSide
        {
            West,
            East
        }
    }
}
