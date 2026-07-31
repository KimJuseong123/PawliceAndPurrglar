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

        /// <summary>
        /// What fills a block.
        /// </summary>
        private enum Use
        {
            Houses,
            Civic,
            Plaza,
            Green
        }

        /// <summary>
        /// A city block: a rectangle and what stands in it.
        /// </summary>
        private readonly struct Block
        {
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinZ;
            public readonly float MaxZ;
            public readonly Use Use;
            public readonly string Stem;
            public readonly string Label;

            public Block(
                float minX,
                float maxX,
                float minZ,
                float maxZ,
                Use use,
                string stem = null,
                string label = null)
            {
                MinX = minX;
                MaxX = maxX;
                MinZ = minZ;
                MaxZ = maxZ;
                Use = use;
                Stem = stem;
                Label = label;
            }

            public float Width => MaxX - MinX;
            public float Depth => MaxZ - MinZ;

            public Vector3 Centre => new(
                (MinX + MaxX) * 0.5f,
                0f,
                (MinZ + MaxZ) * 0.5f);
        }

        /// <summary>
        /// The town, authored as blocks rather than derived from a grid.
        ///
        /// This is the other way round from the first attempt and the reason it looked
        /// like a spreadsheet. Drawing evenly spaced roads and filling the leftovers
        /// gives identical square blocks by construction; the reference drawing is the
        /// opposite — a wide shallow terrace along the top, three big civic lots across
        /// the middle, narrow rotated rows down the sides, and alleys where two blocks
        /// nearly touch. So the blocks are written down and the roads are whatever is
        /// left between them, which is how a town actually reads.
        ///
        /// Four bands with different depths, and a different number of blocks in each
        /// so the vertical streets do not line up all the way through. Every figure is
        /// still the real map's: 80 x 72 m of ground, a 12 x 8 m lot, 4 m streets and
        /// 3 m alleys.
        /// </summary>
        private static readonly Block[] Blocks =
        {
            // North terrace: wide and shallow, split by one alley.
            new(-28f, -4f, 36f, 48f, Use.Houses),
            new(-1f, 23f, 36f, 48f, Use.Houses),
            new(27f, 52f, 36f, 48f, Use.Houses),

            // Second band: deeper, and its streets do not align with the terrace's.
            new(-28f, -12f, 18f, 32f, Use.Houses),
            new(-8f, 14f, 18f, 32f, Use.Houses),
            new(17f, 38f, 18f, 32f, Use.Houses),
            new(42f, 52f, 18f, 32f, Use.Green),

            // Civic band: the three destinations that have models, with a narrow
            // rotated row against the west wall.
            new(-28f, -16f, -2f, 14f, Use.Houses),
            new(-12f, 6f, -2f, 14f, Use.Civic, "building_supermarket",
                "Supermarket"),
            new(10f, 28f, -2f, 14f, Use.Civic, "building_police_station",
                "Police Station"),
            new(32f, 50f, -2f, 14f, Use.Civic, "building_bookstore",
                "Bookstore"),

            // South band: the plaza off-centre, the jewellery lot beside it behind an
            // alley, and terraces at both ends.
            new(-28f, -10f, -22f, -6f, Use.Houses),
            new(-6f, 12f, -22f, -6f, Use.Plaza),
            new(15f, 33f, -22f, -6f, Use.Civic, null, "Jewellery Store"),
            new(37f, 52f, -22f, -6f, Use.Houses)
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
            int placed = BuildBlocks(buildings, sizes, out int houses);
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
        /// <summary>
        /// Tarmac everywhere the blocks are not.
        ///
        /// Derived from the block table rather than listed, so a block cannot be moved
        /// without its street following. Between two neighbours in the same band the
        /// gap becomes a street if it is wide, an alley if it is narrow — which is
        /// where the reference's hiding places are — and the space between bands
        /// becomes a road across the whole map.
        /// </summary>
        private static int BuildRoadNetwork(Transform parent)
        {
            Transform roads = Child("Roads", parent);
            Material road = Load<Material>($"{MaterialDirectory}/Road.mat");
            Material plaza = Load<Material>($"{MaterialDirectory}/Plaza.mat");
            int count = 0;

            // Across, between the bands. Taken from the distinct band edges so the
            // count follows the table.
            float[] bandEdges = Blocks
                .SelectMany(block => new[] { block.MinZ, block.MaxZ })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            for (int edge = 0; edge < bandEdges.Length - 1; edge++)
            {
                float gap = bandEdges[edge + 1] - bandEdges[edge];
                if (gap < 1f || gap > 8f)
                {
                    // Either a band itself, or two bands that touch.
                    continue;
                }

                Slab(
                    roads,
                    $"Street z {(bandEdges[edge] + bandEdges[edge + 1]) * 0.5f:0}",
                    new Vector3(
                        MapCentreX,
                        0.02f,
                        (bandEdges[edge] + bandEdges[edge + 1]) * 0.5f),
                    new Vector3(MapWidth, 0.04f, gap),
                    road,
                    false);
                count++;
            }

            // Along, between neighbours in the same band, and only as long as the band
            // is deep. That is what stops the verticals running the whole way through
            // and gives the T-junctions.
            foreach (var band in Blocks.GroupBy(block => block.MinZ))
            {
                Block[] ordered = band
                    .OrderBy(block => block.MinX)
                    .ToArray();
                float depth = ordered[0].Depth;
                float centre = (ordered[0].MinZ + ordered[0].MaxZ) * 0.5f;

                // The map edge to the first block, then between each pair.
                var edges = new List<(float From, float To)>
                {
                    (MapMinX, ordered[0].MinX)
                };
                for (int index = 0; index < ordered.Length - 1; index++)
                {
                    edges.Add((ordered[index].MaxX, ordered[index + 1].MinX));
                }

                edges.Add((ordered[^1].MaxX, MapMaxX));

                foreach ((float from, float to) in edges)
                {
                    float gap = to - from;
                    if (gap < 1f)
                    {
                        continue;
                    }

                    bool alley = gap < 3.5f;
                    Slab(
                        roads,
                        alley
                            ? $"Alley x {(from + to) * 0.5f:0}"
                            : $"Street x {(from + to) * 0.5f:0}",
                        new Vector3((from + to) * 0.5f, 0.03f, centre),
                        new Vector3(gap, 0.06f, depth),
                        road,
                        false);
                    count++;
                }
            }

            // The plaza floor, under the fountain.
            Block plazaBlock = Blocks.First(block => block.Use == Use.Plaza);
            Slab(
                roads,
                "Central Plaza",
                new Vector3(plazaBlock.Centre.x, 0.05f, plazaBlock.Centre.z),
                new Vector3(plazaBlock.Width, 0.1f, plazaBlock.Depth),
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

        /// <summary>
        /// Everything that stands in a block, from the same table the roads came from.
        ///
        /// Houses are laid out on the real 12 x 8 m lot with a 2 m verge, rotated to
        /// face the near street, and a block that is deeper than it is wide gets them
        /// turned sideways — which is what puts the rows down the map's edges facing
        /// inward, as the reference has them.
        /// </summary>
        private static int BuildBlocks(
            Transform parent,
            Measurements sizes,
            out int houses)
        {
            const float Verge = 2f;
            int specials = 0;
            houses = 0;

            foreach (Block block in Blocks)
            {
                switch (block.Use)
                {
                    case Use.Plaza:
                    case Use.Green:
                        continue;

                    case Use.Civic when block.Stem == null:
                        // The jewellery shop exists only as a .blend, and Unity is not
                        // asked to import those — that would make the build depend on
                        // Blender being installed. A greybox of the store footprint,
                        // which is what the real town uses for it too.
                        Slab(
                            parent,
                            $"{block.Label} (greybox, no FBX)",
                            block.Centre + new Vector3(0f, 1.8f, 0f),
                            new Vector3(LotX, 3.6f, LotZ),
                            Load<Material>(
                                $"{MaterialDirectory}/JewelryStore.mat"),
                            true);
                        specials++;
                        continue;

                    case Use.Civic:
                        if (PlaceBuilding(
                                parent,
                                block.Stem,
                                block.Centre,
                                block.Centre.z > MapCentreZ ? 180f : 0f,
                                block.Label,
                                sizes))
                        {
                            specials++;
                        }

                        continue;
                }

                // Houses. Rotated when the block is deeper than it is wide, so the lot
                // runs along the block rather than across it.
                bool sideways = block.Depth > block.Width;
                float lotAlong = sideways ? LotZ : LotX;
                float lotAcross = sideways ? LotX : LotZ;
                float along = (sideways ? block.Width : block.Width)
                    - Verge * 2f;
                float across = block.Depth - Verge * 2f;
                if (sideways)
                {
                    along = block.Depth - Verge * 2f;
                    across = block.Width - Verge * 2f;
                }

                if (along < lotAlong || across < lotAcross)
                {
                    continue;
                }

                int fit = Mathf.Max(
                    1,
                    Mathf.FloorToInt(along / (lotAlong + 2f)));
                int rows = Mathf.Max(
                    1,
                    Mathf.FloorToInt(across / (lotAcross + 2f)));
                for (int row = 0; row < rows; row++)
                {
                    float rowOffset =
                        (row - (rows - 1) * 0.5f) * (lotAcross + 2f);
                    for (int slot = 0; slot < fit; slot++)
                    {
                        float slotOffset =
                            (slot - (fit - 1) * 0.5f) * (lotAlong + 2f);
                        Vector3 at = block.Centre + (sideways
                            ? new Vector3(rowOffset, 0f, slotOffset)
                            : new Vector3(slotOffset, 0f, rowOffset));

                        // Facing the nearer long edge of its own block.
                        float yaw = sideways
                            ? (rowOffset <= 0f ? 270f : 90f)
                            : (rowOffset <= 0f ? 0f : 180f);
                        if (PlaceBuilding(
                                parent,
                                "building_house_1f",
                                at,
                                yaw,
                                $"House ({at.x:0},{at.z:0})",
                                sizes))
                        {
                            houses++;
                        }
                    }
                }
            }

            return specials;
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
                Blocks.First(block => block.Use == Use.Plaza).Centre
                    + new Vector3(0f, 0.12f, 0f),
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

            // On the corners of every block, just outside it, so lamps and trees
            // line the streets without standing in a lane. Taken from the block table
            // like everything else, so moving a block moves its planting.
            foreach (Block block in Blocks)
            {
                if (block.Use == Use.Plaza)
                {
                    continue;
                }

                var corners = new[]
                {
                    new Vector3(block.MinX - 1.4f, 0f, block.MinZ - 1.4f),
                    new Vector3(block.MaxX + 1.4f, 0f, block.MaxZ + 1.4f)
                };

                GameObject lamp = Instantiate(
                    "env_street_lamp",
                    EnvironmentDirectory,
                    dressing,
                    corners[0],
                    Quaternion.identity,
                    Vector3.one
                        * ScaleTo(sizes, "env_street_lamp", 4.5f, false),
                    $"Lamp ({corners[0].x:0},{corners[0].z:0})");
                if (lamp != null)
                {
                    Paint(lamp, metal);
                    count++;
                }

                GameObject tree = Instantiate(
                    "env_tree",
                    EnvironmentDirectory,
                    dressing,
                    corners[1],
                    Quaternion.identity,
                    Vector3.one * ScaleTo(sizes, "env_tree", 6f, false),
                    $"Tree ({corners[1].x:0},{corners[1].z:0})");
                if (tree != null)
                {
                    PaintTree(tree, bark, leaves);
                    count++;
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

            // The same procedural biped walk the real scene uses. Without it the
            // character slides: the locomotion clips come from TopDownEngine, which
            // is not in the repository, so nothing moves the legs unless this does.
            if (model != null)
            {
                var stride = player.AddComponent<
                    PawsAndLoot.Animation.CompanionLegAnimator>();
                stride.Configure(
                    model.transform,
                    PawsAndLoot.Animation.CompanionLegAnimator.GaitMode.Biped,
                    model.GetComponent<Animator>());
                if (stride.LegCount == 0)
                {
                    Debug.LogWarning(
                        "[SANDBOX] No limb bones found, so the walk will not "
                        + "play.");
                }
            }

            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, playerConfig, match, null);
            player.AddComponent<PlayerKeyboardInput>().Configure(motor, true);
            player.GetComponent<PawsAndLoot.Animation.CompanionLegAnimator>()
                ?.ConfigureAirborneSource(motor);

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
