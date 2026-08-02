using System.Collections.Generic;
using System.IO;
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
        private const string GeneratedDirectory =
            "Assets/_Project/Art/Generated";

        /// <summary>
        /// How far the road tile has to turn before its markings run the way an
        /// east-west street does. One number, because the whole grid hangs off
        /// it and getting it wrong turns every centre line sideways.
        /// </summary>
        private const float RoadTileYaw = 0f;

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
        /// One street, by its centre line.
        ///
        /// Written down rather than derived from the blocks. The blocks came
        /// first while the town was being invented; now there is a drawing of
        /// the streets to match, and deriving them from something else would
        /// mean editing that something else and hoping the roads followed.
        /// </summary>
        private readonly struct Street
        {
            public Street(
                string name,
                bool horizontal,
                float fixedCoordinate,
                float from,
                float to,
                float width)
            {
                Name = name;
                Horizontal = horizontal;
                FixedCoordinate = fixedCoordinate;
                From = from;
                To = to;
                Width = width;
            }

            public string Name { get; }
            public bool Horizontal { get; }
            public float FixedCoordinate { get; }
            public float From { get; }
            public float To { get; }
            public float Width { get; }
        }

        /// <summary>
        /// The size of one road tile, and therefore the width of every road.
        ///
        /// The pieces are modular — straight, corner, T, crossroads — and they
        /// only meet if they are laid on one lattice. Four metres because that
        /// is what the streets already were: at six the two roads five metres
        /// apart in the south east would have merged into one, and the plan
        /// would have changed to suit the tiles rather than the other way
        /// round. The alleys widen from three to four to join the grid.
        /// </summary>
        private const float RoadCell = 4f;

        private const float StreetWidth = RoadCell;
        private const float AlleyWidth = RoadCell;

        /// <summary>
        /// The street plan, read off the drawn map.
        ///
        /// Three bands across and six ways down, and the ways down do not all
        /// run the full height — that is what makes T-junctions instead of a
        /// grid, and it is what the drawing shows.
        /// </summary>
        /// Written in the town's own coordinates, which run -28..52 by -22..50
        /// rather than from zero. Read off the drawing as 0..80 by 0..72 and
        /// shifted once here, so the drawing stays readable next to the numbers
        /// instead of every line carrying the offset in its head.
        private static readonly Street[] Streets =
        {
            Across("North Street", 56f, 0f, 80f, StreetWidth),
            Across("Center Street", 28f, 0f, 80f, StreetWidth),

            // The south road stops well short of the east alley, and a second
            // one takes over on the far side of it. Two roads on the same line
            // with ground between them rather than one that runs the width:
            // the south-east has its own way out to the east edge, and getting
            // there from the west means going round instead of straight along.
            // Reaches four metres further east than it used to, to the tile
            // where the plaza east street now comes down to meet it. The two
            // used to stop one tile apart on the diagonal — near enough to see
            // across and not near enough to walk — and both ends were capped
            // off. Not further: the road out to the east is meant to stay
            // separate from this one.
            Across("South Street", 13f, 15f, 52f, StreetWidth),
            Across("South East Street", 13f, 54f, 80f, StreetWidth),

            // Sits flush on the centre road rather than floating above it, so
            // the two read as one wider opening behind the supermarket instead
            // of a lane with a one-metre ribbon of ground trapped under it.
            //
            // Dropping it also removed the need for a link down to the road:
            // touching along its whole length is a better connection than a
            // stub at one end.
            Across("Market Lane", 32f, 12f, 24f, StreetWidth),

            Down("North West Alley", 9f, 56f, 72f, AlleyWidth),
            Down("North Alley", 33f, 56f, 72f, AlleyWidth),

            Down("Market Street", 25f, 13f, 56f, StreetWidth),

            // Stops at the centre road now. Running it to the south edge made
            // the plaza block a corridor rather than a place.
            Down("Station Street", 42f, 28f, 56f, StreetWidth),

            // The south band's own ways down, offset from the ones above so the
            // two bands do not line up into one long straight.
            // Runs off the south edge, so the south-west corner has a way out
            // of its own rather than only the road it came in on.
            Down("West Lower Street", 17f, 0f, 27f, StreetWidth),
            // Its east edge lines up with where the south road stops, so the
            // junction is a clean corner instead of a metre of road carrying on
            // past the turn.
            // Down to the south street, turning the corner rather than
            // stopping above it.
            Down("Plaza East Street", 48f, 12f, 27f, StreetWidth),
            // Runs to the far side of the road rather than stopping on its
            // centre line. Ending halfway across left the alley's lighter
            // surface as a tongue poking into the road, which reads as a
            // mistake rather than a junction.

            // The south east alley used to run four metres west of this one,
            // from the same street, to a dead end two thirds of the way up.
            // Two roads five metres apart cannot both exist on a four metre
            // grid — they came out as one twelve metre slab of tarmac with
            // three centre lines down it — and this is the one that goes
            // somewhere.
            Down("Bookstore Street", 59f, 13f, 56f, StreetWidth)

            // The east street is gone. It ran the full height a few metres from
            // the map edge and left a ribbon of ground nothing could stand in.
        };

        /// <summary>
        /// A street running east to west, snapped onto the tile lattice.
        ///
        /// The plan was drawn in metres and the road is built from square
        /// tiles, so the two have to agree about where a road is. Left
        /// unsnapped, a street whose middle fell between two rows of tiles
        /// claimed the row it leant into, and that row was somebody's front
        /// garden: the police station, the supermarket and the square all lost
        /// their plots to a road that was not there on the plan.
        ///
        /// Snapping moves a street by at most half a tile and settles the
        /// argument in the plan's favour, since everything downstream — blocks,
        /// buildings, lamps — is measured from these same numbers.
        /// </summary>
        private static Street Across(
            string name,
            float z,
            float fromX,
            float toX,
            float width)
        {
            return new Street(
                name,
                true,
                ToLane(z) + MapMinZ,
                ToEdge(fromX) + MapMinX,
                ToEdge(toX) + MapMinX,
                width);
        }

        /// <summary>
        /// Complains about roads that ended up shoulder to shoulder.
        ///
        /// Two parallel streets less than two tiles apart snap onto touching
        /// rows and stop being two streets. Nothing downstream notices: the
        /// cells are laid, the blocks flood around them, the validator passes.
        /// It shows up only as a wide pale ribbon in the middle of the town,
        /// which is how it was found.
        /// </summary>
        private static void ReportCrowdedStreets()
        {
            var lanes = new Dictionary<(bool, int), List<string>>();
            foreach (Street street in Streets)
            {
                int lane = street.Horizontal
                    ? CellAt(0f, street.FixedCoordinate).y
                    : CellAt(street.FixedCoordinate, 0f).x;
                var key = (street.Horizontal, lane);
                if (!lanes.TryGetValue(key, out List<string> names))
                {
                    names = new List<string>();
                    lanes[key] = names;
                }

                names.Add(street.Name);
            }

            foreach (((bool horizontal, int lane), List<string> names) in lanes)
            {
                var key = (horizontal, lane + 1);
                if (lanes.TryGetValue(key, out List<string> next))
                {
                    Debug.LogWarning(
                        $"[SANDBOX] {string.Join(", ", names)} and "
                        + $"{string.Join(", ", next)} are on touching rows of "
                        + "tiles. They will be drawn as one wide road, not two "
                        + "with something between them.");
                }
            }
        }

        /// <summary>The middle of the nearest row of tiles.</summary>
        private static float ToLane(float coordinate)
        {
            return Mathf.Round((coordinate - RoadCell * 0.5f) / RoadCell)
                * RoadCell
                + RoadCell * 0.5f;
        }

        /// <summary>The nearest join between two tiles.</summary>
        private static float ToEdge(float coordinate)
        {
            return Mathf.Round(coordinate / RoadCell) * RoadCell;
        }

        private static Street Down(
            string name,
            float x,
            float fromZ,
            float toZ,
            float width)
        {
            return new Street(
                name,
                false,
                ToLane(x) + MapMinX,
                ToEdge(fromZ) + MapMinZ,
                ToEdge(toZ) + MapMinZ,
                width);
        }

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


        /// <summary>
        /// Draws the street plan next to the drawing it is copied from.
        ///
        /// Lives here rather than in the capture tool because the arrays are
        /// here, and a plan drawn from anything other than the numbers the
        /// scene is built from is a second source of truth.
        /// </summary>
        /// <summary>
        /// The streets as rectangles, in the order they are written.
        /// </summary>
        private static Rect[] StreetAreas()
        {
            return StreetLanes().Select(lane => lane.Area).ToArray();
        }

        /// <summary>
        /// Each street as a rectangle plus the way it runs.
        ///
        /// The road tile has its kerbs, its centre line and its crossing
        /// stripes painted into its texture, so which way round it lies is the
        /// difference between a road and a grey smear.
        /// </summary>
        private static (Rect Area, bool Horizontal)[] StreetLanes()
        {
            var areas = new List<(Rect, bool)>();
            foreach (Street street in Streets)
            {
                float length = street.To - street.From;
                float half = street.Width * 0.5f;
                areas.Add((
                    street.Horizontal
                        ? new Rect(
                            street.From,
                            street.FixedCoordinate - half,
                            length,
                            street.Width)
                        : new Rect(
                            street.FixedCoordinate - half,
                            street.From,
                            street.Width,
                            length),
                    street.Horizontal));
            }

            return areas.ToArray();
        }

        /// <summary>
        /// The blocks, numbered north-west first.
        ///
        /// Both the scene and the blueprint call this rather than each finding
        /// blocks their own way. Two flood fills that disagree about ordering
        /// would put the police station in a different place from the one the
        /// plan says, and nothing would report it.
        /// </summary>
        private static Rect[] NumberedBlocks(out int[] areas)
        {
            Rect[] found = MapBlueprint.FindBlocks(
                MapMinX,
                MapMinZ,
                MapWidth,
                MapDepth,
                StreetAreas(),
                24f,
                out int[] sizes);

            int[] order = Enumerable
                .Range(0, found.Length)
                .OrderByDescending(index => Mathf.RoundToInt(found[index].yMax))
                .ThenBy(index => Mathf.RoundToInt(found[index].xMin))
                .ToArray();

            areas = order.Select(index => sizes[index]).ToArray();
            return order.Select(index => found[index]).ToArray();
        }

        [MenuItem("Paws & Loot/Sandbox/Capture Sandbox Blueprint")]
        public static void CaptureBlueprint()
        {
            var pieces = new List<MapBlueprint.Piece>();

            foreach (Street street in Streets)
            {
                float length = street.To - street.From;
                float half = street.Width * 0.5f;
                Rect area = street.Horizontal
                    ? new Rect(
                        street.From,
                        street.FixedCoordinate - half,
                        length,
                        street.Width)
                    : new Rect(
                        street.FixedCoordinate - half,
                        street.From,
                        street.Width,
                        length);
                pieces.Add(new MapBlueprint.Piece(
                    area,
                    MapBlueprint.RoadColour(street.Width < StreetWidth)));
            }

            // The ground the streets leave behind, numbered the same way the
            // scene numbers it.
            Rect[] blocks = NumberedBlocks(out int[] areas);
            var labels = new List<MapBlueprint.Label>();
            for (int rank = 0; rank < blocks.Length; rank++)
            {
                Rect block = blocks[rank];
                pieces.Add(new MapBlueprint.Piece(
                    block,
                    MapBlueprint.BlockColour(rank)));
                labels.Add(new MapBlueprint.Label(
                    (rank + 1).ToString(),
                    block.center));

                Debug.Log(
                    $"[BLOCK {rank + 1,2}] x {block.xMin,6:0} .. "
                    + $"{block.xMax,3:0}   z {block.yMin,4:0} .. "
                    + $"{block.yMax,3:0}   {block.width,3:0} x "
                    + $"{block.height,2:0} m   usable {areas[rank],4} m2");
            }

            foreach (Placement placement in LayOut(blocks))
            {
                pieces.Add(new MapBlueprint.Piece(
                    placement.Area(FootprintOf(placement.Fill)),
                    MapBlueprint.BuildingColour(
                        placement.Fill == Fill.Plaza)));
            }

            // Roads drawn last so the block tints do not cover them.
            var ordered = new List<MapBlueprint.Piece>();
            ordered.AddRange(pieces.Skip(Streets.Length));
            ordered.AddRange(pieces.Take(Streets.Length));
            ordered.AddRange(pieces
                .Skip(Streets.Length + blocks.Length));

            MapBlueprint.Write(
                "Logs/sandbox-blueprint.png",
                MapMinX,
                MapMinZ,
                MapWidth,
                MapDepth,
                ordered.ToArray(),
                labels.ToArray());
        }

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

            Rect[] blocks = NumberedBlocks(out _);
            int placed = BuildBlocks(buildings, sizes, blocks, out int houses);
            int dressing = BuildDressing(environment, sizes, blocks);

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
        /// <summary>
        /// Lays the streets exactly where the plan says.
        ///
        /// Each one is a single slab, sharing one material for now.
        ///
        /// Per-street texture tiling was tried first and made every road
        /// invisible: a material built with `new Material(...)` in an editor
        /// script lives in memory only, so saving the scene left each renderer
        /// pointing at nothing. Same family as the listener and the list that
        /// do not survive a save. Tiling needs real material assets, and that
        /// is worth doing once the street plan is settled rather than while it
        /// is being moved around.
        /// </summary>
        private static int BuildRoadNetwork(Transform parent)
        {
            Transform roads = Child("Roads", parent);
            Material source = Load<Material>($"{MaterialDirectory}/Road.mat");

            foreach (Street street in Streets)
            {
                float length = street.To - street.From;
                float middle = (street.From + street.To) * 0.5f;
                Vector3 centre = street.Horizontal
                    ? new Vector3(middle, 0.02f, street.FixedCoordinate)
                    : new Vector3(street.FixedCoordinate, 0.02f, middle);
                Vector3 size = street.Horizontal
                    ? new Vector3(length, 0.04f, street.Width)
                    : new Vector3(street.Width, 0.04f, length);

                // Nothing is drawn here. The road is the tile grid, and a
                // slab under it only ever covered it up.
            }

            return Streets.Length;
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
        /// <summary>
        /// What stands in each numbered block.
        ///
        /// Numbers are the ones on the blueprint, ordered north-west first.
        /// Written down rather than inferred from block size: which building
        /// goes where is a design decision, and the police station happening to
        /// be the biggest thing is not a reason to drop it in the biggest gap.
        /// </summary>
        private enum Fill
        {
            OneStorey,
            TwoStorey,
            Supermarket,
            PoliceStation,
            Jewellery,
            Bookstore,
            Plaza
        }

        /// <summary>
        /// A nudge for one building, in metres, applied after the even spacing.
        ///
        /// The layout spreads things down the middle of a block, which is right
        /// for a plain rectangle and wrong for block 4: the market lane bites a
        /// notch out of its south-east, so the two buildings ended up shoulder
        /// to shoulder on the west side with the north-east corner empty. Rather
        /// than teach the spacing about notches, the two that need it say so.
        /// </summary>
        private static readonly Dictionary<string, Vector2> Nudges = new()
        {
            // The supermarket goes to the corner the trees had.
            { "4:Supermarket", new Vector2(7f, 4f) },
            // And the house it was leaning on moves west.
            { "4:OneStorey", new Vector2(-5f, 0f) }
        };

        /// <summary>
        /// What stands where, keyed by a point inside the plot.
        ///
        /// It used to be keyed by the block's number, and the number is not a
        /// property of the map. Blocks are found by flooding the gaps between
        /// roads, so moving a street by two metres to sit on the tile grid
        /// split one plot in two and renumbered every plot after it — the
        /// police station, the square and the supermarket all found themselves
        /// assigned to somebody else's garden, and three of them to a plot too
        /// small to stand in.
        ///
        /// A point inside the plot survives that. The same mistake, and the
        /// same fix, as picking a landmark with FindFirstObjectByType.
        /// </summary>
        private static readonly (Vector2 Inside, Fill[] Contents)[]
            Assignments =
        {
            (new Vector2(-6f, 44f), new[] { Fill.TwoStorey }),
            (new Vector2(30f, 44f),
                new[] { Fill.TwoStorey, Fill.OneStorey }),
            (new Vector2(-16f, 20f),
                new[] { Fill.OneStorey, Fill.Supermarket }),
            (new Vector2(6f, 20f), new[] { Fill.PoliceStation }),
            (new Vector2(22f, 20f), new[] { Fill.Jewellery }),
            (new Vector2(42f, 20f),
                new[] { Fill.TwoStorey, Fill.OneStorey }),
            (new Vector2(-20f, -10f), new[] { Fill.OneStorey }),
            (new Vector2(10f, -2f), new[] { Fill.TwoStorey }),
            (new Vector2(42f, -2f), new[] { Fill.Plaza }),
            (new Vector2(22f, -14f),
                new[] { Fill.Bookstore, Fill.OneStorey, Fill.Supermarket })
        };

        /// <summary>
        /// Footprints in metres. Houses use the village's own 12 x 8 lot.
        /// </summary>
        private static Vector2 FootprintOf(Fill fill)
        {
            return fill switch
            {
                Fill.PoliceStation => new Vector2(12f, 12f),
                Fill.Bookstore => new Vector2(12f, 10f),
                Fill.Jewellery => new Vector2(10f, 8f),
                Fill.Supermarket => new Vector2(12f, 8f),
                Fill.Plaza => new Vector2(16f, 8f),
                _ => new Vector2(LotX, LotZ)
            };
        }

        private static string StemOf(Fill fill)
        {
            return fill switch
            {
                Fill.PoliceStation => "building_police_station",
                Fill.Supermarket => "building_supermarket",
                Fill.Bookstore => "building_bookstore",
                Fill.Jewellery => "building_jewelry",
                Fill.TwoStorey => "building_house_2f",
                Fill.OneStorey => "building_house_1f",
                Fill.Plaza => "env_fountain_plaza",
                _ => null
            };
        }

        /// <summary>
        /// Where one building ends up.
        /// </summary>
        private readonly struct Placement
        {
            public Placement(Fill fill, string label, Vector3 centre, float yaw)
            {
                Fill = fill;
                Label = label;
                Centre = centre;
                Yaw = yaw;
            }

            public Fill Fill { get; }
            public string Label { get; }
            public Vector3 Centre { get; }
            public float Yaw { get; }

            /// <summary>
            /// Ground covered once turned. A quarter turn swaps the footprint,
            /// and forgetting that draws a plan that disagrees with the town.
            /// </summary>
            public Rect Area(Vector2 footprint)
            {
                bool quarterTurned =
                    Mathf.Abs(Mathf.Sin(Yaw * Mathf.Deg2Rad)) > 0.5f;
                float sizeX = quarterTurned ? footprint.y : footprint.x;
                float sizeZ = quarterTurned ? footprint.x : footprint.y;
                return new Rect(
                    Centre.x - sizeX * 0.5f,
                    Centre.z - sizeZ * 0.5f,
                    sizeX,
                    sizeZ);
            }
        }

        /// <summary>
        /// Moves a building until it is standing on ground rather than road.
        ///
        /// Blocks are reported by their bounding box, and two of them are
        /// L-shaped: spacing buildings evenly across that box puts some of them
        /// in the notch, which is road. They were drawn sitting on the street
        /// and nothing objected, because nothing was asking.
        ///
        /// Searches outward from the ideal spot and takes the first place that
        /// fits, so a building moves as little as the shape allows. Refusing to
        /// place it at all is better than placing it in a road — a missing
        /// building is obvious and a building in the carriageway is not.
        /// </summary>
        private static bool TryFit(
            Vector2 footprint,
            Rect[] roads,
            List<Rect> taken,
            ref Vector3 centre)
        {
            const float Step = 1f;
            const float Reach = 10f;

            for (float radius = 0f; radius <= Reach; radius += Step)
            {
                for (float dx = -radius; dx <= radius; dx += Step)
                {
                    for (float dz = -radius; dz <= radius; dz += Step)
                    {
                        // Only the ring being tested, so nearer places are
                        // always tried first.
                        if (radius > 0f
                            && Mathf.Abs(dx) < radius
                            && Mathf.Abs(dz) < radius)
                        {
                            continue;
                        }

                        var candidate = new Vector3(
                            centre.x + dx,
                            centre.y,
                            centre.z + dz);
                        var area = new Rect(
                            candidate.x - footprint.x * 0.5f,
                            candidate.z - footprint.y * 0.5f,
                            footprint.x,
                            footprint.y);

                        if (area.xMin < MapMinX
                            || area.yMin < MapMinZ
                            || area.xMax > MapMaxX
                            || area.yMax > MapMaxZ)
                        {
                            continue;
                        }

                        bool blocked = false;
                        foreach (Rect road in roads)
                        {
                            if (area.Overlaps(road))
                            {
                                blocked = true;
                                break;
                            }
                        }

                        // And not on top of something already placed. Spacing
                        // items evenly across a block says nothing about
                        // whether they fit: two twelve-metre buildings nine and
                        // a half metres apart overlap, and the supermarket was
                        // standing inside a house because of it.
                        if (!blocked)
                        {
                            foreach (Rect other in taken)
                            {
                                if (area.Overlaps(other))
                                {
                                    blocked = true;
                                    break;
                                }
                            }
                        }

                        if (!blocked)
                        {
                            centre = candidate;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Works out where everything goes, without building any of it.
        ///
        /// Shared by the scene and the plan. Two copies of this arithmetic would
        /// drift, and a plan that draws the police station somewhere the town
        /// does not put it is worse than no plan.
        /// </summary>
        private static List<Placement> LayOut(Rect[] blocks)
        {
            const float Verge = 2.5f;
            var placements = new List<Placement>();
            var taken = new List<Rect>();
            Rect[] roads = RoadCellAreas();

            foreach ((Vector2 inside, Fill[] contents) in Assignments)
            {
                int number = System.Array.FindIndex(
                    blocks,
                    candidate => candidate.Contains(inside)) + 1;
                if (number < 1)
                {
                    Debug.LogWarning(
                        $"[SANDBOX] No plot contains {inside}, so "
                        + $"{string.Join(", ", contents)} has nowhere to go. "
                        + "The street plan moved out from under the "
                        + "assignments.");
                    continue;
                }

                Rect block = blocks[number - 1];
                bool tall = block.height > block.width;

                for (int index = 0; index < contents.Length; index++)
                {
                    Fill fill = contents[index];
                    float run = tall ? block.height : block.width;
                    float share = (run - Verge * 2f) / contents.Length;
                    float along = (tall ? block.yMin : block.xMin)
                        + Verge
                        + share * (index + 0.5f);
                    float across = tall
                        ? block.center.x
                        : block.center.y;

                    // South-facing unless that will not fit.
                    //
                    // Turning a building sideways is worth doing when the block
                    // is long and thin, and costs the entrance facing the
                    // street. So it is tried second: only when the buildings
                    // laid out the natural way would not go in. Turning by
                    // default put two twelve-metre-deep houses in a
                    // twenty-four-metre block and one of them had nowhere to
                    // stand.
                    Vector2 shape = FootprintOf(fill);
                    float alongIfSouth = tall ? shape.y : shape.x;
                    float alongIfTurned = tall ? shape.x : shape.y;
                    float needed = contents.Length * (alongIfSouth + 3f);
                    float yaw =
                        needed <= run || alongIfTurned >= alongIfSouth
                            ? 180f
                            : 270f;
                    Vector3 centre = tall
                        ? new Vector3(across, 0f, along)
                        : new Vector3(along, 0f, across);

                    // Turned footprints are what has to clear the road, not the
                    // authored one.
                    Vector2 turned = Mathf.Approximately(yaw, 270f)
                        ? new Vector2(shape.y, shape.x)
                        : shape;

                    if (Nudges.TryGetValue(
                            $"{number}:{fill}",
                            out Vector2 nudge))
                    {
                        centre += new Vector3(nudge.x, 0f, nudge.y);
                    }

                    if (!TryFit(turned, roads, taken, ref centre))
                    {
                        Debug.LogWarning(
                            $"[SANDBOX] Block {number} has nowhere clear for "
                            + $"{fill}. It was left out rather than dropped in "
                            + "a road.");
                        continue;
                    }

                    taken.Add(new Rect(
                        centre.x - turned.x * 0.5f,
                        centre.z - turned.y * 0.5f,
                        turned.x,
                        turned.y));
                    placements.Add(new Placement(
                        fill,
                        $"Block {number} {fill}",
                        centre,
                        yaw));
                }
            }

            return placements;
        }

        /// <summary>
        /// Fills each block with what it was assigned, evenly spaced along
        /// whichever way the block runs longer.
        ///
        /// Two buildings in a block that is deeper than it is wide are laid out
        /// down it and turned a quarter, so they read as a pair of long
        /// buildings rather than two squares stacked. Everything else is spread
        /// along the block with the same gap between neighbours, which is what
        /// stops a wide block reading as one building beside dead ground.
        ///
        /// Houses face south. The model's front is at +Z, so they are turned
        /// 180 degrees: the front door meets the street below and the back door
        /// opens onto whatever is behind.
        /// </summary>
        private static int BuildBlocks(
            Transform parent,
            Measurements sizes,
            Rect[] blocks,
            out int houses)
        {
            int specials = 0;
            houses = 0;

            foreach (Placement placement in LayOut(blocks))
            {
                string stem = StemOf(placement.Fill);
                if (stem == null
                    || !PlaceBuilding(
                        parent,
                        stem,
                        placement.Centre,
                        placement.Yaw,
                        FootprintOf(placement.Fill),
                        placement.Label,
                        sizes))
                {
                    Vector2 footprint = FootprintOf(placement.Fill);
                    GameObject box = CreateBox(
                        parent,
                        placement.Label,
                        placement.Centre + Vector3.up * 3f,
                        new Vector3(footprint.x, 6f, footprint.y),
                        Load<Material>(
                            $"{MaterialDirectory}/Sandbox_Stone.mat"));
                    box.transform.rotation =
                        Quaternion.Euler(0f, placement.Yaw, 0f);
                }

                if (placement.Fill == Fill.OneStorey
                    || placement.Fill == Fill.TwoStorey)
                {
                    houses++;
                }
                else
                {
                    specials++;
                }
            }

            return specials;
        }

        /// <summary>
        /// A flat square with a fountain in the middle, rather than a building.
        /// </summary>
        private static void BuildPlaza(
            Transform parent,
            Placement placement)
        {
            Vector2 footprint = FootprintOf(Fill.Plaza);
            Slab(
                parent,
                placement.Label,
                new Vector3(placement.Centre.x, 0.03f, placement.Centre.z),
                new Vector3(footprint.x, 0.06f, footprint.y),
                Load<Material>($"{MaterialDirectory}/Plaza.mat"),
                false);

            CreateBox(
                parent,
                placement.Label + " Fountain",
                new Vector3(placement.Centre.x, 0.6f, placement.Centre.z),
                new Vector3(3f, 1.2f, 3f),
                Load<Material>($"{MaterialDirectory}/Sandbox_Stone.mat"));
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 centre,
            Vector3 size,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = centre;
            box.transform.localScale = size;
            if (material != null)
            {
                box.GetComponent<Renderer>().sharedMaterial = material;
            }

            return box;
        }

        /// <summary>
        /// Fits a model to the footprint it was given.
        ///
        /// Everything used to be squeezed into the house lot whatever it was,
        /// so the police station came out the size of a bungalow and the plan
        /// and the town disagreed about how much room each thing takes. The
        /// footprint the layout reserved is the size it should be.
        /// </summary>
        private static bool PlaceBuilding(
            Transform parent,
            string stem,
            Vector3 centre,
            float yaw,
            Vector2 footprint,
            string name,
            Measurements sizes)
        {
            Vector3 size = sizes.Of(stem);
            float scale = Mathf.Min(
                footprint.x / Mathf.Max(0.01f, size.x),
                footprint.y / Mathf.Max(0.01f, size.z));

            // Looked for in both folders. The plaza is a fountain, filed with
            // the environment, and asking only the buildings folder for it
            // returned nothing: the town got a grey box where its square
            // should be and no warning that anything was wrong.
            GameObject instance = Instantiate(
                stem,
                DirectoryOf(stem),
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

            // Local space, because the transform is scaled and Unity scales
            // the collider with it. Handing it the world size meant the box was
            // scaled twice: the buildings were wrapped in colliders several
            // times their own size, and walking near one pushed the player out
            // of the world through the ground.
            BoxCollider box = instance.AddComponent<BoxCollider>();
            Vector3 local = bounds.size / Mathf.Max(0.0001f, scale);
            box.center = new Vector3(0f, local.y * 0.5f, 0f);
            box.size = local;

            // Nothing in this town moves, so all of it can be batched. The
            // buildings were the only thing that never said so.
            MakeBatchable(instance);
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
        /// <summary>
        /// Lays the road surface, the grass and the things that stand beside a
        /// street.
        ///
        /// The roads were flat slabs with one stretched texture. The model is a
        /// tile a metre across meant to be repeated, so it is repeated: a four
        /// by sixty-six metre street is a grid of tiles, not one picture blown
        /// up sixty-six times.
        ///
        /// Lamps and trees are spaced along the streets rather than scattered.
        /// A lamp every fifteen metres reads as a town; a lamp wherever there
        /// happened to be room reads as a field.
        /// </summary>
        /// <summary>
        /// Lays the road surface, the lawns and the things that stand beside a
        /// street.
        /// </summary>
        private static int BuildDressing(
            Transform parent,
            Measurements sizes,
            Rect[] blocks)
        {
            Transform surface = Child("Surface", parent);
            Transform dressing = Child("Dressing", parent);
            int count = LayRoads(surface);

            count += LayGrass(
                surface,
                blocks
                    .Select(block => new Rect(
                        block.xMin + 0.5f,
                        block.yMin + 0.5f,
                        block.width - 1f,
                        block.height - 1f))
                    .ToArray());

            Rect[] built = LayOut(blocks)
                .Select(placement =>
                    placement.Area(FootprintOf(placement.Fill)))
                .ToArray();

            count += LineStreets(
                dressing,
                sizes,
                "env_street_lamp",
                18f,
                1.4f,
                3.2f,
                built);
            count += LineStreets(
                dressing,
                sizes,
                "env_tree",
                16f,
                2.8f,
                4.5f,
                built);

            return count;
        }

        /// <summary>
        /// Marks an object and everything under it as batchable.
        ///
        /// The flag is per object, not per hierarchy, and these models arrive
        /// as hundreds of parts under one root. Setting it on the root alone
        /// left two thousand of the two thousand three hundred renderers in the
        /// scene outside batching, which is most of the reason the town was
        /// slow even after its triangle count came down.
        /// </summary>
        private static void MakeBatchable(GameObject root)
        {
            foreach (Transform part in
                root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(
                    part.gameObject,
                    StaticEditorFlags.BatchingStatic);
            }
        }

        /// <summary>
        /// Which folder a model was filed under.
        /// </summary>
        private static string DirectoryOf(string stem)
        {
            return stem.StartsWith("env_")
                ? EnvironmentDirectory
                : BuildingDirectory;
        }

        /// <summary>
        /// Which way a road tile opens, before it is turned.
        /// </summary>
        [System.Flags]
        private enum Ways
        {
            None = 0,
            North = 1,
            East = 2,
            South = 4,
            West = 8
        }

        /// <summary>
        /// Every road piece and the sides its carriageway runs out of.
        ///
        /// Measured, not guessed: `Report Road Pieces` photographs each one
        /// from above and `Capture Model Sheet` puts them side by side. The
        /// file names do not say — "road tile" is the crossing, "road section"
        /// is the plain straight, "street intersection" is the T — and laying
        /// them by name put a pedestrian crossing on every metre of every
        /// street in the town.
        /// </summary>
        private static readonly (string Stem, Ways Open)[] RoadPieces =
        {
            ("env_road_crossroad",
                Ways.North | Ways.East | Ways.South | Ways.West),
            ("env_road_intersection", Ways.North | Ways.East | Ways.West),
            ("env_road_corner", Ways.West | Ways.North),
            ("env_road_section", Ways.North | Ways.South),
            ("env_road_curve", Ways.North)
        };

        private const string CrossingStem = "env_road_tile";

        /// <summary>
        /// Lays the roads as a grid of modular tiles.
        ///
        /// Each cell asks its four neighbours whether they are road too, and
        /// the answer picks the piece and the quarter turn: four neighbours is
        /// a crossroads, three a T with its closed side facing the gap, two
        /// opposite a straight, two adjacent a corner, one a dead end.
        ///
        /// Crossings are not part of that. They are laid afterwards, one to a
        /// street, on a straight cell near where the street begins — a town
        /// where every tile is a crossing is a car park.
        /// </summary>
        private static int LayRoads(Transform parent)
        {
            ReportCrowdedStreets();

            Dictionary<Vector2Int, List<int>> cells = RoadCellStreets();
            HashSet<Vector2Int> crossings = Crossings(cells);
            var tally = new Dictionary<string, int>();
            int laid = 0;

            foreach (Vector2Int cell in cells.Keys)
            {
                Ways open = OpeningsAt(cell, cells);
                if (!Choose(open, out string stem, out float yaw))
                {
                    continue;
                }

                if (crossings.Contains(cell))
                {
                    stem = CrossingStem;
                }

                if (!Lay(parent, stem, cell, yaw, 0.02f))
                {
                    continue;
                }

                tally.TryGetValue(stem, out int running);
                tally[stem] = running + 1;
                laid++;
            }

            // Printed because a wrong junction is not an error anywhere — it
            // just looks wrong. A pile of dead ends means the streets are not
            // meeting; none at all means nothing is being capped that should
            // be.
            Debug.Log(
                "[SANDBOX] Road tiles: "
                + string.Join(
                    ", ",
                    tally.OrderBy(entry => entry.Key)
                        .Select(entry => $"{entry.Key} {entry.Value}")));
            return laid;
        }

        /// <summary>
        /// Which sides of a cell the road carries on through.
        ///
        /// A side is open when the cell next door is road and the direction
        /// runs along some street that one of the two cells belongs to.
        ///
        /// Asking only whether the neighbour is road cannot tell a junction
        /// from two roads running side by side, and this plan has such pairs:
        /// every cell of those stretches had three road neighbours, so every
        /// cell was given a T-junction and the result was a wide pale ribbon of
        /// mismatched pieces.
        ///
        /// Asking whether the two cells share a street is too strict the other
        /// way. A side street that stops against a main road often stops one
        /// tile short of it — the streets meet without ever occupying the same
        /// tile — and then neither side would open, so the side street got a
        /// dead end painted across it and the main road ran past behind. That
        /// is what closed off the road between the station and the jeweller.
        ///
        /// The direction is what matters. Running south out of a north-south
        /// street is the road carrying on; running north out of an east-west
        /// one is the road jumping across to its neighbour, and it should not.
        /// </summary>
        private static Ways OpeningsAt(
            Vector2Int cell,
            Dictionary<Vector2Int, List<int>> cells)
        {
            Ways open = Ways.None;

            if (Continues(cell, Vector2Int.up, cells)) open |= Ways.North;
            if (Continues(cell, Vector2Int.right, cells)) open |= Ways.East;
            if (Continues(cell, Vector2Int.down, cells)) open |= Ways.South;
            if (Continues(cell, Vector2Int.left, cells)) open |= Ways.West;

            return open;
        }

        private static bool Continues(
            Vector2Int cell,
            Vector2Int step,
            Dictionary<Vector2Int, List<int>> cells)
        {
            Vector2Int neighbour = cell + step;
            if (!cells.TryGetValue(neighbour, out List<int> theirs))
            {
                return false;
            }

            bool alongX = step.x != 0;
            foreach (int street in cells[cell])
            {
                if (Streets[street].Horizontal == alongX)
                {
                    return true;
                }
            }

            foreach (int street in theirs)
            {
                if (Streets[street].Horizontal == alongX)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The piece and quarter turn that opens exactly the given sides.
        /// </summary>
        private static bool Choose(Ways open, out string stem, out float yaw)
        {
            foreach ((string candidate, Ways sides) in RoadPieces)
            {
                for (int quarter = 0; quarter < 4; quarter++)
                {
                    if (Turn(sides, quarter) != open)
                    {
                        continue;
                    }

                    stem = candidate;
                    yaw = quarter * 90f;
                    return true;
                }
            }

            // An isolated cell has nothing to join, so it gets a plain piece
            // rather than nothing at all.
            stem = "env_road_section";
            yaw = 0f;
            return open == Ways.None;
        }

        /// <summary>
        /// The sides a piece opens after a quarter turn clockwise.
        ///
        /// A yaw of ninety degrees carries north to east, so the flags move the
        /// same way round.
        /// </summary>
        private static Ways Turn(Ways sides, int quarters)
        {
            Ways turned = Ways.None;
            var order = new[] { Ways.North, Ways.East, Ways.South, Ways.West };
            for (int index = 0; index < 4; index++)
            {
                if ((sides & order[index]) != 0)
                {
                    turned |= order[(index + quarters) % 4];
                }
            }

            return turned;
        }

        /// <summary>
        /// One crossing per street, on a cell where the road runs straight.
        ///
        /// Chosen a quarter of the way along rather than at the end, so it
        /// falls where somebody would actually walk across rather than in the
        /// middle of a junction.
        /// </summary>
        private static HashSet<Vector2Int> Crossings(
            Dictionary<Vector2Int, List<int>> cells)
        {
            var chosen = new HashSet<Vector2Int>();
            foreach (Street street in Streets)
            {
                float length = street.To - street.From;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    float along = street.From + length * (0.25f + attempt * 0.1f);
                    Vector3 at = street.Horizontal
                        ? new Vector3(along, 0f, street.FixedCoordinate)
                        : new Vector3(street.FixedCoordinate, 0f, along);
                    Vector2Int cell = CellAt(at.x, at.z);

                    if (!cells.ContainsKey(cell))
                    {
                        continue;
                    }

                    Ways open = OpeningsAt(cell, cells);
                    bool straight =
                        open == (Ways.North | Ways.South)
                        || open == (Ways.East | Ways.West);

                    if (straight && chosen.Add(cell))
                    {
                        break;
                    }
                }
            }

            return chosen;
        }

        private static Vector2Int CellAt(float x, float z)
        {
            return new Vector2Int(
                Mathf.FloorToInt((x - MapMinX) / RoadCell),
                Mathf.FloorToInt((z - MapMinZ) / RoadCell));
        }

        private static Vector3 CentreOf(Vector2Int cell)
        {
            return new Vector3(
                MapMinX + (cell.x + 0.5f) * RoadCell,
                0f,
                MapMinZ + (cell.y + 0.5f) * RoadCell);
        }

        /// <summary>
        /// Every lattice cell whose middle falls on a street, and which streets
        /// put it there.
        ///
        /// The cells are what the town is actually built from, so this is also
        /// what the buildings are kept off — asking the nominal rectangles
        /// instead would let a house stand on a tile that the grid rounded into
        /// the road.
        /// </summary>
        private static Dictionary<Vector2Int, List<int>> RoadCellStreets()
        {
            var cells = new Dictionary<Vector2Int, List<int>>();
            Rect[] areas = StreetAreas();

            for (int street = 0; street < areas.Length; street++)
            {
                Rect area = areas[street];
                Vector2Int from = CellAt(area.xMin, area.yMin);
                Vector2Int to = CellAt(area.xMax, area.yMax);
                for (int x = from.x; x <= to.x; x++)
                {
                    for (int z = from.y; z <= to.y; z++)
                    {
                        var cell = new Vector2Int(x, z);
                        Vector3 centre = CentreOf(cell);
                        if (!area.Contains(new Vector2(centre.x, centre.z)))
                        {
                            continue;
                        }

                        if (!cells.TryGetValue(cell, out List<int> owners))
                        {
                            owners = new List<int>();
                            cells[cell] = owners;
                        }

                        owners.Add(street);
                    }
                }
            }

            return cells;
        }

        private static HashSet<Vector2Int> RoadCells()
        {
            return new HashSet<Vector2Int>(RoadCellStreets().Keys);
        }

        private static Rect[] RoadCellAreas()
        {
            return RoadCells()
                .Select(cell =>
                {
                    Vector3 centre = CentreOf(cell);
                    return new Rect(
                        centre.x - RoadCell * 0.5f,
                        centre.z - RoadCell * 0.5f,
                        RoadCell,
                        RoadCell);
                })
                .ToArray();
        }

        private static bool Lay(
            Transform parent,
            string stem,
            Vector2Int cell,
            float yaw,
            float height)
        {
            Mesh face = FlatTileLibrary.TileFor(
                stem,
                EnvironmentDirectory,
                out Material paint);
            if (face == null || paint == null)
            {
                return false;
            }

            var tile = new GameObject($"{stem} {cell.x}_{cell.y}");
            tile.transform.SetParent(parent, false);
            Vector3 centre = CentreOf(cell);
            tile.transform.SetPositionAndRotation(
                new Vector3(centre.x, height, centre.z),
                Quaternion.Euler(0f, yaw, 0f));
            tile.transform.localScale =
                new Vector3(RoadCell, 1f, RoadCell);
            tile.AddComponent<MeshFilter>().sharedMesh = face;

            var renderer = tile.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paint;

            // Ground cannot shadow itself and there is nothing under it.
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            MakeBatchable(tile);
            return true;
        }

        /// <summary>
        /// Fills the blocks with lawn, avoiding the roads.
        /// </summary>
        private static int LayGrass(Transform parent, Rect[] blocks)
        {
            // Pulled in from the edge. The tile has a bevelled rim in a
            // different colour, and repeating it across a lawn drew a tan grid
            // over the whole town.
            Mesh face = FlatTileLibrary.TileFor(
                "env_grass_tile",
                EnvironmentDirectory,
                out Material paint,
                0.18f);
            if (face == null || paint == null)
            {
                return 0;
            }

            HashSet<Vector2Int> roads = RoadCells();
            var lawn = new HashSet<Vector2Int>();
            foreach (Rect block in blocks)
            {
                Vector2Int from = CellAt(block.xMin, block.yMin);
                Vector2Int to = CellAt(block.xMax, block.yMax);
                for (int x = from.x; x <= to.x; x++)
                {
                    for (int z = from.y; z <= to.y; z++)
                    {
                        var cell = new Vector2Int(x, z);
                        if (roads.Contains(cell))
                        {
                            continue;
                        }

                        Vector3 centre = CentreOf(cell);
                        if (block.Contains(new Vector2(centre.x, centre.z)))
                        {
                            lawn.Add(cell);
                        }
                    }
                }
            }

            int laid = 0;
            foreach (Vector2Int cell in lawn)
            {
                var tile = new GameObject($"env_grass_tile {cell.x}_{cell.y}");
                tile.transform.SetParent(parent, false);
                Vector3 centre = CentreOf(cell);
                tile.transform.position =
                    new Vector3(centre.x, 0.01f, centre.z);
                tile.transform.localScale =
                    new Vector3(RoadCell, 1f, RoadCell);
                tile.AddComponent<MeshFilter>().sharedMesh = face;

                var renderer = tile.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = paint;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                MakeBatchable(tile);
                laid++;
            }

            return laid;
        }

        /// <summary>
        /// Puts one model down each side of every street at a fixed spacing,
        /// set back from the kerb.
        ///
        /// Skips anything that would land on another street, which is what
        /// keeps lamps out of junctions.
        /// </summary>
        private static int LineStreets(
            Transform parent,
            Measurements sizes,
            string stem,
            float spacing,
            float setback,
            float targetHeight,
            Rect[] buildings)
        {
            Vector3 native = sizes.Of(stem);
            if (native.y <= 0.01f)
            {
                return 0;
            }

            float scale = targetHeight / native.y;
            Rect[] roads = RoadCellAreas();
            int placed = 0;

            foreach (Street street in Streets)
            {
                float length = street.To - street.From;
                int slots = Mathf.FloorToInt(length / spacing);
                if (slots < 1)
                {
                    continue;
                }

                float offset = street.Width * 0.5f + setback;
                for (int slot = 1; slot <= slots; slot++)
                {
                    float along = street.From
                        + length * slot / (slots + 1f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 at = street.Horizontal
                            ? new Vector3(
                                along,
                                0f,
                                street.FixedCoordinate + offset * side)
                            : new Vector3(
                                street.FixedCoordinate + offset * side,
                                0f,
                                along);

                        var footing = new Rect(
                            at.x - 0.8f,
                            at.z - 0.8f,
                            1.6f,
                            1.6f);
                        // Kept clear of the buildings as well as the roads. A
                        // tree at the shop door hides the shop, which is the one
                        // thing on the block anybody is looking for.
                        var elbow = new Rect(
                            at.x - 3.5f,
                            at.z - 3.5f,
                            7f,
                            7f);
                        if (at.x < MapMinX + 1f
                            || at.z < MapMinZ + 1f
                            || at.x > MapMaxX - 1f
                            || at.z > MapMaxZ - 1f
                            || Overlaps(footing, roads)
                            || Overlaps(elbow, buildings))
                        {
                            continue;
                        }

                        GameObject piece = Instantiate(
                            stem,
                            EnvironmentDirectory,
                            parent,
                            at,
                            Quaternion.Euler(0f, slot * 37f % 360f, 0f),
                            Vector3.one * scale,
                            $"{stem} {street.Name} {slot}{side}");
                        if (piece == null)
                        {
                            return placed;
                        }

                        // Scenery, not obstacles. Their own colliders are
                        // wrongly sized for the same reason the buildings' were,
                        // and a lamp post that shoves the player is worse than
                        // one you can walk through.
                        foreach (Collider collider in
                            piece.GetComponentsInChildren<Collider>(true))
                        {
                            Object.DestroyImmediate(collider);
                        }

                        // Dense scans, all of them. Their shadows cost a
                        // second pass over every one of those triangles and buy
                        // very little at this size.
                        foreach (Renderer renderer in
                            piece.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.shadowCastingMode =
                                UnityEngine.Rendering.ShadowCastingMode.Off;
                        }

                        MakeBatchable(piece);
                        placed++;
                    }
                }
            }

            return placed;
        }

        private static bool Overlaps(Rect area, Rect[] others)
        {
            foreach (Rect other in others)
            {
                if (area.Overlaps(other))
                {
                    return true;
                }
            }

            return false;
        }

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
