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
    /// A throwaway town to walk around in, built to the real map's numbers.
    ///
    /// Separate from <c>Game.unity</c> completely: its own scene file, its own folder,
    /// its own build output, and never added to the build scene list — that list is
    /// generated from <c>GameSceneCatalog</c> and validated to hold exactly three
    /// entries, so a fourth would break the shipping contract to serve a sandbox.
    ///
    /// Every dimension here is the town's own. Inventing a grid produced a place that
    /// looked like a spreadsheet and, worse, told you nothing: a street that feels
    /// comfortable at a made-up width says nothing about the street the game has. So
    /// the road lines, their widths, the 12 x 8 m building lot and the destinations'
    /// positions are read from the same constants <c>GreyboxMapSetup</c> uses, and
    /// this differs only in what stands in the blocks.
    ///
    /// It is a shape test, not a game: one police character with the real motor,
    /// controller and camera, and no thief, animals, HUD or session.
    /// </summary>
    internal static class MapSandboxSetup
    {
        public const string ScenePath =
            "Assets/_Project/Sandbox/MapSandbox.unity";

        private const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";
        private const string EnvironmentDirectory =
            "Assets/_Project/Art/Environment";
        private const string MaterialDirectory =
            "Assets/_Project/Materials/Greybox";

        // The town, exactly as the real one measures itself.
        private const float MapMinX = -28f;
        private const float MapMaxX = 52f;
        private const float MapMinZ = -22f;
        private const float MapMaxZ = 50f;
        private const float MapWidth = MapMaxX - MapMinX;
        private const float MapDepth = MapMaxZ - MapMinZ;
        private const float MapCentreX = (MapMinX + MapMaxX) * 0.5f;
        private const float MapCentreZ = (MapMinZ + MapMaxZ) * 0.5f;

        private const float RoadInset = 2f;
        private const float RoadSpanX = MapWidth - RoadInset * 2f;
        private const float RoadSpanZ = MapDepth - RoadInset * 2f;

        private const float NorthStreetNear = 26f;
        private const float NorthStreetFar = 39f;
        private const float EastStreet = 40f;

        /// <summary>
        /// The lot every building in the town is fitted to, and the reason the houses
        /// stopped overlapping. Fitting them to a block instead made them three times
        /// too big for the character standing next to them.
        /// </summary>
        private const float LotX = 12f;
        private const float LotZ = 8f;

        private static readonly float[] EastWestRoads =
        {
            0f, 12f, -12f, NorthStreetNear, NorthStreetFar
        };

        private static readonly float[] NorthSouthRoads =
        {
            -24f, -18f, 0f, 18f, 24f, EastStreet
        };

        /// <summary>
        /// Where the town puts its four destinations. Kept rather than re-invented, so
        /// that walking between them here takes as long as it does in the game.
        /// </summary>
        private static readonly (string Name, string Stem, Vector3 At)[]
            Specials =
            {
                ("Supermarket", "building_supermarket",
                    new Vector3(-9f, 0f, 6f)),
                ("Bookstore", "building_bookstore",
                    new Vector3(9f, 0f, 6f)),
                ("Police Station", "building_police_station",
                    new Vector3(-9f, 0f, 19f)),

                // The jewellery shop exists only as a .blend, and Unity is not asked
                // to import those — that would make the build depend on Blender being
                // installed. A greybox of the same footprint, which is what the real
                // town uses for it as well.
                ("Jewellery Store", null, new Vector3(9f, 0f, -6f))
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

            BuildGround(environment);
            int roads = BuildRoadNetwork(environment);
            int placed = BuildSpecials(buildings, sizes);
            int houses = BuildHouses(buildings, sizes);
            int dressing = BuildDressing(environment, sizes);

            BuildPlayer(root, match);
            BuildLight(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                "[SANDBOX] Rebuilt to the real map's numbers: "
                + $"{MapWidth:0}x{MapDepth:0}m, {roads} road strips, "
                + $"{placed} destinations, {houses} houses on a "
                + $"{LotX:0}x{LotZ:0}m lot, {dressing} pieces of dressing.");
        }

        /// <summary>
        /// A player you can walk around in, from the sandbox scene alone. Passed to
        /// the pipeline directly rather than registered, so the shipping build's
        /// three-scene contract is left alone.
        /// </summary>
        [MenuItem("Paws & Loot/Sandbox/Build Windows Map Sandbox")]
        public static void BuildSandboxPlayer()
        {
            const string output = "Builds/Sandbox/Windows/MapSandbox.exe";
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
        /// The footprint of every model this uses, measured once rather than assumed.
        /// The main map has been bitten twice by eyeballed coordinates.
        /// </summary>
        private struct Measurements
        {
            public Dictionary<string, Vector3> Sizes;

            public Vector3 Of(string stem)
            {
                return stem != null
                    && Sizes.TryGetValue(stem, out Vector3 size)
                        ? size
                        : Vector3.one;
            }
        }

        private static Measurements Measure()
        {
            var sizes = new Dictionary<string, Vector3>();
            foreach (string path in AssetDatabase
                .FindAssets("t:Model", new[]
                {
                    BuildingDirectory,
                    EnvironmentDirectory
                })
                .Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    continue;
                }

                GameObject probe = Object.Instantiate(asset);
                probe.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                probe.transform.localScale = Vector3.one;
                sizes[System.IO.Path.GetFileNameWithoutExtension(path)] =
                    WorldBounds(probe).size;
                Object.DestroyImmediate(probe);
            }

            return new Measurements { Sizes = sizes };
        }

        private static Bounds WorldBounds(GameObject instance)
        {
            var bounds = new Bounds(instance.transform.position, Vector3.zero);
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

            return bounds;
        }

        private static Transform Child(string name, Transform parent)
        {
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created.transform;
        }

        private static MatchRuntimeState BuildSystems(Transform root)
        {
            Transform systems = Child("Systems", root);
            MatchRuntimeState match =
                systems.gameObject.AddComponent<MatchRuntimeState>();

            var config = Load<MatchConfig>(
                "Assets/_Project/Settings/Configs/MatchConfig.asset");
            if (config == null)
            {
                Debug.LogError(
                    "[SANDBOX] MatchConfig is missing, so nothing will move. "
                    + "Run Create Default Config Assets first.");
            }

            // Started automatically, because the state machine goes Lobby then
            // Countdown then Playing and should not be made to jump the queue.
            match.Configure(config, true);
            systems.gameObject.AddComponent<SandboxFreeRoam>().Configure(match);
            return match;
        }

        private static void BuildGround(Transform parent)
        {
            Slab(
                parent,
                "Ground",
                new Vector3(MapCentreX, -0.5f, MapCentreZ),
                new Vector3(MapWidth, 1f, MapDepth),
                Load<Material>($"{MaterialDirectory}/Ground.mat"),
                true);
        }

        /// <summary>
        /// The town's road network, line for line: five east-west streets, a southern
        /// alley, and six north-south routes with the wide ones at x = 0, +/-18 and
        /// the east street.
        ///
        /// Flat tiles, as in the game. The environment art's road tile is 1 m with a
        /// raised bevel, so one scaled up to fill a 4 m lane reads as a row of
        /// separate slabs — laying it as a real 1 m grid is a job worth doing once
        /// these widths are settled.
        /// </summary>
        private static int BuildRoadNetwork(Transform parent)
        {
            Transform roads = Child("Roads", parent);
            Material road = Load<Material>($"{MaterialDirectory}/Road.mat");
            Material plaza = Load<Material>($"{MaterialDirectory}/Plaza.mat");
            int count = 0;

            foreach (float z in EastWestRoads)
            {
                bool central = Mathf.Approximately(z, 0f);
                Slab(
                    roads,
                    $"East-West Road {z:0}",
                    new Vector3(MapCentreX, central ? 0.02f : 0.025f, z),
                    new Vector3(
                        central ? MapWidth : RoadSpanX,
                        central ? 0.04f : 0.05f,
                        4f),
                    road,
                    false);
                count++;
            }

            Slab(
                roads,
                "South Outer Alley",
                new Vector3(MapCentreX, 0.03f, -18f),
                new Vector3(RoadSpanX, 0.06f, 3f),
                road,
                false);
            count++;

            foreach (float x in NorthSouthRoads)
            {
                bool wide = Mathf.Approximately(x, 0f)
                    || Mathf.Approximately(Mathf.Abs(x), 18f)
                    || Mathf.Approximately(x, EastStreet);
                Slab(
                    roads,
                    $"North-South Road {x:0}",
                    new Vector3(x, 0.035f, MapCentreZ),
                    new Vector3(wide ? 4f : 3f, 0.07f, RoadSpanZ),
                    road,
                    false);
                count++;
            }

            Slab(
                roads,
                "Central Plaza",
                new Vector3(0f, 0.08f, 0f),
                new Vector3(8f, 0.12f, 8f),
                plaza,
                false);
            count++;
            return count;
        }

        private static void Slab(
            Transform parent,
            string name,
            Vector3 centre,
            Vector3 size,
            Material material,
            bool solid)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.SetParent(parent, false);
            slab.transform.position = centre;
            slab.transform.localScale = size;
            if (!solid)
            {
                Object.DestroyImmediate(slab.GetComponent<Collider>());
            }

            if (material != null)
            {
                slab.GetComponent<Renderer>().sharedMaterial = material;
            }
        }

        private static int BuildSpecials(
            Transform parent,
            Measurements sizes)
        {
            int placed = 0;
            foreach ((string name, string stem, Vector3 at) in Specials)
            {
                if (stem == null)
                {
                    Slab(
                        parent,
                        $"{name} (greybox, no FBX)",
                        at + new Vector3(0f, 1.8f, 0f),
                        new Vector3(LotX, 3.6f, LotZ),
                        Load<Material>(
                            $"{MaterialDirectory}/JewelryStore.mat"),
                        true);
                    placed++;
                    continue;
                }

                if (PlaceBuilding(parent, stem, at, 0f, name, sizes))
                {
                    placed++;
                }
            }

            return placed;
        }

        /// <summary>
        /// Houses in the blocks the roads leave behind.
        ///
        /// Derived rather than listed. A cross-product of the town's district rows and
        /// columns put most of the houses on tarmac — the real map places specific
        /// houses at specific points, and copying the lines without the placements
        /// gave two houses in an empty town. So the blocks are worked out from the
        /// roads themselves: the gap between each pair of neighbouring streets is a
        /// band, and a band wide enough for a lot gets houses along it.
        ///
        /// Only the roofed variant. The other one has had its roof removed so the
        /// furniture inside can be seen, which from the street reads as a house with
        /// its lid off.
        /// </summary>
        private static int BuildHouses(
            Transform parent,
            Measurements sizes)
        {
            float[] eastWest = EastWestRoads
                .Concat(new[] { -18f, MapMinZ, MapMaxZ })
                .OrderBy(value => value)
                .ToArray();
            float[] northSouth = NorthSouthRoads
                .Concat(new[] { MapMinX, MapMaxX })
                .OrderBy(value => value)
                .ToArray();

            // Half a road, and no more.
            //
            // Three metres looked safer and emptied the town: the streets sit 12 m
            // apart and the lot is 8 m deep, so 2 m of verge on each side is exactly
            // what fits — which is what the real map does. An extra metre disqualified
            // every 12 m band and left one house standing on its own.
            const float Margin = 2f;

            int placed = 0;
            for (int band = 0; band < eastWest.Length - 1; band++)
            {
                float depth = eastWest[band + 1] - eastWest[band]
                    - Margin * 2f;
                if (depth < LotZ)
                {
                    continue;
                }

                float z = (eastWest[band] + eastWest[band + 1]) * 0.5f;
                for (int column = 0;
                    column < northSouth.Length - 1;
                    column++)
                {
                    float width = northSouth[column + 1] - northSouth[column]
                        - Margin * 2f;
                    if (width < LotX)
                    {
                        continue;
                    }

                    // As many as fit across the block, spaced by the lot itself so
                    // they cannot overlap however the roads move.
                    int fit = Mathf.Max(
                        1,
                        Mathf.FloorToInt(width / (LotX + 2f)));
                    float centre =
                        (northSouth[column] + northSouth[column + 1]) * 0.5f;
                    for (int slot = 0; slot < fit; slot++)
                    {
                        float x = centre
                            + (slot - (fit - 1) * 0.5f) * (LotX + 2f);
                        var at = new Vector3(x, 0f, z);
                        if (NearASpecial(at))
                        {
                            continue;
                        }

                        if (PlaceBuilding(
                                parent,
                                "building_house_1f",
                                at,
                                z > MapCentreZ ? 180f : 0f,
                                $"House ({x:0},{z:0})",
                                sizes))
                        {
                            placed++;
                        }
                    }
                }
            }

            return placed;
        }

        private static bool NearASpecial(Vector3 at)
        {
            foreach ((string _, string _, Vector3 special) in Specials)
            {
                if (Mathf.Abs(at.x - special.x) < LotX + 2f
                    && Mathf.Abs(at.z - special.z) < LotZ + 2f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One building on the town's lot, dropped onto the ground and boxed.
        ///
        /// Boxed from what the model actually became rather than from the lot it was
        /// asked to fill: the two differ on the shorter axis of every fitted building,
        /// and that difference used to be an invisible wall standing off the side of
        /// it.
        /// </summary>
        private static bool PlaceBuilding(
            Transform parent,
            string stem,
            Vector3 centre,
            float yaw,
            string name,
            Measurements sizes)
        {
            Vector3 size = sizes.Of(stem);
            float scale = Mathf.Min(
                LotX / Mathf.Max(0.01f, size.x),
                LotZ / Mathf.Max(0.01f, size.z));

            GameObject instance = Instantiate(
                stem,
                BuildingDirectory,
                parent,
                centre,
                Quaternion.Euler(0f, yaw, 0f),
                Vector3.one * scale,
                name);
            if (instance == null)
            {
                return false;
            }

            Bounds bounds = WorldBounds(instance);
            instance.transform.position += new Vector3(
                centre.x - bounds.center.x,
                -bounds.min.y,
                centre.z - bounds.center.z);

            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
            box.size = bounds.size;
            return true;
        }

        /// <summary>
        /// The fountain on the plaza, and trees and lamps along the streets.
        ///
        /// The environment FBXs import without materials, so everything from that
        /// folder renders white and has to be painted. The trees are painted in two
        /// parts by height rather than by name: bark below, leaves above, because one
        /// colour over the whole model gave a tree with a green trunk.
        /// </summary>
        private static int BuildDressing(
            Transform parent,
            Measurements sizes)
        {
            Transform dressing = Child("Dressing", parent);
            int count = 0;

            GameObject fountain = Instantiate(
                "env_fountain_plaza",
                EnvironmentDirectory,
                dressing,
                new Vector3(0f, 0.14f, 0f),
                Quaternion.identity,
                Vector3.one * ScaleTo(sizes, "env_fountain_plaza", 8f, true),
                "Fountain");
            if (fountain != null)
            {
                Paint(
                    fountain,
                    Colour("Sandbox_Stone", new Color(0.55f, 0.53f, 0.50f)));
                count++;
            }

            Material bark =
                Colour("Sandbox_Bark", new Color(0.34f, 0.24f, 0.16f));
            Material leaves =
                Colour("Sandbox_Leaves", new Color(0.20f, 0.42f, 0.22f));
            Material metal =
                Colour("Sandbox_LampPost", new Color(0.20f, 0.21f, 0.24f));

            // On the corners where the streets cross, pulled clear of the tarmac so
            // that nothing stands in a lane.
            foreach (float x in NorthSouthRoads)
            {
                foreach (float z in EastWestRoads)
                {
                    if (Mathf.Approximately(z, 0f))
                    {
                        // The central street runs through the plaza; leave it clear.
                        continue;
                    }

                    GameObject lamp = Instantiate(
                        "env_street_lamp",
                        EnvironmentDirectory,
                        dressing,
                        new Vector3(x + 3.4f, 0f, z + 3.4f),
                        Quaternion.identity,
                        Vector3.one
                            * ScaleTo(sizes, "env_street_lamp", 4.5f, false),
                        $"Lamp ({x:0},{z:0})");
                    if (lamp != null)
                    {
                        Paint(lamp, metal);
                        count++;
                    }

                    GameObject tree = Instantiate(
                        "env_tree",
                        EnvironmentDirectory,
                        dressing,
                        new Vector3(x - 3.4f, 0f, z - 3.4f),
                        Quaternion.identity,
                        Vector3.one * ScaleTo(sizes, "env_tree", 6f, false),
                        $"Tree ({x:0},{z:0})");
                    if (tree != null)
                    {
                        PaintTree(tree, bark, leaves);
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// The scale that makes a model a given size. The environment models come in
        /// at roughly one unit, so a metre figure is the only thing worth writing
        /// down: at 1.6x the lamps were ankle height and read as litter.
        /// </summary>
        private static float ScaleTo(
            Measurements sizes,
            string stem,
            float wanted,
            bool byFootprint)
        {
            Vector3 size = sizes.Of(stem);
            float reference = byFootprint
                ? Mathf.Max(size.x, size.z)
                : size.y;
            return wanted / Mathf.Max(0.01f, reference);
        }

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

        /// <summary>
        /// Bark low, leaves high, split by where each part sits rather than by what it
        /// is called. A name list would need the model's own naming and would fail
        /// silently the day it changed; height is a property of a tree.
        /// </summary>
        private static void PaintTree(
            GameObject tree,
            Material bark,
            Material leaves)
        {
            Bounds whole = WorldBounds(tree);
            float split = whole.min.y + whole.size.y * 0.38f;
            foreach (Renderer renderer in
                tree.GetComponentsInChildren<Renderer>(true))
            {
                Material chosen =
                    renderer.bounds.center.y < split ? bark : leaves;
                if (chosen != null)
                {
                    renderer.sharedMaterial = chosen;
                }
            }
        }

        /// <summary>
        /// One police character with the real motor, controller and camera.
        ///
        /// The controller is centred on its object's origin, not on half its height,
        /// and that is what stopped the character being buried. The authored model is
        /// placed with its feet at the object's pivot, and a capsule's bottom sits at
        /// <c>pivot + center.y - height/2</c> — so a centre of 0.9 put the bottom at
        /// the pivot, the pivot on the ground, and the feet 0.9 m under it. A centre
        /// of zero puts the bottom 0.9 m below the pivot instead, which is what the
        /// real game does: its officer stands with a pivot at 1.08 and foot bones at
        /// 0.195, a gap of exactly half the capsule.
        /// </summary>
        private static void BuildPlayer(
            Transform root,
            MatchRuntimeState match)
        {
            var player = new GameObject("Police Player");
            player.transform.SetParent(root, false);

            // Just off the plaza, which is the middle of the town.
            player.transform.position = new Vector3(0f, 1.2f, -6f);

            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = Vector3.zero;
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
                    "[SANDBOX] The police model did not instantiate.");
            }
            else
            {
                // The guard the real scene uses. The shared locomotion controller's
                // clips come from TopDownEngine, which is not in the repository, and
                // an Animator left running on missing clips buries the character.
                var animator = model.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.gameObject.AddComponent<
                        PawsAndLoot.Animation.AnimatorClipGuard>();
                }
            }

            var playerConfig = Load<PlayerConfig>(
                "Assets/_Project/Settings/Configs/PlayerConfig.asset");
            if (playerConfig == null)
            {
                Debug.LogError(
                    "[SANDBOX] PlayerConfig is missing, so the player will not "
                    + "move. Run Create Default Config Assets first.");
                return;
            }

            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, playerConfig, match, null);
            player.AddComponent<PlayerKeyboardInput>().Configure(motor, true);

            var cameraObject = new GameObject("Sandbox Camera");
            cameraObject.transform.SetParent(root, false);
            UnityEngine.Camera camera =
                cameraObject.AddComponent<UnityEngine.Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 400f;

            // Higher and further back than the match camera: a shape test wants to
            // see the block you are standing in, not just the pavement.
            var offset = new Vector3(0f, 22f, -18f);
            cameraObject.AddComponent<TopDownFollowCamera>()
                .Configure(player.transform, offset, 0.12f);
            cameraObject.transform.SetPositionAndRotation(
                player.transform.position + offset,
                Quaternion.LookRotation(-offset.normalized, Vector3.up));

            Debug.Log(
                $"[SANDBOX] Police pivot {player.transform.position}, capsule "
                + $"bottom {controller.bounds.min.y:0.00}.");
        }

        private static void BuildLight(Transform root)
        {
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(root, false);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, 35f, 0f);
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

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>
        /// A coloured material, cloned from one the project already ships so the
        /// shader is whatever the render pipeline actually wants.
        /// <c>Shader.Find</c> in batch mode returned something that rendered brown.
        /// </summary>
        private static Material Colour(string name, Color colour)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Tint(existing, colour);
                return existing;
            }

            var template = Load<Material>($"{MaterialDirectory}/Ground.mat");
            if (template == null)
            {
                return null;
            }

            var material = new Material(template) { name = name };
            Tint(material, colour);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Tint(Material material, Color colour)
        {
            material.color = colour;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", colour);
            }
        }
    }
}
