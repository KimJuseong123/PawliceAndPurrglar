using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Camera;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using PawsAndLoot.Sandbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds a throwaway town to walk around in, in its own scene.
    ///
    /// Separate from <c>Game.unity</c> on purpose and completely: its own scene file,
    /// its own folder, its own build output, and it is never added to the build scene
    /// list — that list is generated from <c>GameSceneCatalog</c> and validated to
    /// hold exactly three entries, so adding a fourth would break the shipping
    /// contract to serve a sandbox.
    ///
    /// It is a shape test, not a game. One of each special building that exists as an
    /// FBX, houses filling the rest of the blocks, roads and a plaza from the
    /// environment art, and a single police character with the real motor and camera.
    /// No thief, no animals, no HUD, no session: those are the things that make the
    /// real scene expensive to iterate on, and none of them tell you whether a street
    /// is wide enough to run down.
    ///
    /// Every footprint here is measured from the model rather than assumed. The main
    /// map has been bitten twice by eyeballed coordinates — five rocks sealed inside
    /// buildings, two houses blocking an alley — and a road grid is exactly the kind of
    /// thing that looks fine in a log and wrong on screen.
    /// </summary>
    internal static class MapSandboxSetup
    {
        public const string ScenePath =
            "Assets/_Project/Sandbox/MapSandbox.unity";

        private const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";

        /// <summary>
        /// One city block, and the road that runs round it. Taken from the reference
        /// drawing's proportions: blocks a little wider than deep, with roads wide
        /// enough for two characters to pass and for the fixed camera to read.
        /// </summary>
        private const float RoadWidth = 10f;

        /// <summary>
        /// Blocks of different sizes, on purpose.
        ///
        /// Equal blocks in a 3x3 grid gave a town that reads as a spreadsheet, and
        /// the reference drawing is not that: its blocks are different shapes and its
        /// roads do not all line up. Uneven columns and rows cost nothing here —
        /// everything is placed from these numbers — and they make the streets tell
        /// you where you are.
        /// </summary>
        private static readonly float[] ColumnWidths = { 40f, 30f, 46f };
        private static readonly float[] RowDepths = { 24f, 34f, 28f };

        private static int Columns => ColumnWidths.Length;
        private static int Rows => RowDepths.Length;

        /// <summary>
        /// The special buildings, in the order the reference puts them along the
        /// middle band. The jewellery shop is missing: it exists only as a
        /// <c>.blend</c>, and Unity is not asked to import those. It gets a greybox
        /// stand-in so the layout still has four destinations.
        /// </summary>
        private static readonly string[] MiddleBand =
        {
            "building_supermarket",
            "building_police_station",
            "building_bookstore"
        };

        [MenuItem("Paws & Loot/Sandbox/Rebuild Map Sandbox")]
        public static void RebuildSandbox()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            Transform root = new GameObject("Sandbox").transform;
            Transform environment = Child("Environment", root);
            Transform buildings = Child("Buildings", root);

            MatchRuntimeState match = BuildSystems(root);
            Measurements sizes = Measure();

            float townWidth = ColumnWidths.Sum()
                + (Columns + 1) * RoadWidth;
            float townDepth = RowDepths.Sum() + (Rows + 1) * RoadWidth;
            BuildGround(environment, townWidth, townDepth, sizes);
            int roadPieces = BuildRoads(
                environment,
                townWidth,
                townDepth,
                sizes);

            int placed = BuildBlocks(buildings, sizes, out Vector3 plazaCentre);
            int dressing = BuildDressing(
                environment,
                townWidth,
                townDepth,
                plazaCentre,
                sizes);

            BuildPlayer(root, match, plazaCentre);
            BuildLight(root);

            Directory("Assets/_Project/Sandbox");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                $"[SANDBOX] road tile measures {sizes.Of("env_road_tile")}, "
                + $"plaza {sizes.Of("env_fountain_plaza")}.");
            Debug.Log(
                $"[SANDBOX] Map sandbox rebuilt: {townWidth:0}x{townDepth:0}m, "
                + $"{placed} buildings, {roadPieces} road pieces, "
                + $"{dressing} pieces of dressing. Scene at '{ScenePath}'.");
        }

        /// <summary>
        /// A player you can walk around in, built from the sandbox scene alone.
        ///
        /// Its own output folder and its own scene list. The shipping build is
        /// generated from <c>GameSceneCatalog</c> and validated to contain exactly its
        /// three scenes, so the sandbox is passed to the pipeline directly instead of
        /// being registered — a fourth entry in that list would fail the scene
        /// contract test for the sake of a test map.
        /// </summary>
        [MenuItem("Paws & Loot/Sandbox/Build Windows Map Sandbox")]
        public static void BuildSandboxPlayer()
        {
            const string output =
                "Builds/Sandbox/Windows/MapSandbox.exe";
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(
                    System.IO.Path.GetFullPath(output)));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            UnityEditor.Build.Reporting.BuildReport report =
                BuildPipeline.BuildPlayer(options);
            if (report.summary.result
                != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[SANDBOX] Build failed: {report.summary.result}, "
                    + $"{report.summary.totalErrors} errors.");
                return;
            }

            Debug.Log(
                "[SANDBOX] Map sandbox player built at "
                + System.IO.Path.GetFullPath(output));
        }

        /// <summary>
        /// The footprint of every model this uses, measured once.
        ///
        /// Road tiles are the reason. Their size decides the spacing of everything
        /// else, and a guess would either leave gaps between tiles or overlap them —
        /// neither of which shows up anywhere except on screen.
        /// </summary>
        private struct Measurements
        {
            public Dictionary<string, Vector3> Sizes;

            public Vector3 Of(string stem)
            {
                return Sizes.TryGetValue(stem, out Vector3 size)
                    ? size
                    : Vector3.one;
            }
        }

        private static Measurements Measure()
        {
            var sizes = new Dictionary<string, Vector3>();
            foreach (string path in
                AssetDatabase.FindAssets("t:Model", new[]
                {
                    BuildingDirectory,
                    EnvironmentDirectory
                }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    continue;
                }

                GameObject probe = Object.Instantiate(asset);
                probe.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                probe.transform.localScale = Vector3.one;

                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                foreach (Renderer renderer in
                    probe.GetComponentsInChildren<Renderer>(true))
                {
                    if (first)
                    {
                        bounds = renderer.bounds;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                sizes[System.IO.Path.GetFileNameWithoutExtension(path)] =
                    first ? Vector3.one : bounds.size;
                Object.DestroyImmediate(probe);
            }

            return new Measurements { Sizes = sizes };
        }

        private static Transform Child(string name, Transform parent)
        {
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static void Directory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Sandbox");
            }
        }

        private static MatchRuntimeState BuildSystems(Transform root)
        {
            Transform systems = Child("Systems", root);
            MatchRuntimeState match =
                systems.gameObject.AddComponent<MatchRuntimeState>();

            // Without a MatchConfig it refuses every transition, so the sandbox
            // stayed in Lobby and the motor would not move: "MatchRuntimeState
            // requires a MatchConfig" in the player's console. The countdown is off
            // because there is nothing to count down to here.
            var matchConfig = AssetDatabase.LoadAssetAtPath<MatchConfig>(
                "Assets/_Project/Settings/Configs/MatchConfig.asset");
            if (matchConfig == null)
            {
                Debug.LogError(
                    "[SANDBOX] MatchConfig is missing, so nothing will move. "
                    + "Run Create Default Config Assets first.");
            }

            // Started automatically, because the state machine will not go
            // straight from Lobby to Playing and should not be made to.
            match.Configure(matchConfig, true);
            systems.gameObject.AddComponent<SandboxFreeRoam>()
                .Configure(match);
            return match;
        }

        /// <summary>
        /// A grass slab under everything, so a character who walks off the road still
        /// has something under their feet. Greybox rather than tiled grass models: one
        /// box does the job and 400 tiles would not do it better.
        /// </summary>
        private static void BuildGround(
            Transform parent,
            float width,
            float depth,
            Measurements sizes)
        {
            GameObject ground = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale =
                new Vector3(width + 20f, 1f, depth + 20f);
            // The map's own ground material, not a new one. Creating a material in
            // batch mode found the wrong shader and came out brown; there is a known
            // good one sitting in the project already.
            ground.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/Greybox/Ground.mat");
        }

        /// <summary>
        /// Roads laid along the gaps between blocks, tiled by the measured size of the
        /// tile model so the pieces meet.
        /// </summary>
        private static int BuildRoads(
            Transform parent,
            float townWidth,
            float townDepth,
            Measurements sizes)
        {
            Transform roads = Child("Roads", parent);
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Materials/Greybox/Road.mat");

            // Flat strips, not tiled models.
            //
            // The road tile measures 0.93 x 1.00 m and has a raised bevel round its
            // edge, so scaled up eleven times to fill a 10 m lane it reads as a row
            // of separate slabs with gaps between them — which is exactly how it
            // looked. A strip is one box per lane, it meets its neighbours, and it is
            // the surface being tested rather than the decoration on it. The plaza,
            // the trees and the lamps are still the environment art.
            int count = 0;
            foreach (float z in LaneCentres(Rows, RowDepths))
            {
                Strip(
                    roads,
                    new Vector3(0f, 0.01f, z),
                    new Vector3(townWidth, 0.02f, RoadWidth),
                    material,
                    $"Road EW {z:0}");
                count++;
            }

            foreach (float x in LaneCentres(Columns, ColumnWidths))
            {
                Strip(
                    roads,
                    new Vector3(x, 0.01f, 0f),
                    new Vector3(RoadWidth, 0.02f, townDepth),
                    material,
                    $"Road NS {x:0}");
                count++;
            }

            return count;
        }

        /// <summary>
        /// The centre of each road, walking along a row of uneven block sizes.
        /// </summary>
        private static IEnumerable<float> LaneCentres(
            int blocks,
            IReadOnlyList<float> sizes)
        {
            float span = sizes.Sum() + (blocks + 1) * RoadWidth;
            float cursor = -span * 0.5f;
            for (int lane = 0; lane <= blocks; lane++)
            {
                yield return cursor + RoadWidth * 0.5f;
                if (lane < blocks)
                {
                    cursor += RoadWidth + sizes[lane];
                }
            }
        }

        /// <summary>
        /// The middle of a block, and how big it is.
        /// </summary>
        private static Bounds BlockBounds(int row, int column)
        {
            float townWidth = ColumnWidths.Sum() + (Columns + 1) * RoadWidth;
            float x = -townWidth * 0.5f + RoadWidth;
            for (int index = 0; index < column; index++)
            {
                x += ColumnWidths[index] + RoadWidth;
            }

            x += ColumnWidths[column] * 0.5f;

            float townDepth = RowDepths.Sum() + (Rows + 1) * RoadWidth;
            float z = townDepth * 0.5f - RoadWidth;
            for (int index = 0; index < row; index++)
            {
                z -= RowDepths[index] + RoadWidth;
            }

            z -= RowDepths[row] * 0.5f;
            return new Bounds(
                new Vector3(x, 0f, z),
                new Vector3(ColumnWidths[column], 0f, RowDepths[row]));
        }

        private static void Strip(
            Transform parent,
            Vector3 centre,
            Vector3 size,
            Material material,
            string name)
        {
            GameObject strip =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent, false);
            strip.transform.position = centre;
            strip.transform.localScale = size;
            Object.DestroyImmediate(strip.GetComponent<Collider>());
            if (material != null)
            {
                strip.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        /// <summary>
        /// Fills the blocks: the special buildings across the middle row, houses
        /// everywhere else, and the middle block of the bottom row left open for the
        /// plaza.
        /// </summary>
        private static int BuildBlocks(
            Transform parent,
            Measurements sizes,
            out Vector3 plazaCentre)
        {
            plazaCentre = Vector3.zero;
            // Only the roofed house. The other variant is the one with its roof
            // removed so you can see the furniture inside, which from the street
            // reads as a house someone has taken the lid off.
            const string House = "building_house_1f";

            // The same lot the real town measures its houses against, so a character
            // stands next to a house here at exactly the proportion they do in the
            // game. Fitting houses to these blocks instead made them three times too
            // big, which is why they overlapped.
            const float HouseLotX = 12f;
            const float HouseLotZ = 8f;

            int placed = 0;
            int special = 0;
            int houseIndex = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Bounds block = BlockBounds(row, column);
                    Vector3 centre = block.center;

                    if (row == 2 && column == 1)
                    {
                        plazaCentre = centre;
                        continue;
                    }

                    if (row == 1 && special < MiddleBand.Length)
                    {
                        if (PlaceBuilding(
                                parent,
                                MiddleBand[special],
                                centre,
                                0f,
                                15f,
                                14f,
                                sizes))
                        {
                            placed++;
                        }

                        special++;
                        continue;
                    }

                    // Two houses per residential block, side by side across its long
                    // axis, each facing the nearer road. Four was too many: they
                    // ended up shoulder to shoulder with no yard between them.
                    // As many houses as fit at the game's own house size, in a row
                    // along the block's long axis with a gap between them. Spacing
                    // comes from the lot, not from the block, so they cannot overlap
                    // however the block sizes change.
                    bool wide = block.size.x >= block.size.z;
                    float along = wide ? block.size.x : block.size.z;
                    float pitch = HouseLotX + 4f;
                    int fit = Mathf.Max(1, Mathf.FloorToInt(along / pitch));
                    for (int slot = 0; slot < fit; slot++)
                    {
                        float offset = (slot - (fit - 1) * 0.5f) * pitch;
                        Vector3 spot = centre + (wide
                            ? new Vector3(offset, 0f, 0f)
                            : new Vector3(0f, 0f, offset));
                        houseIndex++;
                        if (PlaceBuilding(
                                parent,
                                House,
                                spot,
                                wide ? 0f : 90f,
                                HouseLotX,
                                HouseLotZ,
                                sizes))
                        {
                            placed++;
                        }
                    }
                }
            }

            // The jewellery shop has no FBX, only a .blend. A labelled greybox keeps
            // the layout honest instead of quietly having three destinations where
            // the design calls for four.
            PlaceJewelleryStandIn(parent, BlockBounds(2, 0).center);
            placed++;
            return placed;
        }

        /// <summary>
        /// One building, scaled to its lot and given a box to stand in the way.
        /// </summary>
        private static bool PlaceBuilding(
            Transform parent,
            string stem,
            Vector3 centre,
            float yaw,
            float lotWidth,
            float lotDepth,
            Measurements sizes)
        {
            Vector3 size = sizes.Of(stem);
            float scale = Mathf.Min(
                lotWidth / Mathf.Max(0.01f, size.x),
                lotDepth / Mathf.Max(0.01f, size.z));

            GameObject instance = Instantiate(
                stem,
                BuildingDirectory,
                parent,
                centre,
                Quaternion.Euler(0f, yaw, 0f),
                Vector3.one * scale,
                $"{stem} ({centre.x:0},{centre.z:0})");
            if (instance == null)
            {
                return false;
            }

            // Dropped onto the ground and boxed, from what it actually became rather
            // than from the lot it was asked to fill. The two differ on the shorter
            // axis of every fitted building.
            var bounds = new Bounds(centre, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!first)
            {
                instance.transform.position += new Vector3(
                    centre.x - bounds.center.x,
                    -bounds.min.y,
                    centre.z - bounds.center.z);
                BoxCollider box =
                    instance.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                box.size = bounds.size;
            }

            return true;
        }

        private static void PlaceJewelleryStandIn(
            Transform parent,
            Vector3 centre)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Jewellery Shop (greybox stand-in, no FBX)";
            box.transform.SetParent(parent, false);
            box.transform.position = centre + new Vector3(0f, 4f, 0f);
            box.transform.localScale = new Vector3(14f, 8f, 11f);
            box.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Materials/Greybox/JewelryStore.mat");
        }

        /// <summary>
        /// The plaza, some trees and lamps. Everything from the environment art the
        /// reference actually shows; nothing invented to fill space.
        /// </summary>
        private static int BuildDressing(
            Transform parent,
            float townWidth,
            float townDepth,
            Vector3 plazaCentre,
            Measurements sizes)
        {
            Transform dressing = Child("Dressing", parent);
            int count = 0;

            Bounds plazaBlock = BlockBounds(2, 1);
            Vector3 plazaSize = sizes.Of("env_fountain_plaza");
            float plazaScale = Mathf.Min(
                plazaBlock.size.x / Mathf.Max(0.01f, plazaSize.x),
                plazaBlock.size.z / Mathf.Max(0.01f, plazaSize.z));
            GameObject plaza = Instantiate(
                "env_fountain_plaza",
                EnvironmentDirectory,
                dressing,
                plazaCentre,
                Quaternion.identity,
                Vector3.one * plazaScale,
                "Plaza");
            if (plaza != null)
            {
                // The environment FBXs import without materials, so everything from
                // that folder renders white. Painted rather than left as it was: a
                // white plaza is not a plaza.
                Paint(plaza, LoadOrCreate(
                    "Sandbox_Plaza",
                    new Color(0.72f, 0.70f, 0.66f)));
                count++;
            }

            // Lamps on the road corners and a tree beside each one, which is where
            // the reference puts them.
            // Sized against the character, not guessed. A 4 m lamp and a 6 m tree
            // are what those things are, and the models come in at one unit tall —
            // at 1.6x they were ankle height and read as litter.
            Material lampMaterial = LoadOrCreate(
                "Sandbox_Lamp",
                new Color(0.24f, 0.25f, 0.28f));
            Material treeMaterial = LoadOrCreate(
                "Sandbox_Tree",
                new Color(0.22f, 0.42f, 0.24f));

            foreach (float x in LaneCentres(Columns, ColumnWidths))
            {
                foreach (float z in LaneCentres(Rows, RowDepths))
                {
                    GameObject lamp = Instantiate(
                        "env_street_lamp",
                        EnvironmentDirectory,
                        dressing,
                        new Vector3(x + RoadWidth * 0.38f, 0f,
                            z + RoadWidth * 0.38f),
                        Quaternion.identity,
                        Vector3.one * ScaleFor(sizes, "env_street_lamp", 4.5f),
                        $"Lamp ({x:0},{z:0})");
                    if (lamp != null)
                    {
                        Paint(lamp, lampMaterial);
                        count++;
                    }

                    GameObject tree = Instantiate(
                        "env_tree",
                        EnvironmentDirectory,
                        dressing,
                        new Vector3(x - RoadWidth * 0.38f, 0f,
                            z - RoadWidth * 0.38f),
                        Quaternion.identity,
                        Vector3.one * ScaleFor(sizes, "env_tree", 6f),
                        $"Tree ({x:0},{z:0})");
                    if (tree != null)
                    {
                        Paint(tree, treeMaterial);
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// One police character, the real motor, the real camera.
        ///
        /// No role board, no network link, no HUD and no companion: the point is to
        /// walk. Keyboard input is switched on directly, which in the real game the
        /// network bridge takes over.
        /// </summary>
        private static void BuildPlayer(
            Transform root,
            MatchRuntimeState match,
            Vector3 plazaCentre)
        {
            var player = new GameObject("Police Player");
            player.transform.SetParent(root, false);
            player.transform.position =
                plazaCentre + new Vector3(
                    0f,
                    1.2f,
                    -RowDepths[2] * 0.5f - RoadWidth * 0.5f);

            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.45f;

            GameObject model =
                PlaceholderModelLibrary.TryInstantiateAuthoredCharacter(
                    "police",
                    player.transform,
                    1.8f);
            if (model == null)
            {
                Debug.LogError(
                    "[SANDBOX] The police model did not instantiate, so there "
                    + "is nobody to walk.");
            }
            else
            {
                // The same guard the real scene uses. The shared locomotion
                // controller's clips come from TopDownEngine, which is not in the
                // repository, and an Animator left running on missing clips buries
                // the character in the ground — hips at 0.07 m instead of 0.45.
                var animator = model.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.gameObject
                        .AddComponent<PawsAndLoot.Animation
                            .AnimatorClipGuard>();
                }

                Debug.Log(
                    $"[SANDBOX] Police model '{model.name}' with "
                    + $"{model.GetComponentsInChildren<Renderer>(true).Length} "
                    + $"renderers at {player.transform.position}.");
            }

            var config =
                AssetDatabase.LoadAssetAtPath<PlayerConfig>(
                    "Assets/_Project/Settings/Configs/PlayerConfig.asset");
            if (config == null)
            {
                Debug.LogError(
                    "[SANDBOX] PlayerConfig is missing, so the player will not "
                    + "move. Run Create Default Config Assets first.");
                return;
            }

            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, config, match, null);
            player.AddComponent<PlayerKeyboardInput>()
                .Configure(motor, true);

            var cameraObject = new GameObject("Sandbox Camera");
            cameraObject.transform.SetParent(root, false);
            UnityEngine.Camera camera =
                cameraObject.AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 400f;

            // Higher and further back than the match camera. A shape test wants to
            // see the block you are standing in, not just the pavement.
            var offset = new Vector3(0f, 26f, -22f);
            cameraObject.AddComponent<TopDownFollowCamera>()
                .Configure(player.transform, offset, 0.12f);
            cameraObject.transform.position =
                player.transform.position + offset;
            cameraObject.transform.rotation =
                Quaternion.LookRotation(-offset.normalized, Vector3.up);
        }

        private static void BuildLight(Transform root)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(root, false);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation =
                Quaternion.Euler(48f, 35f, 0f);
        }

        /// <summary>
        /// The scale that makes a model a given number of metres tall. The
        /// environment models come in at roughly one unit, so a metre figure is the
        /// only thing worth writing down.
        /// </summary>
        private static float ScaleFor(
            Measurements sizes,
            string stem,
            float wantedHeight)
        {
            float height = Mathf.Max(0.01f, sizes.Of(stem).y);
            return wantedHeight / height;
        }

        /// <summary>
        /// Paints every renderer under an object, for the environment models that
        /// import without materials and would otherwise be white.
        /// </summary>
        private static void Paint(GameObject instance, Material material)
        {
            if (material == null)
            {
                return;
            }

            foreach (Renderer renderer in
                instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static GameObject Instantiate(
            string stem,
            string directory,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{directory}/{stem}.fbx");
            if (asset == null)
            {
                Debug.LogWarning($"[SANDBOX] '{stem}.fbx' not found.");
                return null;
            }

            var instance =
                (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return instance;
        }

        private static Material LoadOrCreate(string name, Color colour)
        {
            string path = $"Assets/_Project/Materials/Greybox/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }

            material.color = colour;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
