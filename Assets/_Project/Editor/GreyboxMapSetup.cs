using System;
using System.Collections.Generic;
using System.IO;
using PawsAndLoot.Animation;
using PawsAndLoot.Audio;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Map;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
using PawsAndLoot.Integration.Network;
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

        /// <summary>
        /// The chase camera looks due north from south of the player and never
        /// yaws. The village is strictly axis aligned, so a straight southern
        /// view keeps every road horizontal or vertical on screen, maps WASD
        /// onto the map axes with no diagonal ambiguity, and minimises the
        /// wall occlusion an angled view would create.
        ///
        /// Switching to a south-east view only needs an X value here, for
        /// example (-11f, 16f, -11f) for a 45 degree yaw; the camera and the
        /// movement basis both follow this single offset.
        /// </summary>
        /// <summary>
        /// Pulled in from the original (0, 16, -14) in two steps: 15% then a
        /// further 10%, so everything on screen is about 29% larger than it
        /// started. The village read as too small in frame to follow the
        /// characters and animals.
        ///
        /// The angle is unchanged: both components are scaled by the same
        /// factor, so the camera moves along the same line and the fixed
        /// rotation derived from this offset stays identical.
        /// </summary>
        private static readonly Vector3 FixedCameraOffset =
            new(0f, 12.36f, -10.82f);

        /// <summary>
        /// Village extents. The original 56x44 block is kept exactly where it
        /// was and the map grows only north and east, so every existing route,
        /// spawn point, building and ladder keeps its coordinates. Rescaling
        /// around the origin instead would have moved all of them and
        /// invalidated the MAP-001 route validation.
        ///
        /// Two rows north and two columns east, at the 12 m block pitch the
        /// original grid already uses.
        /// </summary>
        private const float MapMinX = -28f;
        private const float MapMaxX = 52f;
        private const float MapMinZ = -22f;
        private const float MapMaxZ = 46f;

        private const float MapWidth = MapMaxX - MapMinX;
        private const float MapDepth = MapMaxZ - MapMinZ;
        private const float MapCenterX = (MapMinX + MapMaxX) * 0.5f;
        private const float MapCenterZ = (MapMinZ + MapMaxZ) * 0.5f;

        /// <summary>
        /// Through-roads stop short of the wall by this much, matching the
        /// original layout where the loop roads were inset from the ground edge.
        /// </summary>
        private const float RoadInset = 2f;

        private const float RoadSpanX = MapWidth - RoadInset * 2f;
        private const float RoadSpanZ = MapDepth - RoadInset * 2f;

        /// <summary>
        /// The expansion street grid.
        ///
        /// Every value is chosen against the lanes the original grid already
        /// occupies — verticals at x = -24, -18, 0, 18, 24 and horizontals at
        /// z = -18, -12, 0, 12, 18 — so a new street never lands on top of one
        /// or a metre away from it. The house rows below sit in the gaps these
        /// leave, which is what makes the district read as blocks.
        /// </summary>
        private const float NorthStreetNear = 26f;
        private const float NorthStreetFar = 39f;
        private const float EastStreet = 40f;

        /// <summary>
        /// The band freed by dropping the old north outer alley at z = 18.
        ///
        /// That alley was 4 m from the first district street, leaving a sliver
        /// of bare ground no block could use, and the two authored houses were
        /// already sitting on top of it and blocking it. No route referenced it
        /// — the eight waypoints on z = -18 are all the southern alley — so the
        /// district street replaces it and this band becomes buildable.
        /// </summary>
        private const float NorthBandRow = 19f;

        private const float NorthRowNear = 32.5f;
        private const float NorthRowFar = 43.4f;
        private const float EastColumnNear = 32f;
        private const float EastColumnFar = 47f;

        /// <summary>
        /// Authored characters arrive normalised to a roughly one-unit box, so
        /// their relative sizes carry no meaning and each one is scaled to an
        /// explicit height. The players share a height so neither role reads as
        /// larger, and the animals are sized against them.
        /// </summary>
        private const float AuthoredCharacterHeight = 1.7f;

        /// <summary>
        /// The companions read as roughly half a player. Realistic pet sizes
        /// made them hard to pick out from the greybox at the fixed camera
        /// distance, and the animals are half the game.
        ///
        /// The cat stays a little under the dog so the two silhouettes are still
        /// distinguishable at a glance.
        /// </summary>
        private const float AuthoredDogHeight = 1.36f;
        private const float AuthoredCatHeight = 1.2f;

        /// <summary>
        /// The merchant is a character the player deals with, not scenery, so it
        /// is nearly player-sized. The bin is taller than the raccoon on
        /// purpose: it has to read as something the raccoon is hiding *inside*,
        /// which it cannot do if the two are the same height.
        /// </summary>
        private const float AuthoredRaccoonHeight = 1.35f;
        private const float TrashBinHeight = 2.08f;

        [MenuItem("Paws & Loot/Setup/Rebuild MAP-001 Greybox Village")]
        public static void CreateGameScene()
        {
            PlaceholderModelLibrary.ResetMissingAssetLog();
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
            var pendingLadderClimbs = new List<LadderTraversal>();
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
                ladders,
                pendingLadderClimbs,
                "building_supermarket");
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
                ladders,
                pendingLadderClimbs,
                "building_bookstore");
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
                ladders,
                pendingLadderClimbs);
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
                MapWidth,
                MapDepth,
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

            // MAP-003 ladders need the match runtime, which only exists now.
            foreach (LadderTraversal climb in pendingLadderClimbs)
            {
                climb.Configure(
                    climb.GetComponent<Collider>(),
                    climb.transform.Find("Bottom"),
                    climb.transform.Find("Top"),
                    matchRuntime);
            }

            Debug.Log(
                $"[MAP-003] {pendingLadderClimbs.Count} climbable ladders "
                + "configured.");
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
            ConfigureMatchResultEvaluator(
                controlBindings,
                matchRuntime);
            PawsAndLoot.Gameplay.Camera.TopDownFollowCamera followCamera =
                ConfigurePlayerFollowCamera(
                    roleMarkers[PlayerRole.Police].transform);
            LocalPlayerRoleSelector roleSelector = CreateLocalRoleSelector(
                villageRoot.transform,
                controlBindings,
                followCamera);
            MatchEndController matchEndController =
                ConfigureMatchEndController(
                controlBindings,
                matchRuntime,
                roleSelector);
            ConfigureMatchResultFlow(
                matchRuntime,
                matchEndController);
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
            CreateAuthoredSceneDressing(
                villageRoot.transform,
                locations);
            CompanionCommandDispatcher companionDispatcher =
                CreateCompanions(
                    villageRoot.transform,
                    controlBindings,
                    matchRuntime,
                    matchEndController);
            CreateSceneInterface(
                roleSelector,
                matchRuntime,
                companionDispatcher);

            CreateAudio(
                villageRoot.transform,
                companionDispatcher,
                controlBindings,
                matchRuntime,
                matchEndController);
            CreateNetworkSync(
                villageRoot.transform,
                controlBindings,
                matchRuntime);

            // ART-005 probe, inert unless -perfProbe is passed.
            var perfObject = new GameObject("Scene Performance Probe");
            perfObject.transform.SetParent(villageRoot.transform);
            perfObject.AddComponent<
                PawsAndLoot.TechnicalValidation.ScenePerformanceProbe>();

            // ART-012 runs last so it sees every generated object.
            SceneOptimizationPass.Run(villageRoot);

            string scenePath = GameSceneCatalog.GetPath(GameSceneId.Game);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new InvalidOperationException(
                    $"Failed to save MAP-001 scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            ValidateScene();
            foreach (string missing in
                PlaceholderModelLibrary.MissingAssetPaths)
            {
                Debug.LogWarning(
                    $"[Placeholder] Model missing, kept greybox primitive: "
                    + $"{missing}");
            }

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
            MatchResultFlowController resultFlow =
                FindInScene<MatchResultFlowController>(scene);

            if (map == null
                || probe == null
                || camera == null
                || camera.orthographic
                || canvas == null
                || eventSystem == null
                || resultFlow == null)
            {
                throw new InvalidOperationException(
                    "MAP-001 scene requires its map, traversal probe, perspective camera, UI, and result flow.");
            }

            map.ValidateOrThrow();
            resultFlow.ValidateOrThrow();
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
            // Night sky. Dark enough to read as night, not so dark that the
            // greybox map stops being legible from the fixed camera.
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.15f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;

            // Moonlight rather than daylight. Kept as a directional light so
            // the buildings still cast readable shadows and the map keeps its
            // shape; it is the intensity and colour that say "night", not the
            // absence of light.
            //
            // Raised from the first pass. 0.32 read as night but was oppressive
            // to actually play in: the town stopped being readable, which on a
            // fixed camera means the player loses track of where they are rather
            // than feeling hunted. Street lamps are coming, and this is the
            // level the map has to be legible at before they arrive — they
            // should be pools of interest, not the only way to see the road.
            var lightObject = new GameObject(
                "Moonlight",
                typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            Light light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.62f;
            light.color = new Color(0.66f, 0.74f, 1f);
            light.shadowStrength = 0.68f;

            // The old point light existed to flatten the daytime scene. At night
            // a broad fill would undo the whole effect, so it becomes a faint
            // sky bounce instead of a lamp — lifted alongside the moon so
            // unlit faces and alley walls do not go to solid black.
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight =
                new Color(0.20f, 0.23f, 0.33f);
            RenderSettings.fog = false;
        }

        /// <summary>
        /// One player's night adaptation, as a directional fill lit only on that
        /// player's own screen.
        ///
        /// A directional light because it has to lift the whole town rather than
        /// follow the character like a second torch — that would hand the thief a
        /// lantern and undo the night for both of them.
        ///
        /// The thief gets roughly three times the police's. The night exists to
        /// stop the officer seeing across the map, and the story that pays for
        /// the asymmetry is simply that a cat burglar works in the dark.
        /// </summary>
        private static void CreateNightVisionFill(
            GameObject player,
            PlayerRoleIdentity identity,
            MatchRuntimeState matchRuntime,
            PlayerRole role)
        {
            var fillObject = new GameObject(
                "Night Vision Fill",
                typeof(Light));
            fillObject.transform.SetParent(player.transform, false);
            // Steeper than the moon and from the other side, so it fills the
            // faces the moonlight leaves black instead of doubling it.
            fillObject.transform.rotation =
                Quaternion.Euler(62f, 140f, 0f);

            Light fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            // Measured against the plan-view render: the scene without any fill
            // averages 19% luminance with no crushed blacks, so 0.42 lifts the
            // thief's screen roughly a third above the officer's without
            // turning their night into dusk.
            fill.intensity = role == PlayerRole.Thief ? 0.42f : 0.16f;
            fill.color = role == PlayerRole.Thief
                // Faintly cool. A warm fill at night reads as dawn, and the
                // thief is meant to see better, not to see a different time of
                // day.
                ? new Color(0.72f, 0.8f, 0.95f)
                : new Color(0.68f, 0.74f, 0.9f);
            // No shadows. A second shadow-casting light at night doubles every
            // building's shadow and the map stops reading.
            fill.shadows = LightShadows.None;

            player.AddComponent<
                    PawsAndLoot.Gameplay.Players.NightVisionFill>()
                .Configure(identity, matchRuntime, fill);
        }

        /// <summary>
        /// The officer's torch: the light, and the outline of what it means.
        ///
        /// The light alone was not enough to play against. A spot light fades
        /// out, so both players had to guess where the edge of "seen" was, and
        /// the light's own angle did not even match the rule. Now the shape
        /// comes from <c>FlashlightCone</c> and the wedge is drawn on the ground
        /// for both of them.
        /// </summary>
        private static void CreatePoliceFlashlight(
            GameObject police,
            MatchRuntimeState matchRuntime,
            PlayerRoleIdentity identity)
        {
            var beamObject = new GameObject("Flashlight", typeof(Light));
            beamObject.transform.SetParent(police.transform, false);
            beamObject.transform.localPosition =
                new Vector3(0f, 1.35f, 0.25f);

            Light beam = beamObject.GetComponent<Light>();
            beam.type = LightType.Spot;
            // Range and angle from the same constants the visibility rule reads,
            // so the lit floor is an honest picture of what the officer can see.
            beam.range = PawsAndLoot.Gameplay.Players
                .FlashlightCone.RangeMeters;
            beam.spotAngle = PawsAndLoot.Gameplay.Players
                .FlashlightCone.SpotAngleDegrees;
            beam.innerSpotAngle = PawsAndLoot.Gameplay.Players
                .FlashlightCone.SpotAngleDegrees * 0.5f;
            beam.intensity = 7f;
            beam.color = new Color(1f, 0.96f, 0.82f);
            // Shadows off on purpose: a spot light chasing a running character
            // through a greybox town produces more flicker than atmosphere.
            beam.shadows = LightShadows.None;

            police.AddComponent<
                    PawsAndLoot.Gameplay.Players.PoliceFlashlight>()
                .Configure(beam, matchRuntime);

            // The wedge on the ground, shown to both players. The thief needs it
            // more than the officer does: dodging around a beam is only a plan
            // if the beam has a visible edge.
            //
            // Its own object, not the player's, because it must stay flat on the
            // road while the character it follows leans and turns.
            var coneObject = new GameObject("Flashlight Cone View");
            coneObject.transform.SetParent(police.transform, false);
            coneObject.AddComponent<
                    PawsAndLoot.Animation.FlashlightConeView>()
                .Configure(
                    identity,
                    matchRuntime,
                    LoadOrCreateGlowMaterial(
                        "Greybox_TorchWedge",
                        new Color(1f, 0.93f, 0.66f, 0.10f)),
                    LoadOrCreateGlowMaterial(
                        "Greybox_TorchNear",
                        new Color(1f, 0.88f, 0.55f, 0.20f)));
        }

        /// <summary>
        /// A flat translucent marking that does not go dark at night.
        ///
        /// Unlit on purpose: a lit material for a night-time overlay would be
        /// lit by the very moonlight the overlay exists to compensate for, and
        /// the cone outline would be dimmest exactly when it matters. Transparent
        /// with depth writing off so it reads as paint on the road rather than a
        /// pane of glass standing on it.
        /// </summary>
        private static Material LoadOrCreateGlowMaterial(
            string assetName,
            Color color,
            bool doubleSided = false)
        {
            Material material = LoadOrCreateMaterial(
                assetName,
                color,
                "Universal Render Pipeline/Unlit");
            // 1 = Transparent. URP reads the property, the blend factors and the
            // keyword, so all three have to be set or the material stays opaque.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat(
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue =
                (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (doubleSided)
            {
                // 0 = Off. URP reads the property and the render state, so both
                // have to be set.
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateGroundAndBoundaries(
            Transform parent,
            Material ground,
            Material boundary)
        {
            CreateCube(
                "Ground",
                new Vector3(MapCenterX, -0.15f, MapCenterZ),
                new Vector3(MapWidth, 0.3f, MapDepth),
                ground,
                parent,
                false);

            CreateCube(
                "North Boundary",
                new Vector3(MapCenterX, 1f, MapMaxZ),
                new Vector3(MapWidth, 2f, 1f),
                boundary,
                parent,
                true);
            CreateCube(
                "South Boundary",
                new Vector3(MapCenterX, 1f, MapMinZ),
                new Vector3(MapWidth, 2f, 1f),
                boundary,
                parent,
                true);
            CreateCube(
                "West Boundary",
                new Vector3(MapMinX, 1f, MapCenterZ),
                new Vector3(1f, 2f, MapDepth),
                boundary,
                parent,
                true);
            CreateCube(
                "East Boundary",
                new Vector3(MapMaxX, 1f, MapCenterZ),
                new Vector3(1f, 2f, MapDepth),
                boundary,
                parent,
                true);
        }

        private static void CreateRoadNetwork(
            Transform parent,
            Material road,
            Material plaza)
        {
            // Every through-road now spans the widened map. Leaving the old
            // lengths would have left the new district reachable only by
            // walking off the end of a road.
            CreateFlatTile(
                "Central East-West Road",
                new Vector3(MapCenterX, 0.02f, 0f),
                new Vector3(MapWidth, 0.04f, 4f),
                road,
                parent);
            CreateFlatTile(
                "North Loop Road",
                new Vector3(MapCenterX, 0.025f, 12f),
                new Vector3(RoadSpanX, 0.05f, 4f),
                road,
                parent);
            CreateFlatTile(
                "South Loop Road",
                new Vector3(MapCenterX, 0.025f, -12f),
                new Vector3(RoadSpanX, 0.05f, 4f),
                road,
                parent);
            // The north outer alley that used to run along z = 18 is gone; the
            // district street at z = 26 does its job without stranding a
            // 0.5 m strip between the two. The southern alley stays: eight
            // route waypoints run along it.
            CreateFlatTile(
                "South Outer Alley",
                new Vector3(MapCenterX, 0.03f, -18f),
                new Vector3(RoadSpanX, 0.06f, 3f),
                road,
                parent);

            // Expansion streets. Placed so the new districts read as city
            // blocks rather than two stripes across an empty field: each street
            // bounds a block that is actually filled with houses, and the
            // spacing matches the block depth the original grid already uses.
            CreateFlatTile(
                "North District Road",
                new Vector3(MapCenterX, 0.025f, NorthStreetNear),
                new Vector3(RoadSpanX, 0.05f, 4f),
                road,
                parent);
            CreateFlatTile(
                "North Ridge Road",
                new Vector3(MapCenterX, 0.025f, NorthStreetFar),
                new Vector3(RoadSpanX, 0.05f, 4f),
                road,
                parent);

            // One new vertical, not two. A second would have landed within a
            // metre of the existing x = 24 alley and left a sliver of bare
            // ground between them instead of a block.
            foreach (float x in
                new[] { -24f, -18f, 0f, 18f, 24f, EastStreet })
            {
                bool wide = x == 0f
                    || Mathf.Abs(x) == 18f
                    || x == EastStreet;
                CreateFlatTile(
                    $"Vertical Route {x:0}",
                    new Vector3(x, 0.035f, MapCenterZ),
                    new Vector3(
                        wide ? 4f : 3f,
                        0.07f,
                        RoadSpanZ),
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

        /// <summary>
        /// Places the authored models that have no gameplay system yet: the
        /// police station and houses the GDD map lists, the raccoon merchant
        /// at its trading yard, and the two animals beside their owners.
        ///
        /// These are visual only. They carry no collider so they cannot block
        /// the validated MAP-001 routes, and the animals have no AI until
        /// COMP-001 lands. MAP-002 adds the real collision pass.
        /// </summary>
        private static void CreateAuthoredSceneDressing(
            Transform parent,
            IReadOnlyDictionary<GreyboxLocationId, Transform> locations)
        {
            Transform root = CreateChild("Authored Dressing", parent);
            root.localPosition = Vector3.zero;

            // MAP-002. Each dressing building gets a simple box collider sized
            // from its measured height, never its visual mesh.
            // All three sit in the band between the north loop road and the
            // first district street, on the block grid rather than across it.
            //
            // Their old positions were both wrong and only visible from above:
            // the station was centred at x = -25 with a 12 m footprint, so a
            // third of it stood outside the west wall, and the two houses were
            // laid across the z = 18 alley, blocking a road the player could
            // otherwise run down.
            CreateDressingBuilding(
                "building_police_station",
                root,
                new Vector3(-9f, 0f, NorthBandRow),
                12f,
                8f);
            CreateDressingBuilding(
                "building_house_1f",
                root,
                new Vector3(9f, 0f, NorthBandRow),
                12f,
                8f);
            CreateDressingBuilding(
                "building_house_1f_with_interior",
                root,
                new Vector3(EastColumnNear, 0f, NorthBandRow),
                9f,
                8f);

            // Inside the trading yard (x -15..-4, z -9.5..-2.5), not beside it.
            // The previous spot put the bin on the southern alley, and once the
            // bin grew it blocked the PoliceToJewelry_SouthLoop route outright.
            CreateRaccoonInBin(
                root,
                locations[GreyboxLocationId.RaccoonMarket].position
                    + new Vector3(-3.5f, 0f, 5f));

            CreateExpansionDistricts(root);
        }

        /// <summary>
        /// Fills the two northern rows and two eastern columns added to the map.
        ///
        /// Every footprint below sits in a gap between road tiles, not on one:
        /// the verticals occupy x = -24, -18, 0, 18, 24, 30, 42 and the
        /// horizontals z = -18, -12, 0, 12, 18, 22, 36. Buildings are sized to
        /// leave those lanes clear, because a box collider dropped onto a road
        /// would silently close a route the MAP-001 validation assumes is open.
        ///
        /// Only two house models exist, so they alternate. They are the same
        /// dressing buildings used elsewhere: a real collider sized from the
        /// measured model height, and no gameplay behaviour of their own.
        /// </summary>
        private static void CreateExpansionDistricts(Transform root)
        {
            int placed = 0;

            // Two houses per block rather than one. A single house in a 14 m
            // block left the district reading as empty ground with a building
            // dropped in it; a pair with an alley between them reads as a
            // street of houses, which is what the concept map shows.
            // One more house in the band the removed alley freed, so that band
            // reads as a block like the rest instead of leftover ground.
            placed += PlaceHouseRow(
                root,
                new[] { new Vector3(EastColumnFar, 0f, NorthBandRow) },
                8f,
                8f,
                placed);

            float[] narrowX = { -12.5f, -5.5f, 5.5f, 12.5f };
            foreach (float rowZ in
                new[] { NorthRowNear, NorthRowFar })
            {
                float depth = rowZ == NorthRowNear ? 8f : 4.5f;
                var row = new List<Vector3>();
                foreach (float x in narrowX)
                {
                    row.Add(new Vector3(x, 0f, rowZ));
                }

                placed += PlaceHouseRow(root, row, 6f, depth, placed);

                // The same row continued into the eastern district, where the
                // blocks are wider so the houses are too.
                placed += PlaceHouseRow(
                    root,
                    new[]
                    {
                        new Vector3(EastColumnNear, 0f, rowZ)
                    },
                    9f,
                    depth,
                    placed);
                placed += PlaceHouseRow(
                    root,
                    new[]
                    {
                        new Vector3(EastColumnFar, 0f, rowZ)
                    },
                    8f,
                    depth,
                    placed);
            }

            // The eastern columns also run south, alongside the original core,
            // filling the blocks the widened map opened up beside it.
            foreach (float columnZ in new[] { -6f, 6f })
            {
                placed += PlaceHouseRow(
                    root,
                    new[]
                    {
                        new Vector3(EastColumnNear, 0f, columnZ)
                    },
                    9f,
                    6f,
                    placed);
                placed += PlaceHouseRow(
                    root,
                    new[]
                    {
                        new Vector3(EastColumnFar, 0f, columnZ)
                    },
                    8f,
                    6f,
                    placed);
            }

            Debug.Log(
                $"[MAP-006] {placed} houses placed in the north and east "
                + "expansion districts.");
        }

        private static int PlaceHouseRow(
            Transform root,
            IReadOnlyList<Vector3> centers,
            float footprintX,
            float footprintZ,
            int startIndex)
        {
            string[] stems =
            {
                "building_house_1f",
                "building_house_1f_with_interior"
            };

            for (int index = 0; index < centers.Count; index++)
            {
                CreateDressingBuilding(
                    stems[(startIndex + index) % stems.Length],
                    root,
                    centers[index],
                    footprintX,
                    footprintZ);
            }

            return centers.Count;
        }

        /// <summary>
        /// COMP-001 scene assembly. Creates one companion per role, wires the
        /// dispatcher between the input adapters and the agents, and returns
        /// the dispatcher so match end can disable both animals.
        /// </summary>
        private static CompanionCommandDispatcher CreateCompanions(
            Transform parent,
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime,
            MatchEndController matchEndController)
        {
            Transform root = CreateChild("Companions", parent);
            root.localPosition = Vector3.zero;
            CompanionConfig config = LoadCompanionConfig();

            // DOG-003 and CAT-004 shared services. The trail lives on the
            // thief because only the thief writes it.
            PlayerRoleControlBinding policeBinding =
                FindBinding(bindings, PlayerRole.Police);
            PlayerRoleControlBinding thiefBinding =
                FindBinding(bindings, PlayerRole.Thief);
            ThiefScentTrail scentTrail =
                thiefBinding.Identity.gameObject
                    .AddComponent<ThiefScentTrail>();
            scentTrail.Configure(0.5f, 12f, 0.75f);

            var boardObject = new GameObject("Distraction Board");
            boardObject.transform.SetParent(root, false);
            DistractionBoard distractionBoard =
                boardObject.AddComponent<DistractionBoard>();
            distractionBoard.Configure(4f);

            var agents = new List<CompanionAgent>();
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                CompanionKind kind =
                    CompanionCommandCatalog.GetCompanionKind(binding.Role);
                string stem = kind == CompanionKind.Dog ? "dog" : "cat";
                float height = kind == CompanionKind.Dog
                    ? AuthoredDogHeight
                    : AuthoredCatHeight;

                var agentObject = new GameObject($"{kind} Companion");
                agentObject.transform.SetParent(root, false);
                agentObject.transform.position =
                    binding.Identity.transform.position
                    + new Vector3(1.6f, -1f, -1.2f);

                // A CharacterController gives the companion the same wall and
                // step collision the players use, sized to the animal.
                CharacterController controller =
                    agentObject.AddComponent<CharacterController>();
                controller.height = Mathf.Max(0.4f, height);
                controller.radius = Mathf.Max(0.15f, height * 0.32f);
                controller.center =
                    new Vector3(0f, controller.height * 0.5f, 0f);
                controller.slopeLimit = 50f;
                controller.stepOffset = Mathf.Min(0.3f, height * 0.4f);
                controller.skinWidth = 0.04f;

                Transform visualRoot = CreateChild(
                    "VisualRoot",
                    agentObject.transform);
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;

                if (PlaceholderModelLibrary.TryInstantiateAuthoredCharacter(
                        stem,
                        visualRoot,
                        height,
                        0f) == null)
                {
                    GameObject fallback = GameObject.CreatePrimitive(
                        PrimitiveType.Capsule);
                    fallback.name = $"{stem}_Fallback";
                    fallback.transform.SetParent(visualRoot, false);
                    fallback.transform.localScale =
                        Vector3.one * height * 0.5f;
                    UnityEngine.Object.DestroyImmediate(
                        fallback.GetComponent<Collider>());
                }

                CompanionCommandResolver resolver =
                    agentObject.AddComponent<CompanionCommandResolver>();
                resolver.Configure(
                    scentTrail,
                    distractionBoard,
                    policeBinding.Identity.transform,
                    26f,
                    thiefBinding.Identity.transform);

                // CAT-005. Only the cat couriers loot, and it hands over to the
                // thief's own carrier so the one-item limit still applies.
                CompanionLootCourier courier = null;
                if (kind == CompanionKind.Cat)
                {
                    Transform catCarryPoint = CreateChild(
                        "CarryPoint",
                        agentObject.transform);
                    catCarryPoint.localPosition =
                        new Vector3(0f, height * 0.75f, height * 0.5f);
                    courier = agentObject.AddComponent<
                        CompanionLootCourier>();
                    courier.Configure(
                        catCarryPoint,
                        thiefBinding.Identity
                            .GetComponent<LootCarrier>());
                }

                CompanionAgent agent =
                    agentObject.AddComponent<CompanionAgent>();
                agent.Configure(
                    kind,
                    binding.Identity.transform,
                    config,
                    matchRuntime,
                    controller,
                    resolver,
                    courier);

                // Real leg bones are swung so the animals walk rather than
                // slide. Reported so a rig without recognisable legs is
                // obvious in the rebuild log.
                //
                // Created before the body animator because the body takes its
                // rhythm from the legs.
                PawsAndLoot.Animation.CompanionLegAnimator legs =
                    agentObject.AddComponent<
                        PawsAndLoot.Animation.CompanionLegAnimator>();
                legs.Configure(visualRoot);

                // Quadrupeds have no clips, so movement is faked on the visual
                // child only, never on the collider root. The body rise follows
                // the legs' footfalls rather than a timer of its own.
                PawsAndLoot.Animation.CompanionProceduralAnimator hop =
                    agentObject.AddComponent<
                        PawsAndLoot.Animation.
                            CompanionProceduralAnimator>();
                hop.Configure(agent, visualRoot, legs);

                if (legs.LegCount == 0)
                {
                    Debug.LogWarning(
                        $"[Placeholder] {kind} has no recognisable leg bones, "
                        + "so only the body hop will play.");
                }
                else
                {
                    Debug.Log(
                        $"[Placeholder] {kind} leg bones driven: "
                        + $"{legs.LegCount}.");
                }

                CompanionIdleBehaviour idle =
                    agentObject.AddComponent<CompanionIdleBehaviour>();
                idle.Configure(agent, visualRoot);

                agents.Add(agent);
            }

            var dispatcherObject = new GameObject("Companion Dispatcher");
            dispatcherObject.transform.SetParent(root, false);
            CompanionCommandDispatcher dispatcher =
                dispatcherObject.AddComponent<
                    CompanionCommandDispatcher>();
            dispatcher.Configure(matchRuntime, agents);

            CompanionMatchEndBridge bridge =
                dispatcherObject.AddComponent<CompanionMatchEndBridge>();
            bridge.Configure(matchEndController, dispatcher);

            // One input adapter per role. Only the locally controlled role
            // reacts, and it can only reach the AI through the dispatcher.
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                CompanionCommandKeyboardInput input =
                    binding.Identity.gameObject.AddComponent<
                        CompanionCommandKeyboardInput>();
                input.Configure(
                    dispatcher,
                    binding.Identity,
                    binding.Identity.transform,
                    binding.KeyboardInput != null
                    && binding.KeyboardInput.IsLocallyControlled);

                // ART-003. Reflects the decided result and accepted commands.
                Animator playerAnimator = binding.Identity
                    .GetComponentInChildren<Animator>(true);
                if (playerAnimator != null)
                {
                    PawsAndLoot.Animation.CharacterOutcomeAnimator outcome =
                        binding.Identity.gameObject.AddComponent<
                            PawsAndLoot.Animation.
                                CharacterOutcomeAnimator>();
                    outcome.Configure(
                        playerAnimator,
                        binding.Identity,
                        matchEndController,
                        dispatcher);
                }
            }

            return dispatcher;
        }

        private static PlayerRoleControlBinding FindBinding(
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            PlayerRole role)
        {
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding?.Identity != null && binding.Role == role)
                {
                    return binding;
                }
            }

            throw new InvalidOperationException(
                $"MAP-001 requires a control binding for '{role}'.");
        }

        /// <summary>
        /// NET-005 scene wiring. Gives every loot item its own network link.
        ///
        /// Found by type rather than passed in, so loot added to the map later is
        /// picked up without touching this method.
        /// </summary>
        private static void CreateLootNetworkLinks()
        {
            int wired = 0;
            foreach (LootItem loot in
                UnityEngine.Object.FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None))
            {
                if (loot.GetComponent<NetworkLootLink>() != null)
                {
                    continue;
                }

                if (loot.GetComponent<Unity.Netcode.NetworkObject>()
                    == null)
                {
                    loot.gameObject.AddComponent<
                        Unity.Netcode.NetworkObject>();
                }

                loot.gameObject
                    .AddComponent<NetworkLootLink>()
                    .Configure(loot);
                wired++;
            }

            Debug.Log($"[NET-005] {wired} loot links wired.");
        }

        /// <summary>
        /// NET-003 and NET-004 scene wiring.
        ///
        /// Every synchronised object is an in-scene NetworkObject, which is why
        /// the session turns NGO scene management on: the server loads the match
        /// scene for everyone and both machines resolve the same objects.
        ///
        /// All of it is inert offline. Without a session the links never spawn,
        /// the local motors keep running and the playtest build behaves exactly
        /// as before.
        /// </summary>
        private static void CreateNetworkSync(
            Transform parent,
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime)
        {
            var links = new List<NetworkPlayerLink>();
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                GameObject player = binding.Identity.gameObject;
                if (player.GetComponent<Unity.Netcode.NetworkObject>()
                    == null)
                {
                    player.AddComponent<Unity.Netcode.NetworkObject>();
                }

                NetworkPlayerLink link =
                    player.AddComponent<NetworkPlayerLink>();
                link.Configure(
                    binding.Identity,
                    player.GetComponent<PlayerMovementMotor>(),
                    binding.KeyboardInput,
                    player.GetComponent<CharacterController>());
                // NET-005 to NET-007. Interact, drop, companion commands, the
                // wallet and the arrest bar all travel on the same link, so a
                // client has one route to the host and none around it.
                link.ConfigureGameplay(
                    player.GetComponent<PlayerInteractionScanner>(),
                    player.GetComponent<PlayerInteractionInput>(),
                    player.GetComponent<LootCarrier>(),
                    player.GetComponent<LootDropInput>(),
                    player.GetComponent<
                        PawsAndLoot.Input.CompanionCommandKeyboardInput>(),
                    player.GetComponent<ThiefLootWallet>(),
                    player.GetComponent<ArrestProgressController>(),
                    player.GetComponent<
                        PawsAndLoot.Gameplay.Items.ToolUseAction>(),
                    player.GetComponent<
                        PawsAndLoot.Gameplay.Items.ToolUseInput>(),
                    player.GetComponent<StunState>(),
                    player.GetComponent<
                        PawsAndLoot.Animation.ThrowPresenter>(),
                    player.GetComponent<
                        PawsAndLoot.Gameplay.Items.ToolCarrier>(),
                    player.GetComponent<PoliceWallet>(),
                    player.GetComponent<
                        PawsAndLoot.Animation.CompanionLegAnimator>());
                links.Add(link);
            }

            CreateLootNetworkLinks();

            var syncObject = new GameObject("Network Sync");
            syncObject.transform.SetParent(parent);
            syncObject.AddComponent<Unity.Netcode.NetworkObject>();
            NetworkMatchMirror mirror =
                syncObject.AddComponent<NetworkMatchMirror>();
            mirror.Configure(matchRuntime);

            var bridgeObject = new GameObject("Network Input Bridge");
            bridgeObject.transform.SetParent(parent);
            NetworkInputBridge bridge =
                bridgeObject.AddComponent<NetworkInputBridge>();
            // The manager only exists at runtime in a session, so the bridge
            // resolves the singleton itself.
            bridge.Configure(null, links);

            // Verifies NET-003 and NET-004 once the match scene is live.
            var matchProbe = new GameObject("Network Match Probe");
            matchProbe.transform.SetParent(parent);
            matchProbe.AddComponent<
                PawsAndLoot.TechnicalValidation.NetworkMatchProbe>();

            // THROW-007. Placed props are replicated as named messages rather
            // than spawned NetworkObjects, and the host owns triggering.
            var trapObject = new GameObject("Trap Coordinator");
            trapObject.transform.SetParent(parent);
            trapObject
                .AddComponent<NetworkItemCoordinator>()
                .Configure(matchRuntime);

            // Disconnect handling deliberately lives on the persistent
            // NetworkManager object in Bootstrap, not here: one handler for the
            // whole session, so a peer loss tears down once instead of once per
            // scene.
            Debug.Log(
                $"[NET-003] {links.Count} player links and the match mirror "
                + "wired into the Game scene.");
        }

        /// <summary>
        /// AUDIO-002. Creates the sound sink and the observer that listens to
        /// the rule layer. Rules never reference either one.
        /// </summary>
        private static void CreateAudio(
            Transform parent,
            CompanionCommandDispatcher dispatcher,
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime,
            MatchEndController matchEndController)
        {
            PlayerRoleControlBinding police =
                FindBinding(bindings, PlayerRole.Police);
            PlayerRoleControlBinding thief =
                FindBinding(bindings, PlayerRole.Thief);

            var audioObject = new GameObject("Game Audio");
            audioObject.transform.SetParent(parent);
            AudioSource oneShot =
                audioObject.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;
            oneShot.spatialBlend = 0f;
            AudioSource music = audioObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;

            GameSoundService service =
                audioObject.AddComponent<GameSoundService>();
            service.Configure(LoadOrCreateSoundBank(), oneShot, music);

            GameSoundObserver observer =
                audioObject.AddComponent<GameSoundObserver>();
            observer.Configure(
                dispatcher,
                thief.Identity.GetComponent<LootCarrier>(),
                thief.Identity.GetComponent<ThiefLootWallet>(),
                police.Identity.GetComponent<ArrestProgressController>(),
                police.Identity.GetComponent<ArrestCompletionController>(),
                matchEndController,
                UnityEngine.Object.FindFirstObjectByType<
                    DistractionBoard>());
        }

        private const string SoundBankPath =
            "Assets/_Project/Settings/Audio/GameSoundBank.asset";

        /// <summary>
        /// Creates the bank on first run so every sound id shows up in the
        /// Inspector waiting for a clip.
        /// </summary>
        private static GameSoundBank LoadOrCreateSoundBank()
        {
            GameSoundBank bank =
                AssetDatabase.LoadAssetAtPath<GameSoundBank>(SoundBankPath);
            if (bank == null)
            {
                string directory =
                    Path.GetDirectoryName(SoundBankPath)?
                        .Replace('\\', '/');
                if (!string.IsNullOrEmpty(directory)
                    && !AssetDatabase.IsValidFolder(directory))
                {
                    Directory.CreateDirectory(
                        Path.GetFullPath(directory));
                    AssetDatabase.Refresh();
                }

                bank = ScriptableObject.CreateInstance<GameSoundBank>();
                AssetDatabase.CreateAsset(bank, SoundBankPath);
            }

            bank.EnsureAllSoundIds();
            EditorUtility.SetDirty(bank);
            Debug.Log(
                $"[AUDIO-001] Sound bank has {bank.EntryCount} entries, "
                + $"{bank.CountMissingClips()} without a clip yet.");
            return bank;
        }

        /// <summary>
        /// MAP-003. A climbable ladder. Collected rather than configured here
        /// because the match runtime does not exist yet when stores are built.
        /// </summary>
        private static void CreateLadderTraversal(
            string name,
            Vector3 groundPosition,
            Vector3 rooftopPosition,
            Transform parent,
            ICollection<LadderTraversal> pending)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent);
            target.transform.position =
                groundPosition + new Vector3(0f, 1f, 0f);
            BoxCollider trigger = target.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.6f, 2f, 1.6f);

            Transform bottom = CreateChild("Bottom", target.transform);
            bottom.position = groundPosition + new Vector3(0f, 1f, 0f);
            Transform top = CreateChild("Top", target.transform);
            top.position = rooftopPosition;

            LadderTraversal ladder =
                target.AddComponent<LadderTraversal>();
            pending.Add(ladder);
        }

        /// <summary>
        /// MAP-002. A dressing building plus one Box collider derived from the
        /// model's measured bounds. The visual mesh is never used for collision.
        /// </summary>
        private static void CreateDressingBuilding(
            string stem,
            Transform parent,
            Vector3 groundCenter,
            float footprintX,
            float footprintZ)
        {
            Transform anchor = CreateChild($"{stem} Anchor", parent);
            anchor.position = groundCenter;
            anchor.localRotation = Quaternion.identity;

            float height = PlaceholderModelLibrary.TryInstantiateBuilding(
                stem,
                anchor,
                groundCenter,
                footprintX,
                footprintZ);
            if (height <= 0f)
            {
                UnityEngine.Object.DestroyImmediate(anchor.gameObject);
                return;
            }

            BoxCollider box = anchor.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(footprintX, height, footprintZ);
        }

        /// <summary>
        /// The raccoon merchant and the bin it hides in.
        ///
        /// The merchant is the one NPC the thief has to find, and on a greybox
        /// map a stationary model beside a stall reads as scenery. Having it
        /// rise and wave when a player comes near is what marks it as the place
        /// to sell without adding a HUD marker.
        ///
        /// The bin's own collider is kept and the raccoon is parented inside it
        /// with no collider of its own, so the merchant can never be walked
        /// into or pushed off its spot.
        /// </summary>
        /// <summary>
        /// Drops a trash can model into a bin volume and scales it to
        /// <paramref name="targetHeight"/> metres.
        ///
        /// The height is measured from the instance rather than assumed: the
        /// authored can is not one unit tall, so passing a scale of 1 produced a
        /// 2.18 m bin against a 1.70 m player. Anything that has to match a
        /// character's height has to be measured, not guessed from the file.
        ///
        /// Returns false when the model is missing, leaving the greybox cube
        /// visible.
        /// </summary>
        /// <summary>
        /// Builds a hinge at the bin's back rim and moves the lid meshes under
        /// it, so rotating one transform swings the real lid open.
        ///
        /// The authored can keeps its lid as separate nodes
        /// (<c>FN_TrashCan_Lid</c> and its handle) with their pivots at the
        /// model origin, so rotating them directly would spin the lid about the
        /// bin's centre instead of its edge. The empty pivot placed on the rim
        /// is what turns that into a hinge.
        ///
        /// Returns null when the model has no lid node, in which case the
        /// greeting still works and only the lid stays shut.
        /// </summary>
        private static Transform TryCreateLidHinge(GameObject canModel)
        {
            // The can arrives as a linked prefab instance, and Unity refuses to
            // reparent a prefab instance's children — SetParent simply does
            // nothing, with no exception and no return value to check. That is
            // why the lid stayed welded to the can and never opened. Unpacking
            // first turns it into plain objects that can be restructured.
            if (PrefabUtility.IsPartOfPrefabInstance(canModel))
            {
                PrefabUtility.UnpackPrefabInstance(
                    canModel,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            Transform lid = FindBone(canModel.transform, "FN_TrashCan_Lid");
            if (lid == null)
            {
                return null;
            }

            var lidRenderer = lid.GetComponent<Renderer>();
            if (lidRenderer == null)
            {
                return null;
            }

            Bounds lidBounds = lidRenderer.bounds;
            var pivotObject = new GameObject("Lid Hinge");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(canModel.transform.parent, false);
            // Back rim: the lid tips up and away from a player walking in from
            // the front, rather than swinging through them.
            pivot.position = new Vector3(
                lidBounds.center.x,
                lidBounds.min.y,
                lidBounds.max.z);
            pivot.rotation = Quaternion.identity;

            lid.SetParent(pivot, true);

            Transform handle =
                FindBone(canModel.transform, "FN_TrashCan_LidHandle");
            if (handle != null)
            {
                handle.SetParent(pivot, true);
            }

            if (pivot.childCount == 0)
            {
                // Reparenting is silent when it fails, so the one thing that
                // makes this whole animation work is asserted rather than
                // assumed.
                Debug.LogError(
                    "[MAP-007] The trash can lid could not be moved onto its "
                    + "hinge, so it will not open.");
            }

            return pivot;
        }

        private static GameObject TryFitTrashCanModel(
            Transform bin,
            float targetHeight,
            Material material)
        {
            GameObject model =
                PlaceholderModelLibrary.TryInstantiateProp(
                    "object_trash_can",
                    bin,
                    Vector3.zero,
                    Vector3.zero,
                    1f,
                    material);
            if (model == null)
            {
                return null;
            }

            bool measured = false;
            var bounds = new Bounds();
            foreach (Renderer renderer in
                model.GetComponentsInChildren<Renderer>(true))
            {
                if (!measured)
                {
                    bounds = renderer.bounds;
                    measured = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            if (!measured || bounds.size.y <= 0.001f)
            {
                UnityEngine.Object.DestroyImmediate(model);
                return null;
            }

            float correction = targetHeight / bounds.size.y;
            model.transform.localScale *= correction;

            // Re-measured after scaling so the base lands exactly on the
            // ground; the model's pivot is not necessarily at its feet.
            measured = false;
            foreach (Renderer renderer in
                model.GetComponentsInChildren<Renderer>(true))
            {
                if (!measured)
                {
                    bounds = renderer.bounds;
                    measured = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            float groundY = bin.position.y
                - bin.lossyScale.y * 0.5f;
            model.transform.position += Vector3.up
                * (groundY - bounds.min.y);
            return model;
        }

        private static void CreateRaccoonInBin(
            Transform parent,
            Vector3 groundPosition)
        {
            // Double sided so the open lid reveals the inside of the bin
            // rather than a see-through hole.
            Material binMaterial = LoadOrCreateDoubleSidedMaterial(
                "Greybox_TrashBin",
                new Color(0.32f, 0.34f, 0.36f));

            GameObject bin = CreateCube(
                "Raccoon Merchant Bin",
                groundPosition
                    + Vector3.up * (TrashBinHeight * 0.5f),
                new Vector3(
                    TrashBinHeight * 0.77f,
                    TrashBinHeight,
                    TrashBinHeight * 0.77f),
                binMaterial,
                parent,
                true);

            GameObject canModel = TryFitTrashCanModel(
                bin.transform,
                TrashBinHeight,
                binMaterial);
            Transform lidHinge = null;
            if (canModel != null)
            {
                bin.GetComponent<Renderer>().enabled = false;
                lidHinge = TryCreateLidHinge(canModel);
            }

            // Outside the bin's non-uniform scale, so the raccoon is not
            // squashed by the bin's 0.77 width.
            // Nudged north of the bin's centre. The raccoon's rest pose is not
            // centred on its own origin, so sharing the bin's exact position
            // left it sitting visibly toward the south wall.
            Transform pivot = CreateChild("Raccoon Pivot", parent);
            pivot.position = groundPosition + new Vector3(0f, 0f, 0.18f);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Transform anchor = CreateChild("raccoon Visual", pivot);
            anchor.localPosition = Vector3.zero;
            anchor.localRotation = Quaternion.identity;

            GameObject model =
                PlaceholderModelLibrary.TryInstantiateAuthoredCharacter(
                    "raccoon",
                    anchor,
                    AuthoredRaccoonHeight,
                    0f);
            if (model == null)
            {
                UnityEngine.Object.DestroyImmediate(pivot.gameObject);
                return;
            }

            RaccoonBinGreeter greeter =
                pivot.gameObject.AddComponent<RaccoonBinGreeter>();
            greeter.Configure(
                anchor,
                lidHinge,
                // The left arm, not the right. On this rig the two arms mirror,
                // so the same rotation that swung the right arm down lifts the
                // left one — and a wave has to come from a raised hand.
                FindBone(model.transform, "L_Upperarm"),
                FindBone(model.transform, "L_Forearm"),
                // Measured against the rig, not guessed: this raccoon is
                // hunched, with its shoulder 0.58 m and its head 0.70 m above
                // its own feet on a 1.35 m model, and its full bounds reach the
                // whole 1.35 m because of the ears and tail.
                //
                // Hidden sits it high inside the bin rather than below the
                // floor. The bin tapers, so up there the spread arms fit within
                // the walls instead of poking through them, and the raccoon's
                // highest point still clears the 1.89 m rim by a margin.
                //
                // Raised puts the shoulder about a quarter metre proud of the
                // rim, which is what gets the waving hand clear of it.
                TrashBinHeight * 0.26f,
                TrashBinHeight * 0.84f,
                6f);

            Debug.Log(
                "[MAP-007] Raccoon merchant placed in its bin with a "
                + "proximity greeting.");
        }

        /// <summary>
        /// Exact name first, then a case-insensitive contains, because the
        /// authored rigs are not guaranteed to keep the same capitalisation
        /// when they are re-exported.
        /// </summary>
        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (Transform bone in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == boneName)
                {
                    return bone;
                }
            }

            foreach (Transform bone in
                root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.IndexOf(
                        boneName,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return bone;
                }
            }

            Debug.LogWarning(
                $"[MAP-007] Bone '{boneName}' is missing, so that part of "
                + "the raccoon greeting will not animate.");
            return null;
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
            ICollection<Transform> ladders,
            ICollection<LadderTraversal> pendingLadderClimbs,
            string buildingModelStem = null)
        {
            Transform root = CreateChild(name, buildingsRoot);
            GameObject body = CreateCube(
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

            // An authored building replaces the greybox visual. The cubes stay
            // as the simple colliders MAP-002 requires, with their renderers
            // hidden, and the walkable rooftop collider moves onto the real
            // roof height so the model is not clipped by a slab.
            float modelHeight = -1f;
            if (buildingModelStem != null)
            {
                modelHeight =
                    PlaceholderModelLibrary.TryInstantiateBuilding(
                        buildingModelStem,
                        root,
                        center,
                        12f,
                        8f);
            }

            if (modelHeight > 0f)
            {
                body.GetComponent<Renderer>().enabled = false;
                roof.GetComponent<Renderer>().enabled = false;
                body.transform.position =
                    center + Vector3.up * (modelHeight * 0.5f);
                body.transform.localScale =
                    new Vector3(12f, modelHeight, 8f);
                roof.transform.position =
                    center + Vector3.up * (modelHeight + 0.3f);
            }

            float roofHeight = modelHeight > 0f ? modelHeight : 3.9f;
            Vector3 ladderPosition = ladderSide == LadderSide.West
                ? center + new Vector3(-6.45f, 0f, 0f)
                : center + new Vector3(6.45f, 0f, 0f);
            Transform ladder = CreateLadder(
                $"{name} Ladder",
                ladderPosition,
                ladderMaterial,
                featuresRoot,
                roofHeight);
            ladders.Add(ladder);

            // MAP-003. The rooftop landing sits inboard of the parapet so the
            // player lands on the roof rather than on its edge.
            float inboard = ladderSide == LadderSide.West ? 2.2f : -2.2f;
            CreateLadderTraversal(
                $"{name} Ladder Climb",
                ladderPosition,
                new Vector3(
                    ladderPosition.x + inboard,
                    roofHeight + 1f,
                    ladderPosition.z),
                ladder,
                pendingLadderClimbs);

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
                    positions[index]
                        + Vector3.up * (TrashBinHeight * 0.5f),
                    new Vector3(
                        TrashBinHeight * 0.77f,
                        TrashBinHeight,
                        TrashBinHeight * 0.77f),
                    material,
                    parent,
                    true);

                // The cube stays as the simple collider volume (MAP-005) and
                // only its renderer is hidden once the model is available.
                //
                // The child's scale multiplies the cube's, so a uniform 1 leaves
                // the model exactly as tall as the cube. The -0.5 local offset
                // is half the cube in its own space, which drops the model's
                // base onto the ground whatever the height is.
                if (TryFitTrashCanModel(
                        bin.transform,
                        TrashBinHeight,
                        material) != null)
                {
                    bin.GetComponent<Renderer>().enabled = false;
                }

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
            // CreateChild keeps the new object's world position, which leaves
            // it at the world origin instead of on the player.
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            Material roleMaterial =
                LoadOrCreateMaterial($"Role_{role}", color);

            // Authored textured characters take priority. The borrowed cartoon
            // mesh stays as the fallback for anyone without the external
            // package, and the greybox capsule as the last resort.
            GameObject placeholder =
                PlaceholderModelLibrary.TryInstantiateAuthoredCharacter(
                    role == PlayerRole.Police ? "police" : "thief",
                    visualRoot,
                    AuthoredCharacterHeight);
            if (placeholder == null)
            {
                // A light head against the role colour keeps the two
                // silhouettes readable while both roles share one mesh.
                Material headMaterial = LoadOrCreateMaterial(
                    "Placeholder_Head",
                    new Color(0.96f, 0.84f, 0.71f));
                placeholder =
                    PlaceholderModelLibrary.TryInstantiateCharacter(
                        role,
                        visualRoot,
                        roleMaterial,
                        headMaterial);
            }

            if (placeholder == null)
            {
                placeholder = GameObject.CreatePrimitive(
                    PrimitiveType.Capsule);
                placeholder.name = "PlaceholderModel";
                placeholder.transform.SetParent(visualRoot, false);
                placeholder.GetComponent<Renderer>().sharedMaterial =
                    roleMaterial;
                UnityEngine.Object.DestroyImmediate(
                    placeholder.GetComponent<Collider>());
            }
            else if (PlaceholderModelLibrary.TryGetWorldBounds(
                         placeholder,
                         out Bounds modelBounds))
            {
                PlaceholderModelLibrary.TryGetHeadRatio(
                    placeholder,
                    out float heads,
                    out float _);
                Debug.Log(
                    $"[Placeholder] {role} model height "
                    + $"{modelBounds.size.y:0.00}m, feet at "
                    + $"y={modelBounds.min.y:0.00}m, "
                    + $"proportion {heads:0.0} heads.");
            }
            else
            {
                throw new InvalidOperationException(
                    $"{role} placeholder model '{placeholder.name}' has no "
                    + "renderer, so the character would be invisible.");
            }

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

            // Locomotion animation reads the motor and never drives it, so a
            // missing Animator leaves movement untouched.
            Animator characterAnimator =
                player.GetComponentInChildren<Animator>(true);
            if (characterAnimator != null
                && characterAnimator.runtimeAnimatorController != null)
            {
                PawsAndLoot.Animation.PlayerLocomotionAnimator locomotion =
                    player.AddComponent<
                        PawsAndLoot.Animation.PlayerLocomotionAnimator>();
                locomotion.Configure(
                    motor,
                    characterAnimator,
                    playerConfig.MoveSpeed);
            }

            // Procedural walk for the players, driven only when the Animator is
            // off. The locomotion clips come from TopDownEngine and cannot be
            // committed, so without it the characters would slide along with no
            // leg motion at all; this swings their real bones instead.
            //
            // Contralateral biped gait, not the animals' four-beat lateral walk.
            if (characterAnimator != null)
            {
                PawsAndLoot.Animation.CompanionLegAnimator stride =
                    player.AddComponent<
                        PawsAndLoot.Animation.CompanionLegAnimator>();
                stride.Configure(
                    characterAnimator.transform,
                    PawsAndLoot.Animation.CompanionLegAnimator
                        .GaitMode.Biped,
                    characterAnimator);
                if (stride.LegCount == 0)
                {
                    Debug.LogWarning(
                        $"[{role}] No recognisable limb bones, so the "
                        + "procedural walk will not play.");
                }
                else
                {
                    Debug.Log(
                        $"[ART-014] {role} procedural biped walk on "
                        + $"{stride.LegCount} limbs.");
                }

                // No body settle for the players.
                //
                // One was added to make running read better and it made things
                // worse: on a character whose legs barely deform it became the
                // dominant motion and read as vibration rather than a bounce —
                // "눈이 아프다" was the report, and it was right. The officer had
                // looked fine before it went in.
                //
                // The real difference is in the rigs, and it is measurable. Of
                // the officer's 6,258 vertices, 38.8% are weighted to the leg
                // chain against 18.1% to the arms, so their stride dominates.
                // The thief's 3,633 vertices are 19.4% legs against 20.3% arms —
                // half the officer's leg share and no more than their own arms,
                // which is exactly why only the arms read. Both characters swing
                // the same 24° at the bone.
                //
                // That is a weighting job in Blender, not something a bob can
                // paper over. See ART-015.
            }

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

            // THROW-001/002/003. A prop slot separate from the loot slot, so
            // picking up a rock never costs the thief their jewels.
            player.AddComponent<PawsAndLoot.Gameplay.Players.StunState>();
            PawsAndLoot.Gameplay.Items.ToolCarrier toolCarrier =
                player.AddComponent<PawsAndLoot.Gameplay.Items.ToolCarrier>();
            toolCarrier.Configure(identity, matchRuntime);
            PawsAndLoot.Gameplay.Items.ToolUseAction toolUse =
                player.AddComponent<PawsAndLoot.Gameplay.Items.ToolUseAction>();
            toolUse.Configure(identity, toolCarrier, Physics.AllLayers);
            PawsAndLoot.Gameplay.Items.ToolUseInput toolInput =
                player.AddComponent<PawsAndLoot.Gameplay.Items.ToolUseInput>();
            toolInput.Configure(toolUse, locallyControlled);

            // THROW-008. The swing and the flying rock. Subscribes to the throw
            // itself, so it plays wherever the throw was resolved; the other
            // machine is told by the network link.
            PawsAndLoot.Animation.ThrowPresenter throwPresenter =
                player.AddComponent<PawsAndLoot.Animation.ThrowPresenter>();
            throwPresenter.Configure(
                toolUse,
                player.transform,
                LoadOrCreateMaterial(
                    "Greybox_Rock",
                    new Color(0.42f, 0.42f, 0.45f)));

            // Stars over a stunned head. Both screens: landing a hit is most of
            // the reward for throwing, and the thrower has to be able to see it.
            player.AddComponent<PawsAndLoot.Animation.StunStarsView>()
                .Configure(
                    player.GetComponent<
                        PawsAndLoot.Gameplay.Players.StunState>(),
                    // Flat, saturated yellow at full alpha. Unlit, so it stays
                    // this bright at night — a stun read as "the game froze"
                    // when it was subtle, and the fix for that is a colour
                    // nothing else in the town uses.
                    LoadOrCreateGlowMaterial(
                        "Greybox_StunStar",
                        new Color(1f, 0.85f, 0.05f, 1f),
                        // Drawn from both sides. A flat cutout star has no
                        // meaningful back, and single-sided is what made these
                        // invisible for their whole existence — the mesh was
                        // wound backwards and culling discarded all of it. The
                        // winding is fixed and tested; this makes a future
                        // orientation change unable to silently delete them
                        // again.
                        true));

            // Per-screen night adaptation. The thief's is brighter — they are the
            // one being hunted in the dark, and this is the cheapest
            // counterweight available: it costs the police nothing they can see
            // and adds no rule.
            CreateNightVisionFill(player, identity, matchRuntime, role);

            if (role == PlayerRole.Police)
            {
                CreatePoliceFlashlight(player, matchRuntime, identity);

                // The thief is only drawn when the torch is on them. Local to
                // the officer's screen and purely visual — the host still
                // simulates an invisible thief exactly the same way.
                player.AddComponent<
                        PawsAndLoot.Gameplay.Players.FlashlightVisibility>()
                    .Configure(identity, matchRuntime);

                // DOG-003's trail, finally drawn. Shown only while the dog is
                // tracking, and only on this screen; the data has been recorded
                // and followed since the command shipped but was invisible.
                var trailViewObject = new GameObject("Scent Trail View");
                trailViewObject.transform.SetParent(
                    player.transform,
                    false);
                trailViewObject
                    .AddComponent<PawsAndLoot.Animation.ScentTrailView>()
                    .Configure(
                        null,
                        null,
                        identity,
                        matchRuntime,
                        LoadOrCreateMaterial(
                            "Greybox_ScentMark",
                            new Color(1f, 0.78f, 0.32f)));
            }
            if (role == PlayerRole.Police)
            {
                // THROW-011. The officer's purse. Never read by the win
                // condition — it buys equipment and nothing else.
                player.AddComponent<PoliceWallet>().Configure();
            }

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
                FixedCameraOffset,
                0.12f);
            Camera.main.transform.rotation = followCamera.FixedRotation;
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

        private static void ConfigureMatchResultEvaluator(
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime)
        {
            ThiefLootWallet wallet = null;
            ArrestCompletionController arrestCompletion = null;
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding.Role == PlayerRole.Thief)
                {
                    wallet = binding.Identity.GetComponent<
                        ThiefLootWallet>();
                }
                else if (binding.Role == PlayerRole.Police)
                {
                    arrestCompletion =
                        binding.Identity.GetComponent<
                            ArrestCompletionController>();
                }
            }

            if (wallet == null || arrestCompletion == null)
            {
                throw new InvalidOperationException(
                    "MATCH-004 requires the Thief wallet and arrest completion.");
            }

            MatchResultEvaluator evaluator =
                matchRuntime.gameObject.AddComponent<
                    MatchResultEvaluator>();
            evaluator.Configure(
                matchRuntime,
                wallet,
                arrestCompletion);
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

        private static MatchEndController ConfigureMatchEndController(
            IReadOnlyList<PlayerRoleControlBinding> bindings,
            MatchRuntimeState matchRuntime,
            LocalPlayerRoleSelector roleSelector)
        {
            ArrestProgressController arrestProgress = null;
            foreach (PlayerRoleControlBinding binding in bindings)
            {
                if (binding.Role == PlayerRole.Police)
                {
                    arrestProgress =
                        binding.Identity.GetComponent<
                            ArrestProgressController>();
                    break;
                }
            }

            MatchResultEvaluator resultEvaluator =
                matchRuntime.GetComponent<MatchResultEvaluator>();
            if (resultEvaluator == null || arrestProgress == null)
            {
                throw new InvalidOperationException(
                    "MATCH-005 requires result evaluation and arrest progress.");
            }

            MatchEndController endController =
                matchRuntime.gameObject.AddComponent<
                    MatchEndController>();
            endController.Configure(
                matchRuntime,
                resultEvaluator,
                roleSelector,
                arrestProgress);
            return endController;
        }

        private static void ConfigureMatchResultFlow(
            MatchRuntimeState matchRuntime,
            MatchEndController matchEndController)
        {
            MatchResultFlowController resultFlow =
                matchRuntime.gameObject.AddComponent<
                    MatchResultFlowController>();
            resultFlow.Configure(matchRuntime, matchEndController);
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
            // ISSUE-011. Six pieces at 200 gold against a 1,000 target, so the
            // thief can win with five and still has one to spare. A single
            // piece made the sale victory mathematically impossible, which
            // blocked gate B and two of the NET-010 scenarios.
            //
            // Spread across the map on purpose: the thief has to keep crossing
            // ground the police can cover, rather than farming one corner.
            Vector3[] lootSpots =
            {
                locations[GreyboxLocationId.JewelryStore].position
                    + new Vector3(1.8f, 0.5f, 0f),
                locations[GreyboxLocationId.Bookstore].position
                    + new Vector3(0f, 0.5f, 4.5f),
                locations[GreyboxLocationId.Supermarket].position
                    + new Vector3(0f, 0.5f, -4.5f),
                new Vector3(-21f, 0.5f, -6f),
                new Vector3(7f, 0.5f, 26f),
                new Vector3(36f, 0.5f, 0f)
            };

            for (int index = 0; index < lootSpots.Length; index++)
            {
                CreateLootTarget(
                    index == 0
                        ? "Prototype Loot"
                        : $"Loot {index + 1}",
                    lootSpots[index],
                    new Color(0.75f, 0.3f, 0.95f),
                    root);
            }

            Debug.Log(
                $"[ISSUE-011] {lootSpots.Length} loot pieces placed.");

            CreateRockPickups(root, matchRuntime);
            CreateSaleZone(
                "Prototype Sale Point",
                locations[GreyboxLocationId.RaccoonMarket].position
                    + new Vector3(-1.8f, 0.5f, 0f),
                MarketGold,
                root,
                matchRuntime);
            // The old ladder marker was a PrototypeInteractable that only
            // counted presses, which read as a broken ladder. Real climbing now
            // lives on LadderTraversal beside each store ladder.
            // LOOT-005. Two stashes so the thief has a choice, placed at the
            // trash bin corners the concept map uses as hiding places.
            CreateLootHidingSpot(
                "Hiding Spot West",
                new Vector3(-20f, 0.5f, 8f) + new Vector3(1.6f, 0f, 0f),
                root,
                matchRuntime);
            CreateLootHidingSpot(
                "Hiding Spot East",
                new Vector3(20f, 0.5f, -8f) + new Vector3(-1.6f, 0f, 0f),
                root,
                matchRuntime);
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

        /// <summary>
        /// LOOT-005. A stash the thief can hide carried loot in. The cardboard
        /// box model marks it, and the trigger is a simple box so MAP-002 stays
        /// satisfied.
        /// </summary>
        private static void CreateLootHidingSpot(
            string name,
            Vector3 position,
            Transform parent,
            MatchRuntimeState matchRuntime)
        {
            var target = new GameObject(name);
            target.transform.SetParent(parent);
            target.transform.position = position;
            BoxCollider trigger = target.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 2f, 2.4f);

            if (PlaceholderModelLibrary.TryInstantiateProp(
                    "object_cardboard_box",
                    target.transform,
                    new Vector3(0f, -0.5f, 0f),
                    Vector3.zero,
                    1f,
                    LoadOrCreateMaterial(
                        "Interaction_Hide",
                        new Color(0.72f, 0.55f, 0.32f))) == null)
            {
                GameObject fallback = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                fallback.name = "HidingSpotMarker";
                fallback.transform.SetParent(target.transform, false);
                fallback.transform.localPosition =
                    new Vector3(0f, -0.2f, 0f);
                fallback.transform.localScale = Vector3.one * 0.9f;
                fallback.GetComponent<Renderer>().sharedMaterial =
                    LoadOrCreateMaterial(
                        "Interaction_Hide",
                        new Color(0.72f, 0.55f, 0.32f));
                UnityEngine.Object.DestroyImmediate(
                    fallback.GetComponent<Collider>());
            }

            Transform storedRoot = CreateChild(
                "StoredLoot",
                target.transform);
            storedRoot.localPosition = Vector3.zero;
            storedRoot.localRotation = Quaternion.identity;

            LootHidingSpot spot = target.AddComponent<LootHidingSpot>();
            spot.Configure(trigger, storedRoot, matchRuntime);
        }

        /// <summary>
        /// THROW-005, temporary placement. Rocks on the road so the throw can be
        /// used at all; the permanent layout comes with the map pass.
        ///
        /// Either side may take these, and they come back after a while, so
        /// running out of ammo is a short setback rather than the end of the
        /// tool. Spots sit in road gaps that the loot placement already proved
        /// clear of the validated routes.
        /// </summary>
        private static void CreateRockPickups(
            Transform parent,
            MatchRuntimeState matchRuntime)
        {
            // On road intersections, every one of them.
            //
            // The first set was picked by eye off the map and all five landed
            // inside a building: two in shop bodies, two inside houses, one on
            // top of a loot item. Four were unreachable and the fifth was the
            // only one that could be picked up, which is exactly what the
            // playtest reported. Nothing caught it — a pickup sealed in a wall
            // still builds, still validates and still logs "5 rock pickups
            // placed".
            //
            // So these come off the road grid itself: the horizontals at
            // z = 12 / -12 / -18 / 26 and the verticals at x = -24 / -18 / 0 /
            // 18 / 24. An intersection is open ground by construction, and
            // CheckSpotIsClear below re-measures rather than trusting that.
            Vector3[] spots =
            {
                new(-18f, 0.35f, 12f),
                new(18f, 0.35f, -12f),
                new(0f, 0.35f, 26f),
                new(24f, 0.35f, 12f),
                new(-24f, 0.35f, -18f)
            };

            Material rockMaterial = LoadOrCreateMaterial(
                "Greybox_Rock",
                new Color(0.45f, 0.46f, 0.5f));

            for (int index = 0; index < spots.Length; index++)
            {
                var pickup = new GameObject($"Rock Pickup {index + 1}");
                pickup.transform.SetParent(parent);
                pickup.transform.position = spots[index];

                // Trigger, not a solid: a rock in the road must not stop
                // anybody running over it.
                var trigger = pickup.AddComponent<SphereCollider>();
                trigger.radius = 0.5f;
                trigger.isTrigger = true;

                Transform presentation = CreateChild(
                    "PresentationRoot",
                    pickup.transform);
                presentation.localPosition = Vector3.zero;
                if (PlaceholderModelLibrary.TryInstantiateProp(
                        ThrowableCatalog.GetModelStem(
                            ThrowableKind.Rock),
                        presentation,
                        Vector3.zero,
                        Vector3.zero,
                        0.5f,
                        rockMaterial) == null)
                {
                    GameObject fallback = CreateCube(
                        "Rock Fallback",
                        spots[index],
                        Vector3.one * 0.35f,
                        rockMaterial,
                        presentation,
                        false);
                    UnityEngine.Object.DestroyImmediate(
                        fallback.GetComponent<Collider>());
                }

                pickup.AddComponent<ThrowablePickup>().Configure(
                    ThrowableKind.Rock,
                    presentation,
                    false,
                    PlayerRole.Thief,
                    12f,
                    // Stable id. The pickups are scene objects, so the same
                    // number identifies the same rock on both machines without
                    // anything being spawned.
                    index + 1);

                CheckSpotIsClear(pickup.transform, spots[index]);
            }

            Debug.Log($"[THROW-005] {spots.Length} rock pickups placed.");

            CreatePoliceSupplyCounters(parent, matchRuntime);

            // Rocks in the air. One tracker for the scene, on the machine that
            // simulates: the throw is no longer settled at the moment it leaves
            // the hand, so somebody has to advance it.
            var flightObject = new GameObject("Throw Flights");
            flightObject.transform.SetParent(parent);
            flightObject.AddComponent<ThrowFlightTracker>();
        }

        /// <summary>
        /// The supermarket counter where the officer buys their props.
        ///
        /// This replaces the temporary free pickups those props had. Leaving them
        /// on the map would have left the police purse with nothing to buy, and a
        /// currency with nothing to spend it on is not an economy — it is a
        /// number in the corner of the screen.
        ///
        /// Two counters rather than one that cycles: the officer reads two prices
        /// and presses once, instead of pressing to browse while being chased.
        ///
        /// Placed on the pavement outside the supermarket rather than inside it,
        /// because the interior is not walkable in the greybox and the shop being
        /// a detour is the whole cost of restocking.
        /// </summary>
        private static void CreatePoliceSupplyCounters(
            Transform parent,
            MatchRuntimeState matchRuntime)
        {
            Vector3 shopFront = new Vector3(-14.5f, 0.5f, -9f);
            (ThrowableKind kind, int price, Vector3 offset)[] counters =
            {
                (ThrowableKind.GlueTrap, 60, new Vector3(0f, 0f, 0f)),
                (ThrowableKind.SensorLight, 90, new Vector3(2.2f, 0f, 0f))
            };

            foreach ((ThrowableKind kind, int price, Vector3 offset)
                in counters)
            {
                Vector3 spot = shopFront + offset;
                var counter = new GameObject($"{kind} Counter");
                counter.transform.SetParent(parent);
                counter.transform.position = spot;

                var trigger = counter.AddComponent<SphereCollider>();
                trigger.radius = 0.6f;
                trigger.isTrigger = true;

                Material counterMaterial = LoadOrCreateMaterial(
                    $"Greybox_{kind}",
                    kind == ThrowableKind.GlueTrap
                        ? new Color(0.24f, 0.2f, 0.16f)
                        : new Color(0.86f, 0.88f, 0.9f));
                GameObject marker = CreateCube(
                    $"{kind} Counter Marker",
                    spot + Vector3.up * 0.2f,
                    new Vector3(0.7f, 0.9f, 0.7f),
                    counterMaterial,
                    counter.transform,
                    false);
                UnityEngine.Object.DestroyImmediate(
                    marker.GetComponent<Collider>());

                counter.AddComponent<PoliceSupplyCounter>()
                    .Configure(kind, price, matchRuntime);

                CheckSpotIsClear(counter.transform, spot);
            }

            Debug.Log(
                $"[THROW-011] {counters.Length} police supply counters "
                + "placed.");
        }

        /// <summary>
        /// Complains loudly if something is placed inside solid geometry.
        ///
        /// Worth the code because the silent version of this bug cost a whole
        /// playtest: four of five rocks were sealed in buildings and every check
        /// the project has passed anyway. A pickup nobody can reach is
        /// indistinguishable from a pickup that does not work.
        ///
        /// The probe sphere sits above the road surface and below waist height, so
        /// the ground and the road tiles are not obstacles but a wall, a shop body
        /// or another interactable is.
        /// </summary>
        private static void CheckSpotIsClear(Transform placed, Vector3 spot)
        {
            Physics.SyncTransforms();
            Collider[] blockers = Physics.OverlapSphere(
                spot + Vector3.up * 0.05f,
                0.25f,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            foreach (Collider blocker in blockers)
            {
                // Its own trigger and its own model are not obstacles.
                if (blocker == null
                    || blocker.transform.IsChildOf(placed))
                {
                    continue;
                }

                Debug.LogError(
                    $"[THROW-005] '{placed.name}' at {spot} is inside "
                    + $"'{blocker.name}'. Nobody can reach it — a pickup "
                    + "sealed in geometry still builds and still validates.");
                return;
            }
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
            // Keep the loot visual on the loot collider instead of the world
            // origin that CreateChild would otherwise preserve.
            presentationRoot.localPosition = Vector3.zero;
            presentationRoot.localRotation = Quaternion.identity;
            // The jewel box model is 0.23m wide, so it is scaled up to stay
            // readable at the top-down camera distance.
            if (PlaceholderModelLibrary.TryInstantiateProp(
                    "item_jewel_box",
                    presentationRoot,
                    new Vector3(0f, -0.35f, 0f),
                    Vector3.zero,
                    2.4f,
                    LoadOrCreateMaterial("Interaction_Loot", color)) == null)
            {
                GameObject placeholder = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                placeholder.name = "PlaceholderModel";
                placeholder.transform.SetParent(presentationRoot, false);
                placeholder.transform.localScale = Vector3.one * 0.75f;
                placeholder.GetComponent<Renderer>().sharedMaterial =
                    LoadOrCreateMaterial("Interaction_Loot", color);
                UnityEngine.Object.DestroyImmediate(
                    placeholder.GetComponent<Collider>());
            }

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

            // Keep the flat gold marker so the sale trigger footprint stays
            // readable, then stand the market stall model behind it.
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

            PlaceholderModelLibrary.TryInstantiateProp(
                "object_secret_market_stall",
                target.transform,
                new Vector3(0f, -0.5f, 1.4f),
                new Vector3(0f, 180f, 0f),
                1f,
                LoadOrCreateMaterial("RaccoonMarket", MarketGold));

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
            Transform parent,
            float targetHeight = 4f)
        {
            Transform root = CreateChild(name, parent);
            root.position = position;

            // The authored ladder is 2.8m tall and 0.88m wide along X, so it
            // is stretched to whatever rooftop height it has to reach and
            // turned to keep its width across the original rail spacing.
            const float ModelLadderHeight = 2.8f;
            if (PlaceholderModelLibrary.TryInstantiateProp(
                    "object_ladder",
                    root,
                    Vector3.zero,
                    new Vector3(0f, 90f, 0f),
                    targetHeight / ModelLadderHeight,
                    material) != null)
            {
                return root;
            }

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

        /// <summary>
        /// UI-004 button, UI-005 cooldown readout and UI-006 result line for
        /// the local role's representative command.
        /// </summary>
        private static void CreateCompanionCommandHud(
            Transform canvasRoot,
            CompanionCommandDispatcher dispatcher,
            LocalPlayerRoleSelector roleSelector)
        {
            RectTransform panel = CreateRect(
                "Companion Command HUD",
                canvasRoot);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 0f);
            panel.pivot = new Vector2(0f, 0f);
            panel.anchoredPosition = new Vector2(40f, 150f);
            panel.sizeDelta = new Vector2(420f, 150f);

            Text commandLabel = CreateHudLabel(
                "Command Name",
                panel,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 108f),
                new Vector2(400f, 34f),
                TextAnchor.LowerLeft,
                26);
            Text cooldownLabel = CreateHudLabel(
                "Command Cooldown",
                panel,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 78f),
                new Vector2(400f, 28f),
                TextAnchor.LowerLeft,
                20);
            Text feedbackLabel = CreateHudLabel(
                "Command Feedback",
                panel,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(400f, 34f),
                TextAnchor.LowerLeft,
                20);

            RectTransform buttonRect = CreateRect("Command Button", panel);
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 40f);
            buttonRect.sizeDelta = new Vector2(210f, 34f);
            Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = new Color(0.08f, 0.28f, 0.62f, 0.9f);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            Text buttonLabel = CreateHudLabel(
                "Command Button Label",
                buttonRect,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                TextAnchor.MiddleCenter,
                18);
            buttonLabel.text = "COMMAND  [1/2]";

            CompanionCommandHudPresenter presenter =
                panel.gameObject.AddComponent<
                    CompanionCommandHudPresenter>();
            presenter.Configure(
                dispatcher,
                roleSelector,
                commandLabel,
                cooldownLabel,
                feedbackLabel,
                button);
        }

        /// <summary>
        /// CAT-004 police side. A screen marker and a banner that only appear
        /// for the police while a distraction is running.
        /// </summary>
        /// <summary>
        /// CAT-003's report, drawn. The resolver already knew what the cat found
        /// and where; only the words reached the player, so the direction was
        /// thrown away every time the command was used.
        /// </summary>
        private static void CreateScoutMarkers(
            Transform canvas,
            LocalPlayerRoleSelector roleSelector)
        {
            Text report = CreateHudLabel(
                "Scout Report",
                canvas,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-330f, -104f),
                new Vector2(660f, 30f),
                TextAnchor.MiddleCenter,
                20);
            report.color = new Color(0.85f, 1f, 0.86f);

            RectTransform lootMarker = CreateScoutMarker(
                canvas,
                "Scout Loot Marker",
                "보물",
                new Color(1f, 0.86f, 0.35f));
            RectTransform policeMarker = CreateScoutMarker(
                canvas,
                "Scout Police Marker",
                "경찰",
                new Color(1f, 0.45f, 0.42f));

            canvas.gameObject.AddComponent<ScoutMarkerPresenter>()
                .Configure(
                    null,
                    roleSelector,
                    Camera.main,
                    lootMarker,
                    policeMarker,
                    report);
        }

        private static RectTransform CreateScoutMarker(
            Transform canvas,
            string name,
            string caption,
            Color color)
        {
            RectTransform rect = CreateRect(name, canvas);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 30f);
            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.04f, 0.08f, 0.8f);
            background.raycastTarget = false;

            RectTransform labelRect = CreateRect($"{name} Label", rect);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = labelRect.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = caption;
            label.raycastTarget = false;

            rect.gameObject.SetActive(false);
            return rect;
        }

        private static void CreateDistractionAlert(
            Transform canvasRoot,
            LocalPlayerRoleSelector roleSelector)
        {
            DistractionBoard board =
                UnityEngine.Object.FindFirstObjectByType<
                    DistractionBoard>();
            if (board == null)
            {
                return;
            }

            Text banner = CreateHudLabel(
                "Distraction Banner",
                canvasRoot,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(-240f, -190f),
                new Vector2(480f, 40f),
                TextAnchor.MiddleCenter,
                26);
            banner.color = new Color(0.98f, 0.78f, 0.28f);

            RectTransform marker = CreateRect(
                "Distraction Marker",
                canvasRoot);
            marker.anchorMin = new Vector2(0.5f, 0.5f);
            marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(56f, 56f);
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = new Color(0.98f, 0.78f, 0.28f, 0.85f);
            markerImage.raycastTarget = false;
            Text markerLabel = CreateHudLabel(
                "Marker Label",
                marker,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero,
                TextAnchor.MiddleCenter,
                30);
            markerLabel.color = new Color(0.1f, 0.08f, 0.02f);
            marker.gameObject.SetActive(false);

            DistractionAlertPresenter presenter =
                marker.gameObject.AddComponent<
                    DistractionAlertPresenter>();
            presenter.Configure(
                board,
                roleSelector,
                Camera.main,
                marker,
                banner,
                markerLabel);
        }

        private static void CreateSceneInterface(
            LocalPlayerRoleSelector roleSelector,
            MatchRuntimeState matchRuntime,
            CompanionCommandDispatcher companionDispatcher)
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

            RectTransform policeHudRect = CreateRect(
                "Police HUD",
                hudRoot);
            policeHudRect.anchorMin = Vector2.one;
            policeHudRect.anchorMax = Vector2.one;
            policeHudRect.pivot = Vector2.one;
            policeHudRect.anchoredPosition =
                new Vector2(-28f, -104f);
            policeHudRect.sizeDelta = new Vector2(360f, 304f);
            Image policeHudBackground =
                policeHudRect.gameObject.AddComponent<Image>();
            policeHudBackground.color =
                new Color(0.03f, 0.1f, 0.24f, 0.92f);

            Text policeHudTitle = CreateHudLabel(
                "Title",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -14f),
                new Vector2(324f, 38f),
                TextAnchor.MiddleLeft,
                24);
            policeHudTitle.text = "POLICE STATUS";
            Text policeTimeLabel = CreateHudLabel(
                "Remaining Time",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -56f),
                new Vector2(324f, 34f),
                TextAnchor.MiddleLeft,
                20);
            Text policeThiefGoldLabel = CreateHudLabel(
                "Thief Sale Amount",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -94f),
                new Vector2(324f, 34f),
                TextAnchor.MiddleLeft,
                20);
            Text policeArrestLabel = CreateHudLabel(
                "Arrest Progress",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -132f),
                new Vector2(324f, 34f),
                TextAnchor.MiddleLeft,
                20);
            Text policeTheftAlertLabel = CreateHudLabel(
                "Theft Alert",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -170f),
                new Vector2(324f, 38f),
                TextAnchor.MiddleLeft,
                19);
            policeTheftAlertLabel.color =
                new Color(1f, 0.74f, 0.22f);
            Text policeGoalLabel = CreateHudLabel(
                "Current Goal",
                policeHudRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -216f),
                new Vector2(324f, 70f),
                TextAnchor.UpperLeft,
                18);

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

            // Held prop and the key that uses it, above the interaction
            // prompt. A player who does not know F exists is carrying a rock
            // they will never throw.
            RectTransform toolRect =
                CreateRect("Tool Slot", canvasObject.transform);
            toolRect.anchorMin = new Vector2(0.5f, 0f);
            toolRect.anchorMax = new Vector2(0.5f, 0f);
            toolRect.pivot = new Vector2(0.5f, 0f);
            toolRect.anchoredPosition = new Vector2(0f, 106f);
            toolRect.sizeDelta = new Vector2(420f, 40f);
            toolRect.gameObject.AddComponent<Image>().color =
                new Color(0.02f, 0.04f, 0.08f, 0.7f);

            RectTransform toolLabelRect =
                CreateRect("Tool Label", toolRect);
            toolLabelRect.anchorMin = Vector2.zero;
            toolLabelRect.anchorMax = Vector2.one;
            toolLabelRect.offsetMin = Vector2.zero;
            toolLabelRect.offsetMax = Vector2.zero;
            Text toolLabel =
                toolLabelRect.gameObject.AddComponent<Text>();
            toolLabel.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            toolLabel.fontSize = 20;
            toolLabel.alignment = TextAnchor.MiddleCenter;
            toolLabel.raycastTarget = false;

            // The presenter finds the local player itself, because the role is
            // assigned by the host at runtime and is unknown here.
            hudRoot.gameObject.AddComponent<ToolHudPresenter>()
                .Configure(toolLabel);

            // THROW-009. A line for the officer while a sensor's reveal runs.
            //
            // The reveal itself is the payoff and this does not replace it. But a
            // thief exposed behind a building is a reveal the officer never
            // notices, and a lamp flashing off screen is not something they can
            // act on — this is what turns it into "look now".
            RectTransform sensorRect =
                CreateRect("Sensor Alert", canvasObject.transform);
            sensorRect.anchorMin = new Vector2(0.5f, 1f);
            sensorRect.anchorMax = new Vector2(0.5f, 1f);
            sensorRect.pivot = new Vector2(0.5f, 1f);
            sensorRect.anchoredPosition = new Vector2(0f, -96f);
            sensorRect.sizeDelta = new Vector2(520f, 34f);
            Text sensorLabel =
                sensorRect.gameObject.AddComponent<Text>();
            sensorLabel.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            sensorLabel.fontSize = 22;
            sensorLabel.alignment = TextAnchor.MiddleCenter;
            sensorLabel.raycastTarget = false;
            hudRoot.gameObject.AddComponent<SensorAlertPresenter>()
                .Configure(sensorLabel, roleSelector);

            // The same alert as a shape, pointing the way. The caption says what
            // happened; this says where, which is the half the officer needs
            // while looking at their own character rather than at a caption.
            RectTransform radarRoot =
                CreateRect("Sensor Radar", canvasObject.transform);
            radarRoot.anchorMin = new Vector2(0.5f, 0.5f);
            radarRoot.anchorMax = new Vector2(0.5f, 0.5f);
            radarRoot.pivot = new Vector2(0.5f, 0.5f);
            radarRoot.anchoredPosition = Vector2.zero;
            // Smaller than the first attempt, which was reported as too big.
            radarRoot.sizeDelta = new Vector2(240f, 240f);
            SensorRadarPresenter radar =
                hudRoot.gameObject.AddComponent<SensorRadarPresenter>();
            radar.Configure(radarRoot, roleSelector);

            // Seven concentric arcs, all built. How many of them light is what
            // says how close the sensor is, so the presenter switches them on
            // rather than the builder deciding a count.
            //
            // Red: the torch, the stun stars and the ground wedge are all yellow,
            // and a fourth yellow thing on a night screen is one more yellow
            // thing. An alarm should not share a colour with the lighting.
            for (int band = 0; band < 7; band++)
            {
                RectTransform bar = CreateRect(
                    $"Signal Arc {band + 1}",
                    radarRoot);
                bar.anchorMin = new Vector2(0.5f, 0.5f);
                bar.anchorMax = new Vector2(0.5f, 0.5f);
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = Vector2.zero;
                bar.sizeDelta = new Vector2(320f, 320f);
                SensorArcGraphic arc =
                    bar.gameObject.AddComponent<SensorArcGraphic>();
                arc.Configure(26f + band * 17f, 8f, 96f);
                arc.color = new Color(0.95f, 0.16f, 0.16f, 1f);
                arc.raycastTarget = false;
                radar.AddBar(arc);
            }

            radarRoot.gameObject.SetActive(false);

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
            PoliceHudPresenter policeHudPresenter =
                hudRoot.gameObject.AddComponent<PoliceHudPresenter>();
            policeHudPresenter.Configure(
                roleSelector,
                matchRuntime,
                thiefPlayer.GetComponent<ThiefLootWallet>(),
                thiefPlayer.GetComponent<LootCarrier>(),
                policePlayer.GetComponent<
                    ArrestProgressController>(),
                policeHudRect.gameObject,
                policeTimeLabel,
                policeThiefGoldLabel,
                policeArrestLabel,
                policeTheftAlertLabel,
                policeGoalLabel);

            CreateCompanionCommandHud(
                canvasObject.transform,
                companionDispatcher,
                roleSelector);
            CreateDistractionAlert(
                canvasObject.transform,
                roleSelector);
            CreateScoutMarkers(
                canvasObject.transform,
                roleSelector);

            // UX-001. One live instruction, retiring on its own.
            Text guideLabel = CreateHudLabel(
                "First Play Guide",
                canvasObject.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(-330f, 96f),
                new Vector2(660f, 34f),
                TextAnchor.MiddleCenter,
                22);
            guideLabel.color = new Color(0.85f, 0.93f, 1f);
            FirstPlayGuidePresenter guide =
                guideLabel.gameObject.AddComponent<
                    FirstPlayGuidePresenter>();
            guide.Configure(
                roleSelector,
                matchRuntime,
                thiefPlayer.GetComponent<LootCarrier>(),
                thiefPlayer.GetComponent<ThiefLootWallet>(),
                companionDispatcher,
                guideLabel);

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

        private static CompanionConfig LoadCompanionConfig()
        {
            const string path =
                "Assets/_Project/Settings/Configs/CompanionConfig.asset";
            CompanionConfig config =
                AssetDatabase.LoadAssetAtPath<CompanionConfig>(path);
            if (config == null)
            {
                throw new GameConfigurationException(
                    $"COMP-001 requires CompanionConfig at '{path}'.");
            }

            config.ValidateOrThrow();
            return config;
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

        /// <summary>
        /// Same as <see cref="LoadOrCreateMaterial"/> but rendered from both
        /// sides.
        ///
        /// A bin is a single-skinned mesh, so with the default back-face
        /// culling an open lid shows straight through the far wall to whatever
        /// is behind it. Drawing both faces is what makes the inside of the can
        /// read as the inside of a can.
        /// </summary>
        private static Material LoadOrCreateDoubleSidedMaterial(
            string assetName,
            Color color)
        {
            Material material = LoadOrCreateMaterial(assetName, color);
            // 0 = Off. URP reads both the property and the render state, so
            // the keyword has to be set alongside it.
            material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;
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
