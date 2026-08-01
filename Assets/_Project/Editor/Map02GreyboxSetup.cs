using System;
using System.IO;
using PawsAndLoot.Companions;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Generates the isolated MAP-002 layout from the current Game runtime
    /// scaffold. This scene is intentionally not registered in the normal
    /// scene catalog or EditorBuildSettings.
    /// </summary>
    public static class Map02GreyboxSetup
    {
        public const string ScenePath =
            "Assets/_Project/Scenes/GameMap02.unity";
        public const string WindowsBuildPath =
            "Builds/TechnicalValidation/Windows/"
            + "PawsAndLootMap02Greybox.exe";

        private const string RoadModelPath =
            "Assets/_Project/Art/Environment/road section 3d model/"
            + "road+section+3d+model.fbx";
        private const string RoadTexturePath =
            "Assets/_Project/Art/Environment/road section 3d model/"
            + "road+section+3d+model.fbm/"
            + "road+section+3d+model_basecolor.jpg";
        private const string RoadDisplayMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02RoadFbx.mat";
        private const string GridMaterialPath =
            "Assets/_Project/Materials/Greybox/MapGrid.mat";
        private const string TrashBinMaterialPath =
            "Assets/_Project/Materials/Greybox/TrashBin.mat";
        private const string TreeTrunkMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02TreeTrunk.mat";
        private const string TreeCanopyMaterialPath =
            "Assets/_Project/Materials/Greybox/Map02TreeCanopy.mat";
        private const float RoadWidth = 2f;
        private const float RoadHeight = 0.04f;
        private const float MapWidth = 70f;
        private const float MapDepth = 65f;
        private const float BoundaryOffset = 1f;
        private const float TrashBinHeight = 1.4f;
        private const float OneStoreyHouseFootprint = 7.5f;
        private const float PoliceStationX = 27.5f;
        private const float PoliceStationZ = 37f;
        private const float PoliceStationFootprint = 12f;
        private const float PoliceStartFrontClearance = 1.5f;
        private static readonly Vector3 PoliceStartGroundPosition = new(
            PoliceStationX,
            0f,
            PoliceStationZ
                - PoliceStationFootprint * 0.5f
                - PoliceStartFrontClearance);

        private static readonly Vector2[] TrashBinCoordinates =
        {
            new(26f, 55f),
            new(4f, 48f),
            new(63f, 48f),
            new(50f, 38f),
            new(1f, 21f),
            new(64f, 22f),
            new(7f, 2f)
        };

        private static readonly TreeSpec[] Trees =
        {
            new("Tree 01", 12.8f, 62.4f, 0.9f, 12f),
            new("Tree 02", 41f, 57f, 1.05f, 68f),
            new("Tree 03", 58f, 56f, 0.95f, 143f),
            new("Tree 04", 66f, 62f, 1.1f, 221f),
            new("Tree 05", 10.5f, 45f, 0.85f, 37f),
            new("Tree 06", 65f, 43f, 1f, 176f),
            new("Tree 07", 65f, 31f, 0.9f, 295f),
            new("Tree 08", 14.5f, 16.5f, 0.85f, 104f),
            new("Tree 09", 24f, 18f, 1f, 15f),
            new("Tree 10", 30.5f, 22f, 0.9f, 248f),
            new("Tree 11", 33f, 15.5f, 0.95f, 132f),
            new("Tree 12", 42f, 18f, 1.05f, 310f),
            new("Tree 13", 45f, 23f, 0.85f, 57f),
            new("Tree 14", 4f, 6f, 1f, 201f),
            new("Tree 15", 13.8f, 5.5f, 0.9f, 82f),
            new("Tree 16", 26f, 6.5f, 0.9f, 154f),
            new("Tree 17", 54f, 7f, 1.05f, 24f),
            new("Tree 18", 65f, 7f, 0.95f, 266f)
        };

        private static readonly RoadSpec[] Roads =
        {
            RoadSpec.Horizontal("Top Road", 49f, 0f, 66f),
            RoadSpec.Horizontal("Center Road", 26f, 0f, 66f),
            RoadSpec.Horizontal("Lower Left Road", 21f, 0f, 11f),
            RoadSpec.Horizontal("Lower Center Road", 12f, 11f, 48f),
            RoadSpec.Horizontal("Lower Right Road", 13f, 48f, 66f),
            RoadSpec.Vertical("Top Vertical Road", 26f, 49f, 61f),
            RoadSpec.Vertical("Center Left Vertical Road", 20f, 26f, 49f),
            RoadSpec.Vertical("Center Vertical Road", 35f, 26f, 49f),
            RoadSpec.Vertical("Center Right Vertical Road", 49f, 26f, 49f),
            RoadSpec.Vertical("Lower Left Vertical Road", 11f, 0f, 21f),
            RoadSpec.Vertical(
                "Lower Center Left Vertical Road",
                19f,
                12f,
                26f),
            RoadSpec.Vertical(
                "Lower Center Right Vertical Road",
                37f,
                12f,
                26f),
            RoadSpec.Vertical("Lower Right Vertical Road", 48f, 0f, 26f)
        };

        private static readonly BuildingSpec[] Buildings =
        {
            BuildingSpec.House2F(
                "Top Left Two Storey House",
                7f,
                56f,
                180f),
            BuildingSpec.House1F(
                "Top Left Center One Storey House",
                19f,
                55f,
                180f),
            BuildingSpec.House2F(
                "Top Right Center Two Storey House",
                33f,
                56f,
                180f),
            BuildingSpec.House1F(
                "Top Right One Storey House",
                49f,
                55f,
                180f),
            BuildingSpec.House1F(
                "Middle Left Upper One Storey House",
                4.5f,
                43f,
                90f),
            BuildingSpec.House2F(
                "Middle Left Lower Two Storey House",
                5.5f,
                33f,
                90f),
            new BuildingSpec(
                "Supermarket",
                "building_supermarket",
                15f,
                37f,
                12f,
                8f,
                90f),
            new BuildingSpec(
                "Police Station",
                "building_police_station",
                PoliceStationX,
                PoliceStationZ,
                PoliceStationFootprint,
                PoliceStationFootprint,
                180f),
            new BuildingSpec(
                "Bookstore",
                "building_bookstore",
                42f,
                37f,
                12f,
                10f,
                180f),
            BuildingSpec.House2F(
                "Middle Right Upper Two Storey House",
                57f,
                42.5f,
                180f),
            BuildingSpec.House1F(
                "Middle Right Lower One Storey House",
                56f,
                31.5f,
                180f),
            BuildingSpec.House1F(
                "Lower Left One Storey House",
                5.5f,
                15f,
                90f),
            BuildingSpec.House2F(
                "Lower Right Two Storey House",
                56f,
                19.5f,
                180f),
            BuildingSpec.House2F(
                "Bottom Left Two Storey House",
                20f,
                5.5f),
            BuildingSpec.House1F(
                "Bottom Center One Storey House",
                32f,
                6.5f),
            BuildingSpec.House1F(
                "Bottom Right One Storey House",
                42.5f,
                7f)
        };

        [MenuItem("Paws & Loot/Setup/Create MAP-002 Greybox Layout")]
        public static void CreateLayout()
        {
            PlaceholderModelLibrary.ResetMissingAssetLog();
            Scene scene = CloneGameScaffold();
            GreyboxMapDefinition map =
                FindInScene<GreyboxMapDefinition>(scene);
            if (map == null || map.transform.parent == null)
            {
                throw new InvalidOperationException(
                    "MAP-002 requires the Game scene's greybox map scaffold.");
            }

            Transform layoutRoot = map.transform.parent;
            layoutRoot.name = "MAP-002 Greybox Layout";
            Transform environment = RequireDirectChild(
                layoutRoot,
                "Environment");
            ClearEnvironment(layoutRoot, environment);
            ConfigureBounds(environment);
            ConfigurePoliceStart(scene, map);

            GameObject roadModel =
                AssetDatabase.LoadAssetAtPath<GameObject>(RoadModelPath);
            if (roadModel == null)
            {
                throw new FileNotFoundException(
                    $"MAP-002 road FBX is missing: {RoadModelPath}",
                    RoadModelPath);
            }

            Material roadDisplayMaterial =
                CreateOrUpdateRoadDisplayMaterial();
            Transform roadRoot = CreateChild("MAP-002 Roads", environment);
            foreach (RoadSpec road in Roads)
            {
                CreateRoad(
                    road,
                    roadModel,
                    roadDisplayMaterial,
                    roadRoot);
            }

            Transform buildingRoot =
                CreateChild("MAP-002 Buildings", environment);
            foreach (BuildingSpec building in Buildings)
            {
                CreateBuilding(building, buildingRoot);
            }

            Material treeTrunkMaterial = CreateOrUpdateSolidMaterial(
                TreeTrunkMaterialPath,
                new Color(0.34f, 0.18f, 0.08f),
                0.12f);
            Material treeCanopyMaterial = CreateOrUpdateSolidMaterial(
                TreeCanopyMaterialPath,
                new Color(0.18f, 0.48f, 0.16f),
                0.18f);
            Transform treeRoot =
                CreateChild("MAP-002 Trees", environment);
            foreach (TreeSpec tree in Trees)
            {
                CreateTree(
                    tree,
                    treeTrunkMaterial,
                    treeCanopyMaterial,
                    treeRoot);
            }

            Material trashBinMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    TrashBinMaterialPath);
            if (trashBinMaterial == null)
            {
                throw new FileNotFoundException(
                    $"MAP-002 trash-bin material is missing: "
                    + $"{TrashBinMaterialPath}",
                    TrashBinMaterialPath);
            }

            Transform trashBinRoot =
                CreateChild("MAP-002 Trash Bins", environment);
            for (int index = 0;
                 index < TrashBinCoordinates.Length;
                 index++)
            {
                CreateTrashBin(
                    index + 1,
                    TrashBinCoordinates[index],
                    trashBinMaterial,
                    trashBinRoot);
            }

            Material gridMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath);
            if (gridMaterial == null)
            {
                throw new FileNotFoundException(
                    $"MAP-002 grid material is missing: {GridMaterialPath}",
                    GridMaterialPath);
            }

            CreateCoordinateGrid(environment, gridMaterial);

            map.MarkEnvironmentContentCleared();
            map.ResizeDimensions(MapWidth, MapDepth);
            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save MAP-002 scene: {ScenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
            Debug.Log(
                "MAP-002 created: 70x65 bounds, 13 roads, 16 buildings, "
                + "18 sparse greybox trees, 7 green trash bins, "
                + "1m coordinate grid, "
                + $"Police start at ({PoliceStartGroundPosition.x}, "
                + $"{PoliceStartGroundPosition.z}).");
        }

        [MenuItem("Paws & Loot/Setup/Validate MAP-002 Greybox Layout")]
        public static void ValidateScene()
        {
            if (!File.Exists(ScenePath))
            {
                throw new FileNotFoundException(
                    $"MAP-002 scene is missing: {ScenePath}",
                    ScenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            GreyboxMapDefinition map =
                FindInScene<GreyboxMapDefinition>(scene);
            Camera camera = FindInScene<Camera>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            EventSystem eventSystem = FindInScene<EventSystem>(scene);
            MatchResultFlowController resultFlow =
                FindInScene<MatchResultFlowController>(scene);

            if (map == null
                || map.transform.parent == null
                || map.transform.parent.name != "MAP-002 Greybox Layout"
                || camera == null
                || camera.orthographic
                || canvas == null
                || eventSystem == null
                || resultFlow == null)
            {
                throw new InvalidOperationException(
                    "MAP-002 requires its map, perspective camera, UI, "
                    + "event system, and result flow.");
            }

            map.ValidateOrThrow();
            resultFlow.ValidateOrThrow();
            Transform environment = RequireDirectChild(
                map.transform.parent,
                "Environment");
            Transform roadRoot = RequireDirectChild(
                environment,
                "MAP-002 Roads");
            Transform buildingRoot = RequireDirectChild(
                environment,
                "MAP-002 Buildings");
            Transform treeRoot = RequireDirectChild(
                environment,
                "MAP-002 Trees");
            Transform gridRoot = RequireDirectChild(
                environment,
                "MAP-002 Coordinate Grid");
            Transform trashBinRoot = RequireDirectChild(
                environment,
                "MAP-002 Trash Bins");
            Transform gridLines = RequireDirectChild(
                gridRoot,
                "Grid Lines");
            Transform axisLabels = RequireDirectChild(
                gridRoot,
                "Axis Labels");
            Transform coordinateLabels = RequireDirectChild(
                gridRoot,
                "Coordinate Labels");
            if (!Mathf.Approximately(map.MapWidthMeters, MapWidth)
                || !Mathf.Approximately(map.MapDepthMeters, MapDepth)
                || !map.EnvironmentContentCleared
                || roadRoot.childCount != Roads.Length
                || buildingRoot.childCount != Buildings.Length
                || treeRoot.childCount != Trees.Length
                || trashBinRoot.childCount != TrashBinCoordinates.Length
                || gridLines.childCount != 137
                || axisLabels.childCount != 31
                || coordinateLabels.childCount != 36)
            {
                throw new InvalidOperationException(
                    "MAP-002 dimensions or authored layout counts are invalid.");
            }

            foreach (RoadSpec road in Roads)
            {
                ValidateRoad(road, roadRoot);
            }

            foreach (TreeSpec tree in Trees)
            {
                ValidateTree(
                    tree,
                    treeRoot,
                    roadRoot,
                    buildingRoot);
            }
        }

        [MenuItem("Paws & Loot/Technical Validation/Build Windows MAP-002")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
            {
                CreateLayout();
            }
            else
            {
                ValidateScene();
            }

            string absoluteBuildPath = Path.GetFullPath(WindowsBuildPath);
            string directory = Path.GetDirectoryName(absoluteBuildPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Could not resolve MAP-002 build directory for "
                    + $"'{absoluteBuildPath}'.");
            }

            Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = absoluteBuildPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"MAP-002 Windows build failed with "
                    + $"{report.summary.totalErrors} errors.");
            }
        }

        private static Scene CloneGameScaffold()
        {
            string sourcePath =
                GameSceneCatalog.GetPath(GameSceneId.Game);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"Game scene is missing: {sourcePath}",
                    sourcePath);
            }

            Scene source = EditorSceneManager.OpenScene(
                sourcePath,
                OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(
                    source,
                    ScenePath,
                    true))
            {
                throw new InvalidOperationException(
                    $"Failed to clone Game scene to MAP-002: {ScenePath}");
            }

            AssetDatabase.ImportAsset(
                ScenePath,
                ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
        }

        private static void ClearEnvironment(
            Transform layoutRoot,
            Transform environment)
        {
            string[] removableRoots =
            {
                "Roads and Alleys",
                "Buildings",
                "Traversal Features",
                "Authored Dressing",
                "PLAYER-004 Interaction Targets"
            };
            foreach (string childName in removableRoots)
            {
                DestroyDirectChild(layoutRoot, childName);
            }

            for (int index = environment.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = environment.GetChild(index);
                if (child.name != "Ground"
                    && child.name != "North Boundary"
                    && child.name != "South Boundary"
                    && child.name != "West Boundary"
                    && child.name != "East Boundary")
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            Transform routeNetwork = layoutRoot.Find("Route Network");
            if (routeNetwork != null)
            {
                foreach (LineRenderer line in
                    routeNetwork.GetComponentsInChildren<LineRenderer>(true))
                {
                    UnityEngine.Object.DestroyImmediate(line.gameObject);
                }
            }

            GreyboxTraversalProbe traversalProbe =
                UnityEngine.Object.FindFirstObjectByType<GreyboxTraversalProbe>(
                    FindObjectsInactive.Include);
            if (traversalProbe != null
                && traversalProbe.gameObject.scene == layoutRoot.gameObject.scene)
            {
                UnityEngine.Object.DestroyImmediate(
                    traversalProbe.gameObject);
            }
        }

        private static void ConfigureBounds(Transform environment)
        {
            ConfigureSurface(
                RequireDirectChild(environment, "Ground"),
                new Vector3(MapWidth * 0.5f, -0.15f, MapDepth * 0.5f),
                new Vector3(MapWidth, 0.3f, MapDepth));
            RecreateBoundary(
                environment,
                "North Boundary",
                new Vector3(
                    MapWidth * 0.5f,
                    1f,
                    MapDepth + BoundaryOffset),
                new Vector3(
                    MapWidth + BoundaryOffset * 2f,
                    2f,
                    1f));
            RecreateBoundary(
                environment,
                "South Boundary",
                new Vector3(MapWidth * 0.5f, 1f, -BoundaryOffset),
                new Vector3(
                    MapWidth + BoundaryOffset * 2f,
                    2f,
                    1f));
            RecreateBoundary(
                environment,
                "West Boundary",
                new Vector3(-BoundaryOffset, 1f, MapDepth * 0.5f),
                new Vector3(
                    1f,
                    2f,
                    MapDepth + BoundaryOffset * 2f));
            RecreateBoundary(
                environment,
                "East Boundary",
                new Vector3(
                    MapWidth + BoundaryOffset,
                    1f,
                    MapDepth * 0.5f),
                new Vector3(
                    1f,
                    2f,
                    MapDepth + BoundaryOffset * 2f));
        }

        private static void ConfigurePoliceStart(
            Scene scene,
            GreyboxMapDefinition map)
        {
            Transform policeSpawn =
                map.GetLocation(GreyboxLocationId.PoliceSpawn);
            policeSpawn.position = PoliceStartGroundPosition;
            foreach (GreyboxRouteReference route in map.Routes)
            {
                if (route.Waypoints == null || route.Waypoints.Count == 0)
                {
                    continue;
                }

                if (route.From == GreyboxLocationId.PoliceSpawn)
                {
                    route.Waypoints[0].position = policeSpawn.position;
                }

                if (route.To == GreyboxLocationId.PoliceSpawn)
                {
                    route.Waypoints[route.Waypoints.Count - 1].position =
                        policeSpawn.position;
                }
            }

            PlayerRoleIdentity police = null;
            foreach (PlayerRoleIdentity identity in
                FindAllInScene<PlayerRoleIdentity>(scene))
            {
                if (identity.Role == PlayerRole.Police)
                {
                    police = identity;
                    break;
                }
            }

            if (police == null)
            {
                throw new InvalidOperationException(
                    "MAP-002 requires a Police player identity.");
            }

            police.transform.position =
                PoliceStartGroundPosition + Vector3.up;
            foreach (CompanionAgent companion in
                FindAllInScene<CompanionAgent>(scene))
            {
                if (companion.Owner == police.transform)
                {
                    companion.transform.position =
                        police.transform.position
                        + new Vector3(2f, 0f, 0f);
                }
            }

            PawsAndLoot.Gameplay.Camera.TopDownFollowCamera followCamera =
                FindInScene<
                    PawsAndLoot.Gameplay.Camera.TopDownFollowCamera>(scene);
            if (followCamera != null
                && followCamera.Target == police.transform)
            {
                followCamera.SnapToTarget();
            }
        }

        private static void CreateRoad(
            RoadSpec spec,
            GameObject roadModelAsset,
            Material displayMaterial,
            Transform parent)
        {
            Vector3 position;
            if (spec.IsHorizontal)
            {
                position = new Vector3(
                    (spec.Minimum + spec.Maximum) * 0.5f,
                    RoadHeight * 0.5f,
                    spec.FixedCoordinate);
            }
            else
            {
                position = new Vector3(
                    spec.FixedCoordinate,
                    RoadHeight * 0.5f,
                    (spec.Minimum + spec.Maximum) * 0.5f);
            }

            var road = new GameObject(spec.Name);
            road.name = spec.Name;
            road.transform.SetParent(parent);
            road.transform.position = position;
            road.transform.rotation = Quaternion.identity;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(
                roadModelAsset,
                road.transform);
            model.name = "Road Section FBX";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (Collider collider in
                model.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            foreach (Renderer renderer in
                model.GetComponentsInChildren<Renderer>(true))
            {
                int slotCount = Mathf.Max(
                    1,
                    renderer.sharedMaterials.Length);
                var materials = new Material[slotCount];
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = displayMaterial;
                }

                renderer.sharedMaterials = materials;
            }

            if (!PlaceholderModelLibrary.TryGetWorldBounds(
                    model,
                    out Bounds sourceBounds)
                || sourceBounds.size.x <= 0.001f
                || sourceBounds.size.y <= 0.001f
                || sourceBounds.size.z <= 0.001f)
            {
                UnityEngine.Object.DestroyImmediate(road);
                throw new InvalidOperationException(
                    $"MAP-002 road FBX has no measurable renderers: "
                    + RoadModelPath);
            }

            float length = spec.Maximum - spec.Minimum;
            model.transform.localScale = new Vector3(
                RoadWidth / sourceBounds.size.x,
                RoadHeight / sourceBounds.size.y,
                length / sourceBounds.size.z);
            road.transform.rotation = spec.IsHorizontal
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.identity;

            if (!PlaceholderModelLibrary.TryGetWorldBounds(
                    model,
                    out Bounds fittedBounds))
            {
                UnityEngine.Object.DestroyImmediate(road);
                throw new InvalidOperationException(
                    $"MAP-002 road FBX could not be fitted for "
                    + $"'{spec.Name}'.");
            }

            model.transform.position += new Vector3(
                position.x - fittedBounds.center.x,
                -fittedBounds.min.y,
                position.z - fittedBounds.center.z);
        }

        private static void ValidateRoad(
            RoadSpec spec,
            Transform roadRoot)
        {
            Transform road = roadRoot.Find(spec.Name);
            if (road == null
                || road.childCount != 1
                || road.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    $"MAP-002 road '{spec.Name}' requires one collider-free "
                    + "FBX visual.");
            }

            Transform model = road.GetChild(0);
            string prefabPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    model.gameObject);
            if (prefabPath != RoadModelPath
                || !PlaceholderModelLibrary.TryGetWorldBounds(
                    model.gameObject,
                    out Bounds bounds))
            {
                throw new InvalidOperationException(
                    $"MAP-002 road '{spec.Name}' does not use the required "
                    + $"FBX: {RoadModelPath}");
            }

            foreach (Renderer renderer in
                model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null
                        || AssetDatabase.GetAssetPath(material)
                            != RoadDisplayMaterialPath
                        || material.shader == null
                        || material.shader.name
                            != "Universal Render Pipeline/Lit"
                        || AssetDatabase.GetAssetPath(
                                material.GetTexture("_BaseMap"))
                            != RoadTexturePath)
                    {
                        throw new InvalidOperationException(
                            $"MAP-002 road '{spec.Name}' requires the "
                            + "textured URP road material.");
                    }
                }
            }

            float expectedLength = spec.Maximum - spec.Minimum;
            Vector3 expectedSize = spec.IsHorizontal
                ? new Vector3(expectedLength, RoadHeight, RoadWidth)
                : new Vector3(RoadWidth, RoadHeight, expectedLength);
            if (Vector3.Distance(bounds.center, road.position) > 0.01f
                || Vector3.Distance(bounds.size, expectedSize) > 0.02f)
            {
                throw new InvalidOperationException(
                    $"MAP-002 road '{spec.Name}' FBX bounds do not match "
                    + "the authored road slot.");
            }
        }

        private static Material CreateOrUpdateRoadDisplayMaterial()
        {
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(RoadTexturePath);
            if (texture == null)
            {
                throw new FileNotFoundException(
                    $"MAP-002 road texture is missing: {RoadTexturePath}",
                    RoadTexturePath);
            }

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "MAP-002 requires the URP Lit shader for its road FBX.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    RoadDisplayMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "MAP-002 Road FBX"
                };
                AssetDatabase.CreateAsset(
                    material,
                    RoadDisplayMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateSolidMaterial(
            string assetPath,
            Color color,
            float smoothness)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "MAP-002 trees require the URP Lit shader.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateBuilding(
            BuildingSpec spec,
            Transform parent)
        {
            Transform slot = CreateChild(spec.Name, parent);
            Transform anchor = CreateChild(
                $"{spec.ModelStem} Anchor",
                slot);
            Vector3 groundCenter =
                new(spec.X, 0f, spec.Z);
            anchor.position = groundCenter;
            anchor.rotation = Quaternion.identity;

            // The library reports the placed size, and zero when it could not place
            // the model at all. Only the height is wanted here — the footprint is
            // what was asked for, not what came back.
            float height = PlaceholderModelLibrary.TryInstantiateBuildingSized(
                spec.ModelStem,
                anchor,
                groundCenter,
                spec.FootprintX,
                spec.FootprintZ).y;
            if (height <= 0f)
            {
                UnityEngine.Object.DestroyImmediate(slot.gameObject);
                throw new InvalidOperationException(
                    $"Building model '{spec.ModelStem}' could not be "
                    + $"placed for '{spec.Name}'.");
            }

            anchor.rotation = Quaternion.Euler(0f, spec.RotationY, 0f);
            BoxCollider box = anchor.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(
                spec.FootprintX,
                height,
                spec.FootprintZ);
        }

        private static void CreateTree(
            TreeSpec spec,
            Material trunkMaterial,
            Material canopyMaterial,
            Transform parent)
        {
            Transform tree = CreateChild(spec.Name, parent);
            tree.position = new Vector3(spec.X, 0f, spec.Z);
            tree.rotation = Quaternion.Euler(0f, spec.RotationY, 0f);

            float trunkHeight = 2.4f * spec.Scale;
            CreateTreePart(
                PrimitiveType.Cylinder,
                "Trunk",
                new Vector3(0f, trunkHeight * 0.5f, 0f),
                new Vector3(
                    0.55f * spec.Scale,
                    trunkHeight * 0.5f,
                    0.55f * spec.Scale),
                trunkMaterial,
                true,
                tree);
            CreateTreePart(
                PrimitiveType.Sphere,
                "Lower Canopy",
                new Vector3(
                    0f,
                    trunkHeight + 0.45f * spec.Scale,
                    0f),
                new Vector3(2.3f, 1.9f, 2.3f) * spec.Scale,
                canopyMaterial,
                false,
                tree);
            CreateTreePart(
                PrimitiveType.Sphere,
                "Upper Canopy",
                new Vector3(
                    0.25f * spec.Scale,
                    trunkHeight + 1.25f * spec.Scale,
                    -0.15f * spec.Scale),
                new Vector3(1.7f, 1.5f, 1.7f) * spec.Scale,
                canopyMaterial,
                false,
                tree);

            GameObjectUtility.SetStaticEditorFlags(
                tree.gameObject,
                StaticEditorFlags.BatchingStatic);
        }

        private static void CreateTreePart(
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider,
            Transform parent)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(
                    part.GetComponent<Collider>());
            }

            GameObjectUtility.SetStaticEditorFlags(
                part,
                StaticEditorFlags.BatchingStatic);
        }

        private static void ValidateTree(
            TreeSpec spec,
            Transform treeRoot,
            Transform roadRoot,
            Transform buildingRoot)
        {
            Transform tree = treeRoot.Find(spec.Name);
            if (tree == null
                || tree.childCount != 3
                || tree.GetComponentsInChildren<Collider>(true).Length != 1
                || !TryGetWorldBounds(tree.gameObject, out Bounds treeBounds))
            {
                throw new InvalidOperationException(
                    $"MAP-002 tree '{spec.Name}' has invalid geometry.");
            }

            Vector3 expectedPosition = new(spec.X, 0f, spec.Z);
            if (Vector3.Distance(tree.position, expectedPosition) > 0.001f
                || treeBounds.min.x < 0f
                || treeBounds.max.x > MapWidth
                || treeBounds.min.z < 0f
                || treeBounds.max.z > MapDepth)
            {
                throw new InvalidOperationException(
                    $"MAP-002 tree '{spec.Name}' is outside its authored site.");
            }

            foreach (Renderer renderer in
                tree.GetComponentsInChildren<Renderer>(true))
            {
                string expectedMaterialPath = renderer.transform.name == "Trunk"
                    ? TreeTrunkMaterialPath
                    : TreeCanopyMaterialPath;
                if (AssetDatabase.GetAssetPath(renderer.sharedMaterial)
                    != expectedMaterialPath)
                {
                    throw new InvalidOperationException(
                        $"MAP-002 tree '{spec.Name}' has an invalid material.");
                }
            }

            for (int index = 0; index < roadRoot.childCount; index++)
            {
                Transform road = roadRoot.GetChild(index);
                if (TryGetWorldBounds(road.gameObject, out Bounds roadBounds)
                    && OverlapsOnXZ(treeBounds, roadBounds))
                {
                    throw new InvalidOperationException(
                        $"MAP-002 tree '{spec.Name}' overlaps road "
                        + $"'{road.name}'.");
                }
            }

            foreach (BoxCollider buildingCollider in
                buildingRoot.GetComponentsInChildren<BoxCollider>(true))
            {
                if (OverlapsOnXZ(treeBounds, buildingCollider.bounds))
                {
                    throw new InvalidOperationException(
                        $"MAP-002 tree '{spec.Name}' overlaps building "
                        + $"'{buildingCollider.transform.parent.name}'.");
                }
            }
        }

        private static bool OverlapsOnXZ(Bounds first, Bounds second)
        {
            return first.min.x < second.max.x
                && first.max.x > second.min.x
                && first.min.z < second.max.z
                && first.max.z > second.min.z;
        }

        private static void CreateTrashBin(
            int index,
            Vector2 coordinate,
            Material material,
            Transform parent)
        {
            Transform bin = CreateChild($"Green Trash Bin {index}", parent);
            bin.position = new Vector3(
                coordinate.x,
                0f,
                coordinate.y);

            GameObject model = PlaceholderModelLibrary.TryInstantiateProp(
                "object_trash_can",
                bin,
                Vector3.zero,
                Vector3.zero,
                1f,
                material);
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(bin.gameObject);
                throw new InvalidOperationException(
                    "MAP-002 requires object_trash_can.fbx.");
            }

            if (!TryGetWorldBounds(model, out Bounds bounds)
                || bounds.size.y <= 0.001f)
            {
                UnityEngine.Object.DestroyImmediate(bin.gameObject);
                throw new InvalidOperationException(
                    $"Green Trash Bin {index} has no measurable renderers.");
            }

            float scale = TrashBinHeight / bounds.size.y;
            model.transform.localScale *= scale;
            if (!TryGetWorldBounds(model, out Bounds scaled))
            {
                UnityEngine.Object.DestroyImmediate(bin.gameObject);
                throw new InvalidOperationException(
                    $"Green Trash Bin {index} could not be scaled.");
            }

            Vector3 groundCenter = bin.position;
            model.transform.position += groundCenter - new Vector3(
                scaled.center.x,
                scaled.min.y,
                scaled.center.z);

            BoxCollider collider =
                bin.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(
                0f,
                TrashBinHeight * 0.5f,
                0f);
            collider.size = new Vector3(
                1.05f,
                TrashBinHeight,
                1.05f);
        }

        private static bool TryGetWorldBounds(
            GameObject target,
            out Bounds bounds)
        {
            Renderer[] renderers =
                target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return true;
        }

        private static void CreateCoordinateGrid(
            Transform parent,
            Material material)
        {
            Transform gridRoot =
                CreateChild("MAP-002 Coordinate Grid", parent);
            Transform lineRoot = CreateChild("Grid Lines", gridRoot);
            Transform axisLabelRoot = CreateChild("Axis Labels", gridRoot);
            Transform coordinateLabelRoot =
                CreateChild("Coordinate Labels", gridRoot);

            const float lineHeight = 0.055f;
            const float labelHeight = 0.065f;
            const float minorThickness = 0.018f;
            const float majorThickness = 0.06f;

            for (int x = 0; x <= Mathf.RoundToInt(MapWidth); x++)
            {
                float thickness = x % 5 == 0
                    ? majorThickness
                    : minorThickness;
                CreateGridLine(
                    $"X Grid {x}",
                    new Vector3(x, lineHeight, MapDepth * 0.5f),
                    new Vector3(thickness, 0.008f, MapDepth),
                    material,
                    lineRoot);

                if (x % 5 == 0)
                {
                    CreateGridLabel(
                        $"X Coordinate {x}",
                        $"X={x}",
                        new Vector3(x, labelHeight, 0.8f),
                        0.075f,
                        Color.white,
                        axisLabelRoot);
                }
            }

            for (int z = 0; z <= Mathf.RoundToInt(MapDepth); z++)
            {
                float thickness = z % 5 == 0
                    ? majorThickness
                    : minorThickness;
                CreateGridLine(
                    $"Z Grid {z}",
                    new Vector3(MapWidth * 0.5f, lineHeight, z),
                    new Vector3(MapWidth, 0.008f, thickness),
                    material,
                    lineRoot);

                if (z % 5 == 0)
                {
                    CreateGridLabel(
                        $"Z Coordinate {z}",
                        $"Z={z}",
                        new Vector3(0.9f, labelHeight, z),
                        0.075f,
                        Color.white,
                        axisLabelRoot);
                }
            }

            CreateGridLabel(
                "Positive X Axis",
                "+X",
                new Vector3(MapWidth - 1.4f, labelHeight, 2f),
                0.12f,
                Color.cyan,
                axisLabelRoot);
            CreateGridLabel(
                "Positive Z Axis",
                "+Z",
                new Vector3(2f, labelHeight, MapDepth - 1.4f),
                0.12f,
                Color.cyan,
                axisLabelRoot);

            for (int x = 10; x < Mathf.RoundToInt(MapWidth); x += 10)
            {
                for (int z = 10; z < Mathf.RoundToInt(MapDepth); z += 10)
                {
                    CreateGridLabel(
                        $"Coordinate ({x}, {z})",
                        $"({x},{z})",
                        new Vector3(x, labelHeight + 0.005f, z),
                        0.05f,
                        new Color(0.82f, 0.95f, 1f, 0.9f),
                        coordinateLabelRoot);
                }
            }
        }

        private static void CreateGridLine(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = name;
            line.transform.SetParent(parent);
            line.transform.position = position;
            line.transform.rotation = Quaternion.identity;
            line.transform.localScale = scale;
            line.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        private static void CreateGridLabel(
            string name,
            string value,
            Vector3 position,
            float characterSize,
            Color color,
            Transform parent)
        {
            var labelObject = new GameObject(name, typeof(TextMesh));
            labelObject.transform.SetParent(parent);
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh text = labelObject.GetComponent<TextMesh>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 32;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
        }

        private static void ConfigureSurface(
            Transform surface,
            Vector3 position,
            Vector3 scale)
        {
            surface.position = position;
            surface.rotation = Quaternion.identity;
            surface.localScale = scale;
            GreyboxObstacle obstacle =
                surface.GetComponent<GreyboxObstacle>();
            if (obstacle != null)
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(
                surface.gameObject);
        }

        private static void RecreateBoundary(
            Transform environment,
            string name,
            Vector3 position,
            Vector3 scale)
        {
            Transform previous = RequireDirectChild(environment, name);
            MeshRenderer previousRenderer =
                previous.GetComponent<MeshRenderer>();
            Material material = previousRenderer != null
                ? previousRenderer.sharedMaterial
                : null;
            StaticEditorFlags staticFlags =
                GameObjectUtility.GetStaticEditorFlags(previous.gameObject);
            UnityEngine.Object.DestroyImmediate(previous.gameObject);

            GameObject boundary =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            boundary.name = name;
            boundary.transform.SetParent(environment);
            boundary.transform.position = position;
            boundary.transform.rotation = Quaternion.identity;
            boundary.transform.localScale = scale;
            boundary.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(boundary, staticFlags);
        }

        private static Transform CreateChild(
            string name,
            Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            return child.transform;
        }

        private static void DestroyDirectChild(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static Transform RequireDirectChild(
            Transform parent,
            string childName)
        {
            Transform child = parent.Find(childName);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' is missing required child "
                    + $"'{childName}'.");
            }

            return child;
        }

        private static T FindInScene<T>(Scene scene)
            where T : Component
        {
            foreach (T component in FindAllInScene<T>(scene))
            {
                return component;
            }

            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            var results = new System.Collections.Generic.List<T>();
            foreach (GameObject root in roots)
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }

        private readonly struct RoadSpec
        {
            private RoadSpec(
                string name,
                bool isHorizontal,
                float fixedCoordinate,
                float minimum,
                float maximum)
            {
                Name = name;
                IsHorizontal = isHorizontal;
                FixedCoordinate = fixedCoordinate;
                Minimum = minimum;
                Maximum = maximum;
            }

            public string Name { get; }
            public bool IsHorizontal { get; }
            public float FixedCoordinate { get; }
            public float Minimum { get; }
            public float Maximum { get; }

            public static RoadSpec Horizontal(
                string name,
                float z,
                float minimumX,
                float maximumX)
            {
                return new RoadSpec(
                    name,
                    true,
                    z,
                    minimumX,
                    maximumX);
            }

            public static RoadSpec Vertical(
                string name,
                float x,
                float minimumZ,
                float maximumZ)
            {
                return new RoadSpec(
                    name,
                    false,
                    x,
                    minimumZ,
                    maximumZ);
            }
        }

        private readonly struct BuildingSpec
        {
            public BuildingSpec(
                string name,
                string modelStem,
                float x,
                float z,
                float footprintX,
                float footprintZ,
                float rotationY = 0f)
            {
                Name = name;
                ModelStem = modelStem;
                X = x;
                Z = z;
                FootprintX = footprintX;
                FootprintZ = footprintZ;
                RotationY = rotationY;
            }

            public string Name { get; }
            public string ModelStem { get; }
            public float X { get; }
            public float Z { get; }
            public float FootprintX { get; }
            public float FootprintZ { get; }
            public float RotationY { get; }

            public static BuildingSpec House1F(
                string name,
                float x,
                float z,
                float rotationY = 0f)
            {
                return new BuildingSpec(
                    name,
                    "building_house_1f",
                    x,
                    z,
                    OneStoreyHouseFootprint,
                    OneStoreyHouseFootprint,
                    rotationY);
            }

            public static BuildingSpec House2F(
                string name,
                float x,
                float z,
                float rotationY = 0f)
            {
                // MAP-002 currently unifies every house on the authored
                // one-storey model. The semantic slot name is preserved so
                // the requested layout can still be identified.
                return new BuildingSpec(
                    name,
                    "building_house_1f",
                    x,
                    z,
                    OneStoreyHouseFootprint,
                    OneStoreyHouseFootprint,
                    rotationY);
            }
        }

        private readonly struct TreeSpec
        {
            public TreeSpec(
                string name,
                float x,
                float z,
                float scale,
                float rotationY)
            {
                Name = name;
                X = x;
                Z = z;
                Scale = scale;
                RotationY = rotationY;
            }

            public string Name { get; }
            public float X { get; }
            public float Z { get; }
            public float Scale { get; }
            public float RotationY { get; }
        }
    }
}
