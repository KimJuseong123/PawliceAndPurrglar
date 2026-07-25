using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.UI;
using Unity.Netcode;
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
        private const string MatchConfigPath =
            "Assets/_Project/Settings/Configs/MatchConfig.asset";
        private const string ArrestConfigPath =
            "Assets/_Project/Settings/Configs/ArrestConfig.asset";
        private const string LootConfigPath =
            "Assets/_Project/Settings/Configs/LootConfig.asset";
        private const string CommonLootDefinitionPath =
            "Assets/_Project/Data/Loot/common-trinket.asset";

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
            Dictionary<PlayerRole, GameObject> roleMarkers =
                CreateRolePreviewMarkers(
                map,
                locationsRoot,
                PoliceBlue,
                ThiefRed);
            MatchRuntimeState matchRuntime =
                CreatePrototypeMatchRuntime(villageRoot.transform);
            PlayerConfig playerConfig = LoadPlayerConfig();
            var controlBindings = new List<PlayerRoleControlBinding>
            {
                ConfigureSharedPlayerMovement(
                    roleMarkers[PlayerRole.Police],
                    PlayerRole.Police,
                    matchRuntime,
                    playerConfig,
                    true),
                ConfigureSharedPlayerMovement(
                    roleMarkers[PlayerRole.Thief],
                    PlayerRole.Thief,
                    matchRuntime,
                    playerConfig,
                    false)
            };
            ConfigureArrestSystem(controlBindings, matchRuntime);
            PawsAndLoot.Gameplay.Camera.TopDownFollowCamera followCamera =
                ConfigurePlayerFollowCamera(
                    roleMarkers[PlayerRole.Police].transform);
            LocalPlayerRoleSelector roleSelector = CreateLocalRoleSelector(
                villageRoot.transform,
                controlBindings,
                followCamera);
            CreatePrototypeInteractionTargets(
                villageRoot.transform,
                locations,
                ladders,
                matchRuntime);

            CreateRouteLine(
                map.GetRoute(GreyboxMapDefinition.CrossingRouteId),
                route,
                routesRoot);
            CreateTraversalProbe(
                map,
                LoadPlayerMoveSpeed(),
                traversalProbe,
                villageRoot.transform);
            CreateSceneInterface(roleSelector, matchRuntime);

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

        private static Dictionary<PlayerRole, GameObject>
            CreateRolePreviewMarkers(
            GreyboxMapDefinition map,
            Transform parent,
            Color policeColor,
            Color thiefColor)
        {
            return new Dictionary<PlayerRole, GameObject>
            {
                [PlayerRole.Police] = CreateRolePreviewMarker(
                    map,
                    parent,
                    PlayerRole.Police,
                    policeColor),
                [PlayerRole.Thief] = CreateRolePreviewMarker(
                    map,
                    parent,
                    PlayerRole.Thief,
                    thiefColor)
            };
        }

        private static GameObject CreateRolePreviewMarker(
            GreyboxMapDefinition map,
            Transform parent,
            PlayerRole role,
            Color color)
        {
            var marker = new GameObject($"{role} Role Preview");
            marker.transform.SetParent(parent);
            marker.transform.position =
                PlayerRoleSpawnResolver.Resolve(map, role).position
                + Vector3.up;

            Transform visualRoot = CreateChild(
                "VisualRoot",
                marker.transform);
            GameObject placeholder = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            placeholder.name = "PlaceholderModel";
            placeholder.transform.SetParent(visualRoot, false);
            placeholder.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial($"Role_{role}", color);
            UnityEngine.Object.DestroyImmediate(
                placeholder.GetComponent<Collider>());

            Transform carryPoint = CreateChild(
                "CarryPoint",
                marker.transform);
            carryPoint.localPosition = new Vector3(0.7f, 0.8f, 0.4f);

            PlayerRoleIdentity identity =
                marker.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            PlayerVisualRoot playerVisualRoot =
                marker.AddComponent<PlayerVisualRoot>();
            playerVisualRoot.Configure(visualRoot, placeholder);
            return marker;
        }

        private static MatchRuntimeState CreatePrototypeMatchRuntime(
            Transform parent)
        {
            var runtimeObject = new GameObject("Match Runtime");
            runtimeObject.transform.SetParent(parent);
            MatchRuntimeState runtime =
                runtimeObject.AddComponent<MatchRuntimeState>();
            runtime.Configure(LoadMatchConfig(), true);
            return runtime;
        }

        private static PlayerRoleControlBinding
            ConfigureSharedPlayerMovement(
            GameObject player,
            PlayerRole role,
            MatchRuntimeState matchRuntime,
            PlayerConfig playerConfig,
            bool locallyControlled)
        {
            player.name = $"{role} Player";
            Collider primitiveCollider = player.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }

            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.08f;

            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            motor.Configure(
                controller,
                playerConfig,
                matchRuntime,
                Camera.main != null ? Camera.main.transform : null);

            PlayerKeyboardInput keyboardInput =
                player.AddComponent<PlayerKeyboardInput>();
            keyboardInput.Configure(motor, locallyControlled);

            PlayerRoleIdentity identity =
                player.GetComponent<PlayerRoleIdentity>();
            PlayerInteractionScanner interactionScanner =
                player.AddComponent<PlayerInteractionScanner>();
            interactionScanner.Configure(
                identity,
                playerConfig,
                matchRuntime);
            PlayerInteractionInput interactionInput =
                player.AddComponent<PlayerInteractionInput>();
            interactionInput.Configure(
                interactionScanner,
                locallyControlled);
            LootCarrier lootCarrier = player.AddComponent<LootCarrier>();
            lootCarrier.Configure(
                identity,
                matchRuntime,
                player.transform.Find("CarryPoint"));
            LootDropInput lootDropInput =
                player.AddComponent<LootDropInput>();
            lootDropInput.Configure(lootCarrier, locallyControlled);
            LootCarryMovementPenalty carryPenalty =
                player.AddComponent<LootCarryMovementPenalty>();
            carryPenalty.Configure(lootCarrier, motor);
            if (role == PlayerRole.Thief)
            {
                ThiefLootWallet wallet =
                    player.AddComponent<ThiefLootWallet>();
                wallet.Configure(identity, LoadMatchConfig());
            }

            player.AddComponent<NetworkObject>();
            player.GetComponent<PlayerVisualRoot>().ValidateOrThrow();

            return new PlayerRoleControlBinding(
                identity,
                keyboardInput,
                interactionScanner,
                interactionInput,
                lootDropInput);
        }

        private static PawsAndLoot.Gameplay.Camera.TopDownFollowCamera
            ConfigurePlayerFollowCamera(Transform initialTarget)
        {
            if (Camera.main == null)
            {
                throw new InvalidOperationException(
                    "Player movement requires the Game scene Main Camera.");
            }

            var followCamera = Camera.main.gameObject.AddComponent<
                PawsAndLoot.Gameplay.Camera.TopDownFollowCamera>();
            followCamera.Configure(
                initialTarget,
                new Vector3(0f, 16f, -14f),
                0.12f);
            return followCamera;
        }

        private static void ConfigureArrestSystem(
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime)
        {
            PlayerRoleIdentity police = null;
            PlayerRoleIdentity thief = null;
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding.Role == PlayerRole.Police)
                {
                    police = binding.Identity;
                }
                else if (binding.Role == PlayerRole.Thief)
                {
                    thief = binding.Identity;
                }
            }

            if (police == null || thief == null)
            {
                throw new InvalidOperationException(
                    "ARREST-001 requires Police and Thief players.");
            }

            ArrestConfig config = LoadArrestConfig();
            ArrestRangeSensor sensor =
                police.gameObject.AddComponent<ArrestRangeSensor>();
            sensor.Configure(
                police,
                thief,
                config,
                Physics.AllLayers);
            ArrestProgressController progress =
                police.gameObject.AddComponent<ArrestProgressController>();
            progress.Configure(sensor, matchRuntime, config);
            ArrestCompletionController completion =
                police.gameObject.AddComponent<ArrestCompletionController>();
            completion.Configure(progress, matchRuntime);
        }

        private static LocalPlayerRoleSelector CreateLocalRoleSelector(
            Transform parent,
            IEnumerable<PlayerRoleControlBinding> bindings,
            PawsAndLoot.Gameplay.Camera.TopDownFollowCamera followCamera)
        {
            var selectorObject = new GameObject("Local Player Role Selector");
            selectorObject.transform.SetParent(parent);
            LocalPlayerRoleSelector selector =
                selectorObject.AddComponent<LocalPlayerRoleSelector>();
            selector.Configure(bindings, followCamera, PlayerRole.Police);
            return selector;
        }

        private static void CreatePrototypeInteractionTargets(
            Transform parent,
            IReadOnlyDictionary<GreyboxLocationId, Transform> locations,
            IReadOnlyList<Transform> ladders,
            MatchRuntimeState matchRuntime)
        {
            Transform root = CreateChild(
                "PLAYER-004 Interaction Targets",
                parent);
            CreateLootTarget(
                "Prototype Loot",
                locations[GreyboxLocationId.JewelryStore].position
                    + new Vector3(1.8f, 0.5f, 0f),
                new Color(0.75f, 0.3f, 0.95f),
                root);
            CreateSaleZone(
                "Prototype Sale Point",
                locations[GreyboxLocationId.RaccoonMarket].position
                    + new Vector3(-1.8f, 0.5f, 0f),
                MarketGold,
                root,
                matchRuntime);
            CreatePrototypeInteractionTarget(
                "Prototype Ladder Point",
                ladders[0].position + new Vector3(0f, 0.5f, -1.25f),
                PlayerInteractionType.Traversal,
                "Use ladder",
                new Color(0.2f, 0.75f, 0.95f),
                root);
            CreatePrototypeInteractionTarget(
                "Prototype Plaza Point",
                locations[GreyboxLocationId.CentralPlaza].position
                    + new Vector3(-3f, 0.5f, -3f),
                PlayerInteractionType.Generic,
                "Inspect plaza marker",
                Color.white,
                root);
        }

        private static void CreatePrototypeInteractionTarget(
            string name,
            Vector3 position,
            PlayerInteractionType interactionType,
            string prompt,
            Color color,
            Transform parent)
        {
            GameObject target = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            target.name = name;
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.transform.localScale = Vector3.one * 0.75f;
            target.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial(
                    $"Interaction_{interactionType}",
                    color);
            PrototypeInteractable interactable =
                target.AddComponent<PrototypeInteractable>();
            interactable.Configure(interactionType, prompt);
        }

        private static void CreateLootTarget(
            string name,
            Vector3 position,
            Color color,
            Transform parent)
        {
            var target = new GameObject(name);
            target.name = name;
            target.transform.SetParent(parent);
            target.transform.position = position;
            BoxCollider worldCollider =
                target.AddComponent<BoxCollider>();
            worldCollider.size = Vector3.one * 0.75f;

            Transform presentationRoot = CreateChild(
                "PresentationRoot",
                target.transform);
            GameObject placeholder = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            placeholder.name = "PlaceholderModel";
            placeholder.transform.SetParent(presentationRoot, false);
            placeholder.transform.localScale = Vector3.one * 0.75f;
            placeholder.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial("Interaction_Loot", color);
            UnityEngine.Object.DestroyImmediate(
                placeholder.GetComponent<Collider>());

            LootDefinition definition =
                AssetDatabase.LoadAssetAtPath<LootDefinition>(
                    CommonLootDefinitionPath);
            if (definition == null)
            {
                throw new GameConfigurationException(
                    $"Game scene requires LootDefinition at "
                    + $"'{CommonLootDefinitionPath}'.");
            }

            definition.ValidateOrThrow();
            LootItem loot = target.AddComponent<LootItem>();
            loot.Configure(definition, presentationRoot);
        }

        private static void CreateSaleZone(
            string name,
            Vector3 position,
            Color color,
            Transform parent,
            MatchRuntimeState matchRuntime)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent);
            target.transform.position = position;
            BoxCollider saleArea = target.AddComponent<BoxCollider>();
            saleArea.isTrigger = true;
            saleArea.size = new Vector3(3f, 2f, 3f);

            GameObject placeholder = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            placeholder.name = "SaleZoneMarker";
            placeholder.transform.SetParent(target.transform, false);
            placeholder.transform.localPosition =
                new Vector3(0f, -0.45f, 0f);
            placeholder.transform.localScale =
                new Vector3(1.4f, 0.05f, 1.4f);
            placeholder.GetComponent<Renderer>().sharedMaterial =
                LoadOrCreateMaterial("Interaction_Sale", color);
            UnityEngine.Object.DestroyImmediate(
                placeholder.GetComponent<Collider>());

            LootSaleZone saleZone =
                target.AddComponent<LootSaleZone>();
            saleZone.Configure(
                saleArea,
                LoadLootConfig(),
                matchRuntime);
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

        private static void CreateSceneInterface(
            LocalPlayerRoleSelector roleSelector,
            MatchRuntimeState matchRuntime)
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

            RectTransform hudRoot = CreateRect(
                "Common HUD",
                canvasObject.transform);
            hudRoot.anchorMin = Vector2.zero;
            hudRoot.anchorMax = Vector2.one;
            hudRoot.offsetMin = Vector2.zero;
            hudRoot.offsetMax = Vector2.zero;

            Text roleLabel = CreateHudLabel(
                "Role",
                hudRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -28f),
                new Vector2(260f, 48f),
                TextAnchor.MiddleLeft,
                26);
            Text stateLabel = CreateHudLabel(
                "Match State",
                hudRoot,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(28f, -80f),
                new Vector2(300f, 42f),
                TextAnchor.MiddleLeft,
                20);
            Text timerLabel = CreateHudLabel(
                "Match Timer",
                hudRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(220f, 68f),
                TextAnchor.MiddleCenter,
                40);

            RectTransform arrestHudRect = CreateRect(
                "Arrest HUD",
                hudRoot);
            arrestHudRect.anchorMin = new Vector2(0.5f, 0f);
            arrestHudRect.anchorMax = new Vector2(0.5f, 0f);
            arrestHudRect.pivot = new Vector2(0.5f, 0f);
            arrestHudRect.anchoredPosition = new Vector2(0f, 112f);
            arrestHudRect.sizeDelta = new Vector2(520f, 96f);
            Image arrestHudBackground =
                arrestHudRect.gameObject.AddComponent<Image>();
            arrestHudBackground.color =
                new Color(0.03f, 0.05f, 0.09f, 0.92f);

            Text arrestStatusLabel = CreateHudLabel(
                "Arrest Status",
                arrestHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -10f),
                new Vector2(330f, 40f),
                TextAnchor.MiddleLeft,
                22);
            Text thiefWarningLabel = CreateHudLabel(
                "Thief Warning",
                arrestHudRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-18f, -10f),
                new Vector2(140f, 40f),
                TextAnchor.MiddleRight,
                23);
            thiefWarningLabel.color =
                new Color(1f, 0.32f, 0.2f);

            RectTransform arrestBarBackground = CreateRect(
                "Arrest Bar Background",
                arrestHudRect);
            arrestBarBackground.anchorMin = new Vector2(0f, 0f);
            arrestBarBackground.anchorMax = new Vector2(1f, 0f);
            arrestBarBackground.pivot = new Vector2(0.5f, 0f);
            arrestBarBackground.anchoredPosition =
                new Vector2(0f, 16f);
            arrestBarBackground.sizeDelta =
                new Vector2(-36f, 20f);
            Image arrestBarBackgroundImage =
                arrestBarBackground.gameObject.AddComponent<Image>();
            arrestBarBackgroundImage.color =
                new Color(0f, 0f, 0f, 0.72f);

            RectTransform arrestBarFill = CreateRect(
                "Arrest Bar Fill",
                arrestBarBackground);
            arrestBarFill.anchorMin = Vector2.zero;
            arrestBarFill.anchorMax = Vector2.one;
            arrestBarFill.offsetMin = new Vector2(3f, 3f);
            arrestBarFill.offsetMax = new Vector2(-3f, -3f);
            Image arrestProgressFill =
                arrestBarFill.gameObject.AddComponent<Image>();
            arrestProgressFill.type = Image.Type.Filled;
            arrestProgressFill.fillMethod =
                Image.FillMethod.Horizontal;
            arrestProgressFill.fillOrigin =
                (int)Image.OriginHorizontal.Left;
            arrestProgressFill.fillAmount = 0f;

            RectTransform thiefHudRect = CreateRect(
                "Thief HUD",
                hudRoot);
            thiefHudRect.anchorMin = Vector2.one;
            thiefHudRect.anchorMax = Vector2.one;
            thiefHudRect.pivot = Vector2.one;
            thiefHudRect.anchoredPosition =
                new Vector2(-28f, -104f);
            thiefHudRect.sizeDelta = new Vector2(360f, 286f);
            Image thiefHudBackground =
                thiefHudRect.gameObject.AddComponent<Image>();
            thiefHudBackground.color =
                new Color(0.18f, 0.05f, 0.04f, 0.92f);

            Text thiefHudTitle = CreateHudLabel(
                "Title",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -14f),
                new Vector2(324f, 38f),
                TextAnchor.MiddleLeft,
                24);
            thiefHudTitle.text = "THIEF LOOT";
            Text saleAmountLabel = CreateHudLabel(
                "Sale Amount",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -58f),
                new Vector2(324f, 36f),
                TextAnchor.MiddleLeft,
                20);
            Text heldLootLabel = CreateHudLabel(
                "Held Loot",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -100f),
                new Vector2(324f, 36f),
                TextAnchor.MiddleLeft,
                20);
            Text lootPriceLabel = CreateHudLabel(
                "Loot Price",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -142f),
                new Vector2(324f, 36f),
                TextAnchor.MiddleLeft,
                20);
            Text movementPenaltyLabel = CreateHudLabel(
                "Movement Penalty",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -184f),
                new Vector2(324f, 36f),
                TextAnchor.MiddleLeft,
                20);
            Text saleAvailabilityLabel = CreateHudLabel(
                "Sale Availability",
                thiefHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -230f),
                new Vector2(324f, 40f),
                TextAnchor.MiddleLeft,
                21);
            saleAvailabilityLabel.color =
                new Color(1f, 0.78f, 0.25f);

            RectTransform objectiveRect = CreateRect(
                "Role Objective",
                hudRoot);
            objectiveRect.anchorMin = new Vector2(0.5f, 1f);
            objectiveRect.anchorMax = new Vector2(0.5f, 1f);
            objectiveRect.pivot = new Vector2(0.5f, 1f);
            objectiveRect.anchoredPosition = new Vector2(0f, -104f);
            objectiveRect.sizeDelta = new Vector2(760f, 76f);
            Image objectiveBackground =
                objectiveRect.gameObject.AddComponent<Image>();
            objectiveBackground.color =
                new Color(0.03f, 0.05f, 0.09f, 0.9f);
            RectTransform objectiveLabelRect =
                CreateRect("Objective Label", objectiveRect);
            objectiveLabelRect.anchorMin = Vector2.zero;
            objectiveLabelRect.anchorMax = Vector2.one;
            objectiveLabelRect.offsetMin = new Vector2(24f, 8f);
            objectiveLabelRect.offsetMax = new Vector2(-24f, -8f);
            Text objectiveLabel =
                objectiveLabelRect.gameObject.AddComponent<Text>();
            objectiveLabel.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            objectiveLabel.fontSize = 22;
            objectiveLabel.fontStyle = FontStyle.Bold;
            objectiveLabel.alignment = TextAnchor.MiddleCenter;
            objectiveLabel.color = Color.white;
            objectiveLabel.raycastTarget = false;

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

            RectTransform promptRect = CreateRect(
                "Interaction Prompt",
                canvasObject.transform);
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 42f);
            promptRect.sizeDelta = new Vector2(620f, 56f);
            Image promptBackground =
                promptRect.gameObject.AddComponent<Image>();
            promptBackground.color =
                new Color(0.02f, 0.04f, 0.08f, 0.88f);

            RectTransform promptLabelRect =
                CreateRect("Prompt Label", promptRect);
            promptLabelRect.anchorMin = Vector2.zero;
            promptLabelRect.anchorMax = Vector2.one;
            promptLabelRect.offsetMin = Vector2.zero;
            promptLabelRect.offsetMax = Vector2.zero;
            Text promptLabel =
                promptLabelRect.gameObject.AddComponent<Text>();
            promptLabel.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            promptLabel.fontSize = 24;
            promptLabel.fontStyle = FontStyle.Bold;
            promptLabel.alignment = TextAnchor.MiddleCenter;
            promptLabel.color = Color.white;
            promptLabel.raycastTarget = false;

            CommonHudPresenter hudPresenter =
                hudRoot.gameObject.AddComponent<CommonHudPresenter>();
            hudPresenter.Configure(
                matchRuntime,
                roleSelector,
                timerLabel,
                roleLabel,
                stateLabel,
                promptLabel);
            RoleObjectivePresenter objectivePresenter =
                hudRoot.gameObject.AddComponent<
                    RoleObjectivePresenter>();
            objectivePresenter.Configure(
                matchRuntime,
                roleSelector,
                objectiveRect.gameObject,
                objectiveLabel);
            PlayerRoleControlBinding thiefBinding = null;
            foreach (PlayerRoleControlBinding binding
                     in roleSelector.Bindings)
            {
                if (binding.Role == PlayerRole.Thief)
                {
                    thiefBinding = binding;
                    break;
                }
            }

            if (thiefBinding == null)
            {
                throw new InvalidOperationException(
                    "Thief HUD requires a Thief control binding.");
            }

            GameObject thiefPlayer =
                thiefBinding.Identity.gameObject;
            ThiefHudPresenter thiefHudPresenter =
                hudRoot.gameObject.AddComponent<
                    ThiefHudPresenter>();
            thiefHudPresenter.Configure(
                roleSelector,
                thiefPlayer.GetComponent<ThiefLootWallet>(),
                thiefPlayer.GetComponent<LootCarrier>(),
                thiefPlayer.GetComponent<PlayerMovementMotor>(),
                thiefBinding.InteractionScanner,
                LoadLootConfig(),
                thiefHudRect.gameObject,
                saleAmountLabel,
                heldLootLabel,
                lootPriceLabel,
                movementPenaltyLabel,
                saleAvailabilityLabel);

            PlayerRoleControlBinding policeBinding = null;
            foreach (PlayerRoleControlBinding binding
                     in roleSelector.Bindings)
            {
                if (binding.Role == PlayerRole.Police)
                {
                    policeBinding = binding;
                    break;
                }
            }

            if (policeBinding == null)
            {
                throw new InvalidOperationException(
                    "Arrest HUD requires a Police control binding.");
            }

            GameObject policePlayer =
                policeBinding.Identity.gameObject;
            ArrestHudPresenter arrestHudPresenter =
                hudRoot.gameObject.AddComponent<ArrestHudPresenter>();
            arrestHudPresenter.Configure(
                roleSelector,
                policePlayer.GetComponent<
                    ArrestProgressController>(),
                policePlayer.GetComponent<
                    ArrestCompletionController>(),
                arrestHudRect.gameObject,
                arrestProgressFill,
                arrestStatusLabel,
                thiefWarningLabel);

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem));
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static Text CreateHudLabel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment,
            int fontSize)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static PlayerConfig LoadPlayerConfig()
        {
            PlayerConfig config =
                AssetDatabase.LoadAssetAtPath<PlayerConfig>(PlayerConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"MAP-001 requires PlayerConfig at '{PlayerConfigPath}'.");
            }

            config.ValidateOrThrow();
            return config;
        }

        private static MatchConfig LoadMatchConfig()
        {
            MatchConfig config =
                AssetDatabase.LoadAssetAtPath<MatchConfig>(MatchConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"Game scene requires MatchConfig at '{MatchConfigPath}'.");
            }

            config.ValidateOrThrow();
            return config;
        }

        private static LootConfig LoadLootConfig()
        {
            LootConfig config =
                AssetDatabase.LoadAssetAtPath<LootConfig>(LootConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"Game scene requires LootConfig at '{LootConfigPath}'.");
            }

            config.ValidateOrThrow();
            return config;
        }

        private static ArrestConfig LoadArrestConfig()
        {
            ArrestConfig config =
                AssetDatabase.LoadAssetAtPath<ArrestConfig>(
                    ArrestConfigPath);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"Game scene requires ArrestConfig at "
                    + $"'{ArrestConfigPath}'.");
            }

            config.ValidateOrThrow();
            return config;
        }

        private static float LoadPlayerMoveSpeed()
        {
            return LoadPlayerConfig().MoveSpeed;
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
