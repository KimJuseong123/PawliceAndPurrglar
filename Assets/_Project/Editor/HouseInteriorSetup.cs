using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Animation;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the insides of the houses, away from the town.
    ///
    /// The rooms are the house model itself, enlarged. They used to be a greybox box
    /// with four blocks in it, built from guessed proportions, until the model was
    /// measured: <c>building_house_1f_with_interior</c> has 335 renderers against
    /// the roofed variant's 216, and the 111 extra are a furnished interior —
    /// bathroom, bedroom, kitchen, living room, dining room, behind their own
    /// partition walls. Hand-building a worse room beside a better one that already
    /// existed was the wrong trade.
    ///
    /// Enlarged rather than entered at model scale. A house is 7.6 m across inside,
    /// which is four character-widths and too small to chase anybody through. The
    /// alternative — shrinking the characters on the way in — would have re-scaled
    /// the meaning of every tuned distance at once: the 1.75 m arrest range would
    /// cover half a room, a 12 m throw would cross the whole house, and the torch
    /// would light all of it. Growing the room leaves every rule alone.
    ///
    /// Every house gets one, roofed or not. The roof only decides whether you can
    /// see in from the street; it says nothing about whether there is an inside, and
    /// a town where half the houses are solid is a town where the thief learns which
    /// half to run to.
    ///
    /// Not a separate scene. Scene-placed <c>NetworkObject</c>s do not survive a
    /// scene load (ISSUE-016), so an interior scene would mean rebuilding the
    /// session layer; a room that is simply somewhere else in the same scene needs
    /// none of that, and the two players being genuinely far apart in world space is
    /// what stops the officer arresting through a wall.
    /// </summary>
    internal static class HouseInteriorSetup
    {
        /// <summary>
        /// Which interior model stands behind each kind of building.
        ///
        /// The three shops have rooms of their own — a jeweller's cases, a
        /// library's shelves, a grocer's aisles — and the houses share the two
        /// most recent general interiors.
        ///
        /// Which two is settled by triangle count rather than by file date.
        /// Every interior in the folder was unpacked in the same second, so the
        /// dates say only when somebody ran unzip; the counts say which
        /// pipeline made them. `house01` and the jeweller arrive at forty
        /// thousand triangles and the rest at nine hundred and fifty thousand,
        /// which is the generator's own signature and therefore the recent
        /// pair.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, string> InteriorForKind = new()
        {
            { "Supermarket", "interior_supermarket" },
            { "Bookstore", "interior_bookstore" },
            { "Jewellery", "interior_jewelry" },
            { "OneStorey", "interior_house02" },
            { "TwoStorey", "interior_house01" }
        };

        /// <summary>
        /// Where a caught thief waits out the sentence.
        ///
        /// Its own room rather than a corner of the police station, so the
        /// sentence is somewhere with nothing in it and no way out until the
        /// clock says so.
        /// </summary>
        internal const string JailStem = "interior_jail";

        /// <summary>
        /// How wide the cell is across the floor, and how high its walls stand.
        ///
        /// Twelve by three. Wide enough to walk the ten seconds off rather than
        /// stand in one spot, and low enough that the camera clears the top and
        /// the player can see they are somewhere rather than down a shaft.
        /// </summary>
        private const float JailFloor = 12f;

        private const float JailWall = 3f;

        /// <summary>
        /// Which wall a room's front door is in, when the probe gets it wrong.
        ///
        /// The probe reads the widest gap in each wall, and a gap is not always
        /// a door: a glazed shopfront fills the band it looks at, while a sign
        /// rail or a run of low shelving leaves it empty. It put the
        /// supermarket's door a quarter turn out and the house's a half turn
        /// out, both of which were found by walking into them.
        ///
        /// Named here rather than made cleverer. Which wall a door is in is a
        /// fact about a model somebody drew, there are five of them, and a
        /// person looking at the room settles it in a second where a heuristic
        /// keeps being nearly right.
        ///
        /// Directions are the room's own, laid out unrotated: +Z north, -Z
        /// south, +X east, -X west.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, Vector3> DoorWallForModel = new()
        {
            { "interior_bookstore", Vector3.forward },
            { "interior_house01", Vector3.forward },
            { "interior_house02", Vector3.forward },
            { "interior_house03", Vector3.forward },
            { "interior_jewelry", Vector3.forward },
            { "interior_supermarket", Vector3.forward }
        };

        /// <summary>
        /// Builds the cell a caught thief waits in, and gives back the spot
        /// they stand on.
        ///
        /// A room rather than a coordinate outside the police station. Being
        /// arrested used to mean standing in the street beside the building
        /// with the controls off, which reads as the game having frozen. Ten
        /// seconds somewhere that plainly is a cell reads as a sentence.
        ///
        /// Built like the other rooms and then sealed: same model handling,
        /// same collision copy, same floor measurement, but the barrier has no
        /// gap in it. There is no door out of jail — the clock is the door.
        /// </summary>
        internal static Transform BuildJail(
            Transform parent,
            System.Func<string, Transform, Transform> childFactory,
            out int interiorId)
        {
            interiorId = JailInteriorId;

            Physics.SyncTransforms();
            Transform room = childFactory("Jail Interior", parent);

            // Well past the last house room, on the same grid.
            var centre = new Vector3(
                RowStartX,
                0f,
                RowZ - (Columns + 1) * SpacingZ);
            room.position = centre;

            Vector3 native = PlaceholderModelLibrary
                .TryInstantiateBuildingSized(
                    JailStem,
                    room,
                    centre,
                    0f,
                    0f,
                    1f);
            if (native.y <= 0.001f)
            {
                Debug.LogError(
                    $"[MAP-008] '{JailStem}' would not load, so there is no "
                    + "cell. A caught thief will be left in the street.");
                return null;
            }

            // Scaled unevenly, unlike every other room.
            //
            // The cell model is a tall narrow box — half again as high as it is
            // wide, where the other interiors are a sixth. Fitting it to the
            // common wall height gave a floor six metres across under a wall
            // four and a half high, which is a well rather than a room: no
            // space to walk and nothing visible over the top.
            //
            // The floor and the walls are therefore given separately. A plain
            // rectangular cell takes that without looking stretched.
            var lift = new Vector3(
                JailFloor / Mathf.Max(0.01f, native.x),
                JailWall / Mathf.Max(0.01f, native.y),
                JailFloor / Mathf.Max(0.01f, native.z));
            foreach (Transform part in room)
            {
                Vector3 scale = part.localScale;
                part.localScale = new Vector3(
                    scale.x * lift.x,
                    scale.y * lift.y,
                    scale.z * lift.z);
                Vector3 offset = part.position - centre;
                part.position = centre + new Vector3(
                    offset.x * lift.x,
                    offset.y * lift.y,
                    offset.z * lift.z);
            }

            Renderer[] parts = room.GetComponentsInChildren<Renderer>(true);
            if (!TryMeasure(
                    parts,
                    out float floorTop,
                    out Bounds inner,
                    out _,
                    out _))
            {
                Debug.LogError("[MAP-008] The cell has no measurable inside.");
                return null;
            }

            Transform colliders = childFactory("Jail Colliders", room);
            colliders.position = centre;
            AddSolidColliders(
                JailStem,
                colliders,
                parts.Length > 0 ? parts[0].transform : null,
                out Transform collisionRoot);
            floorTop = MeasureFloorTop(collisionRoot, inner, floorTop);

            // Sealed on all four sides. Vector3.zero is not one of the four
            // directions the barrier tests against, so no wall gets a gap.
            BuildPerimeter(colliders, inner, floorTop, Vector3.zero);

            var interior = room.gameObject.AddComponent<HouseInterior>();
            Transform stand = childFactory("Jail Stand", room);
            stand.position =
                new Vector3(inner.center.x, floorTop, inner.center.z);
            interior.Configure(
                JailInteriorId,
                stand,
                stand,
                stand,
                stand,
                new Vector2(
                    Mathf.Max(1f, inner.extents.x - 1.2f),
                    Mathf.Max(1f, inner.extents.z - 1.2f)),
                floorTop);

            Debug.Log(
                $"[MAP-008] Cell built at {stand.position}, "
                + $"{inner.size.x:0.0} x {inner.size.z:0.0} m inside.");
            return stand;
        }

        /// <summary>
        /// The cell's interior id. Far from the houses' numbering so nothing
        /// counts it as one of them.
        /// </summary>
        internal const int JailInteriorId =
            PawsAndLoot.Gameplay.Interiors.HouseInterior.JailId;

        /// <summary>
        /// Where a player is put down, as a fraction of the plan view.
        ///
        /// Read off `Capture Interior Plans` with the marks drawn on it, so the
        /// number here and the spot in the picture are the same thing. `u` runs
        /// left to right, `v` runs top to bottom, both across the square the
        /// plan is framed to — which is the room's larger side plus a margin,
        /// the same framing the picture uses.
        ///
        /// Marked rather than searched. The clear-floor search finds somewhere
        /// a body fits, which is not the same as somewhere it should be: it put
        /// people down facing a bedroom wall three metres from the door they
        /// came in by. A person looking at the plan places it once.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, Vector2> SpawnInPlan = new()
        {
            { "interior_bookstore", new Vector2(0.528f, 0.207f) },
            { "interior_house02", new Vector2(0.552f, 0.292f) },
            { "interior_jewelry", new Vector2(0.463f, 0.169f) },
            { "interior_supermarket", new Vector2(0.448f, 0.178f) }
        };

        /// <summary>
        /// Where treasure can appear in the jeweller's, marked on the plan.
        ///
        /// Seven places and four pieces: the room is never laid out the same way
        /// twice, so neither player can learn it. The officer cannot stand on the
        /// gold bar's spot because there is no gold bar's spot, and the thief
        /// cannot run a memorised route.
        ///
        /// Read straight off the picture, in the same fractions
        /// <see cref="FromPlan"/> takes — each one sits on a cabinet or a shelf
        /// against a wall, which is where a jeweller would put them and where
        /// they are visible from the door.
        ///
        /// Seven rather than four so the draw has something to choose between,
        /// and not many more: every extra one dilutes the room until the pieces
        /// are scattered too thinly to be worth searching.
        /// </summary>
        private static readonly Vector2[] JewelryLootSpotsInPlan =
        {
            new(0.215f, 0.141f),
            new(0.797f, 0.146f),
            new(0.166f, 0.348f),
            new(0.172f, 0.635f),
            new(0.141f, 0.811f),
            new(0.797f, 0.713f),
            new(0.566f, 0.902f)
        };

        /// <summary>
        /// The bookshop's shelves. Nine places: the room is deep and the tall
        /// shelving blocks sight down it, so treasure spread along the aisles
        /// makes searching it a walk rather than a glance.
        /// </summary>
        private static readonly Vector2[] BookstoreLootSpotsInPlan =
        {
            new(0.162f, 0.264f),
            new(0.452f, 0.347f),
            new(0.686f, 0.328f),
            new(0.170f, 0.419f),
            new(0.179f, 0.554f),
            new(0.467f, 0.531f),
            new(0.696f, 0.531f),
            new(0.782f, 0.686f),
            new(0.664f, 0.806f)
        };

        /// <summary>
        /// The one-storey house: bedside tables, the kitchen counter and the
        /// dining table. A house holds less than a shop and is safer to be in,
        /// which is the trade it exists to offer.
        /// </summary>
        private static readonly Vector2[] House01LootSpotsInPlan =
        {
            new(0.762f, 0.323f),
            new(0.260f, 0.437f),
            new(0.116f, 0.622f),
            new(0.232f, 0.701f),
            new(0.692f, 0.655f),
            new(0.661f, 0.876f),
            new(0.804f, 0.882f)
        };

        /// <summary>
        /// The two-storey house, whose plan is an L. Places in both arms, so
        /// walking in does not show you everything there is.
        /// </summary>
        private static readonly Vector2[] House02LootSpotsInPlan =
        {
            new(0.201f, 0.172f),
            new(0.151f, 0.483f),
            new(0.673f, 0.494f),
            new(0.782f, 0.511f),
            new(0.221f, 0.747f),
            new(0.684f, 0.858f),
            new(0.843f, 0.871f)
        };

        /// <summary>
        /// The supermarket's aisles and produce stands. Ten places, the most of
        /// any room: docs/17 wants the widest spread of prices here, and the
        /// narrow gaps between the shelves are where the chases are meant to
        /// happen.
        /// </summary>
        private static readonly Vector2[] SupermarketLootSpotsInPlan =
        {
            new(0.843f, 0.360f),
            new(0.304f, 0.456f),
            new(0.434f, 0.461f),
            new(0.565f, 0.467f),
            new(0.133f, 0.566f),
            new(0.799f, 0.578f),
            new(0.314f, 0.638f),
            new(0.437f, 0.631f),
            new(0.561f, 0.625f),
            new(0.234f, 0.919f)
        };

        /// <summary>
        /// Treasure places per room, keyed by model.
        ///
        /// Keyed rather than one array, because every room will get its own set
        /// and a single list would quietly apply the jeweller's shelves to the
        /// bookshop.
        ///
        /// Declared after the arrays it holds, and that is not cosmetic: static
        /// initialisers run in declaration order, so a dictionary written above
        /// them is built out of nulls. The map then threw on the first room it
        /// tried to mark, with a null reference and no hint that the cause was
        /// the order of two lines.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, Vector2[]> LootSpotsInPlan = new()
        {
            { "interior_jewelry", JewelryLootSpotsInPlan },
            { "interior_bookstore", BookstoreLootSpotsInPlan },
            { "interior_house01", House01LootSpotsInPlan },
            { "interior_house02", House02LootSpotsInPlan },
            { "interior_supermarket", SupermarketLootSpotsInPlan }
        };

        /// <summary>
        /// Where a room's fixed, alarmed piece stands, keyed by model.
        ///
        /// One room has one so far. Keyed anyway, for the same reason the
        /// treasure places are: a bare field would apply the jeweller's cabinet
        /// to the bookshop the moment a second room wanted one.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, Vector2> CaseInPlan = new()
        {
            { "interior_jewelry", new Vector2(0.512f, 0.459f) }
        };

        /// <summary>
        /// The middle of the room, where the big glass case stands with the
        /// diamond ring in it.
        ///
        /// Fixed, and the only fixed piece in the shop. Everything else moves
        /// every match, so the one thing both players can count on is the one
        /// thing that screams when it is taken — which is what makes the
        /// decision to take it a decision at all.
        /// </summary>
        private static readonly Vector2 JewelryCaseInPlan =
            new(0.512f, 0.459f);

        /// <summary>
        /// The margin the plan view is framed with, repeated here so a mark on
        /// the picture lands where it was drawn.
        /// </summary>
        private const float PlanMargin = 1.12f;

        /// <summary>
        /// How far the arrival point is kept from the way out, in metres.
        ///
        /// The exit trigger is 1.2 m deep and a metre and a half tall, and it
        /// has to be walked into rather than pressed. Four metres is well clear
        /// of it with the door still behind you when you turn round.
        /// </summary>
        private const float ExitClearance = 4f;

        /// <summary>
        /// Where the way out stands, when it is not opposite the way in.
        ///
        /// Normally the door is one hole and the same hole serves both
        /// directions, so the spawn mark is enough. The two-storey house has
        /// its entrance in one place and the room's own opening in another, and
        /// leaving through a doorway on the far side of the house is not
        /// leaving the way you came.
        ///
        /// Read off the plan like the spawn, and in the same coordinates.
        ///
        /// Marking the doorway alone is enough, and better than marking the
        /// arrival as well. `mouth` — the fallback — is the middle of whichever
        /// wall the opening was found in, and the middle of a wall is not the
        /// door when the room is L-shaped: the two-storey house has its way in
        /// at the porch on one arm and its `+Z` wall centre over the other, so
        /// the trigger sat in a window across the house. Given the doorway, the
        /// arrival is simply four metres inside it, which is what "just through
        /// the door" means in every room.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, Vector2> DoorInPlan = new()
        {
            { "interior_house01", new Vector2(0.521f, 0.145f) },

            // Kept although nothing uses house03 at the moment. It is keyed by
            // model name, so it costs nothing while the model is on the shelf
            // and is right again the day it goes back in.
            { "interior_house03", new Vector2(0.472f, 0.391f) }
        };

        /// <summary>
        /// What the coarse collision copy of a room is called.
        /// </summary>
        private const string CollisionSuffix = "_col";

        private const string BuildingDirectory =
            "Assets/_Project/Art/Buildings";

        // Interiors are filed with the buildings, which is the one folder
        // PlaceholderModelLibrary loads from. The `interior_` prefix keeps a
        // shell and the room behind it apart without a second folder.

        /// <summary>
        /// How much bigger the room is than the building.
        ///
        /// 2.2 puts the inside at roughly 17 m by 12 m, which is about what the
        /// hand-built 16 m box was and therefore already known to play. It also has
        /// to keep the camera below the wall tops: the interior camera sits at most
        /// <c>1.2 + 6.5·sin(pitch)</c> above the player, and 2.55 m walls at this
        /// scale stand 5.6 m tall, so the camera's pitch is capped to match. Raising
        /// one without the other is how every neighbouring room became visible at
        /// once the first time (ISSUE-035).
        /// </summary>
        private const float InteriorScale = 2.2f;

        /// <summary>
        /// How big a room is fitted to, in metres.
        ///
        /// Fitted to a footprint rather than multiplied by a scale. The scale
        /// was tuned against one model that happened to export at about eight
        /// metres across; every interior since exports at about one, so the
        /// same multiplier produced a room two metres wide — smaller than the
        /// player, and reported as "missing its walls" because nothing that
        /// small can have an inside.
        ///
        /// Seventeen by twelve is what the hand-built room measured, and
        /// therefore what is already known to play.
        /// </summary>
        private const float RoomX = 17f;

        private const float RoomZ = 12f;

        /// <summary>
        /// How tall every room's walls stand, in metres.
        ///
        /// Rooms were being fitted to a shared floor footprint, and a shared
        /// floor is the wrong thing to share. Uniform scale means a model with
        /// a different plan shape shrinks until its long side fits, so the
        /// supermarket — long and thin — came out smaller than a house it
        /// should dwarf, and every room ended up a different height.
        ///
        /// Wall height is what should match. It is the one measurement rooms
        /// genuinely have in common, it keeps the camera's clearance the same
        /// everywhere, and it lets a big shop be a big shop.
        ///
        /// Four and a half metres, which is a decision shared with the
        /// camera. The interior camera sits `1.2 + 6.5·sin(pitch)` above the
        /// player, so at its 28 degree cap it is 4.25 m up — under the wall
        /// tops, which is what stops every neighbouring room appearing at once
        /// (ISSUE-035). Raising one without the other reopens that.
        ///
        /// It also sets how big a room is, since the plan follows the height.
        /// These models are drawn dollhouse-style with walls short relative to
        /// their floor, so this comes out at a generous room rather than a
        /// cramped one — which is the other half of what was wrong.
        /// </summary>
        private const float WallHeight = 4.5f;

        /// <summary>
        /// How far inside its own doorway a player is put down.
        ///
        /// Past the trigger, so arriving is not read as leaving, and far enough
        /// in that the door is behind them when they turn round.
        /// </summary>
        private const float EntryStandoff = 3.2f;

        /// <summary>
        /// How far out from the wall the doorstep sits, in metres.
        ///
        /// Outside the building's own collider, so standing on the step is
        /// standing in the street rather than inside the wall.
        /// </summary>
        private const float DoorStandoff = 1.3f;

        /// <summary>
        /// How close a player has to be for the prompt to appear.
        ///
        /// Generous enough to find without hunting, tight enough that running
        /// down a terrace does not light up four doors at once.
        /// </summary>
        private const float DoorPromptRadius = 2.2f;

        /// <summary>
        /// The world box everything drawn for a building occupies.
        /// </summary>
        private static bool TryGetWorldBounds(
            Transform subject,
            out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer part in
                subject.GetComponentsInChildren<Renderer>(true))
            {
                if (!any)
                {
                    bounds = part.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(part.bounds);
                }
            }

            return any;
        }

        /// <summary>
        /// A grid rather than a row. Nineteen rooms in a line reach 700 m from the
        /// town and put coordinates far from everything else for no benefit.
        /// </summary>
        private const int Columns = 5;
        private const float SpacingX = 44f;
        private const float SpacingZ = 44f;

        /// <summary>
        /// Well south of the map, which runs to z = -22.
        /// </summary>
        private const float RowZ = -70f;
        private const float RowStartX = -28f;

        /// <summary>
        /// The outside of the room. Given mesh colliders, not boxes: these walls have
        /// door-shaped holes in them, and a box from their bounds would seal the way
        /// out. A mesh collider keeps the holes.
        /// </summary>
        private static readonly string[] SolidPrefixes =
        {
            "BD_House1F_Wall_",
            "BD_House1F_Foundation"
        };

        /// <summary>
        /// The partitions, which are rebuilt at half height instead of being kept.
        ///
        /// Full-height partitions are the reason being indoors was hard to look at.
        /// Nine walls at 5.6 m, and the camera can only be 5.2 m up, so several of
        /// them are between it and the player at any angle: removing whichever ones
        /// were in the way meant walls appearing and disappearing every time the view
        /// turned, which reads worse than the walls did.
        ///
        /// A 2 m wall divides the room without ever getting in the way. The character
        /// is 1.9 m, so their head clears it and the camera sees over all of them at
        /// once — the doll's-house read. The rooms stay separate places because the
        /// floors are different colours per room in the model and the furniture is
        /// still where it was.
        ///
        /// The model's own interior door frames go with them: 4.7 m frames standing
        /// over 2 m walls would be the only thing left blocking the view.
        /// </summary>
        private static readonly string[] PartitionPrefixes =
        {
            "IN_House1F_Wall",
            "IN_House1F_Door",
            "IN_House1F_InteriorTrim"
        };

        /// <summary>
        /// How tall a partition is rebuilt. Just over a character, so a head shows
        /// above it and nothing else does.
        /// </summary>
        private const float PartitionHeight = 2f;

        /// <summary>
        /// Furniture worth bumping into, named by item rather than by part.
        ///
        /// One box per piece of furniture, from the union of its parts. Matching
        /// part names instead produced 87 colliders per room — a separate box for
        /// every chair slat, drawer front and sofa cushion — and none of them was
        /// the shape of the thing a player actually walks into. Boxes rather than
        /// meshes because what these are for is cover and somewhere to hide loot
        /// behind, and a resolved chair leg buys nothing.
        /// </summary>
        private static readonly string[] FurniturePrefixes =
        {
            "IN_Bedroom_Bed",
            "IN_Bedroom_Wardrobe",
            "IN_Bedroom_Dresser",
            "IN_Bedroom_Nightstand",
            "IN_LivingRoom_Sofa",
            "IN_LivingRoom_TVStand",
            "IN_LivingRoom_TV",
            "IN_LivingRoom_LowCabinet",
            "IN_LivingRoom_CoffeeTable",
            "IN_LivingRoom_SideTable",
            "IN_LivingRoom_DecorPlant",
            "IN_Kitchen_Counter",
            "IN_Kitchen_Fridge",
            "IN_Kitchen_SinkCabinet",
            "IN_Kitchen_Stove",
            "IN_Kitchen_UpperCabinet",
            "IN_Dining_Table",
            "IN_Dining_Chair",
            "IN_Bathroom_Bathtub",
            "IN_Bathroom_Toilet",
            "IN_Bathroom_Sink",
            "IN_Bathroom_Storage",
            "IN_Entrance_Storage"
        };

        /// <summary>
        /// Anything shorter than this is walked over rather than round: rugs, the
        /// per-room floor surfaces, a mattress lying on a frame that already has a
        /// box of its own.
        /// </summary>
        private const float BlockingHeight = 0.6f;

        public static void Build(
            Transform parent,
            MatchRuntimeState matchRuntime,
            IReadOnlyList<(Transform Building, string Kind)> houses,
            System.Func<string, Color, Material> materialFactory,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cubeFactory,
            System.Func<string, Transform, Transform> childFactory)
        {
            if (houses == null || houses.Count == 0)
            {
                return;
            }

            // Cleared per rebuild. Left standing, a second rebuild in the same
            // editor session would copy the previous rebuild's copies.
            _stemTemplates.Clear();
            _claimedStems.Clear();
            _casedStems.Clear();

            // The ground and the buildings were made moments ago in this same
            // pass, and a collider created this frame is not in the physics
            // scene yet. Every doorstep below is found by raycasting down for
            // ground, so without this the search finds nothing under any of
            // them and every door falls back to a guess.
            Physics.SyncTransforms();

            Transform root = childFactory("House Interiors", parent);
            root.localPosition = Vector3.zero;

            Material floorMaterial = materialFactory(
                "Greybox_InteriorFloor",
                new Color(0.42f, 0.36f, 0.31f));

            // Close to the model's own cream wall so the rebuilt partitions do not
            // read as a different material from the shell they stand in.
            Material partitionMaterial = materialFactory(
                "Greybox_InteriorPartition",
                new Color(0.80f, 0.77f, 0.71f));

            int built = 0;
            int solids = 0;
            int props = 0;
            int partitions = 0;
            for (int index = 0; index < houses.Count; index++)
            {
                (Transform building, string kind) = houses[index];
                if (building == null)
                {
                    continue;
                }

                // A kind with no room of its own gets nothing rather than
                // somebody else's. A door that opens into the wrong shop is
                // worse than a door that does not open.
                // The station is scenery. A thief who can walk into the police
                // station is a thief with a room to hide in inside the one
                // building the game already sends them to under arrest.
                if (kind == "PoliceStation")
                {
                    continue;
                }

                if (!InteriorForKind.TryGetValue(kind, out string stem))
                {
                    Debug.LogWarning(
                        $"[MAP-008] '{kind}' has no interior model, so its "
                        + "building has no inside.");
                    continue;
                }

                var centre = new Vector3(
                    RowStartX + index % Columns * SpacingX,
                    0f,
                    RowZ - index / Columns * SpacingZ);
                if (BuildOne(
                        root,
                        matchRuntime,
                        index,
                        building,
                        stem,
                        centre,
                        floorMaterial,
                        partitionMaterial,
                        cubeFactory,
                        childFactory,
                        ref solids,
                        ref props,
                        ref partitions))
                {
                    built++;
                }
            }

            Debug.Log(
                $"[MAP-008] {built} house interiors built from the model at "
                + $"{InteriorScale:0.#}x, with {solids} shell colliders, "
                + $"{props} furniture colliders and {partitions} partitions "
                + $"rebuilt at {PartitionHeight:0.#}m, front and back doors on "
                + "each.");
        }

        private static bool BuildOne(
            Transform root,
            MatchRuntimeState matchRuntime,
            int index,
            Transform house,
            string stem,
            Vector3 centre,
            Material floorMaterial,
            Material partitionMaterial,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cube,
            System.Func<string, Transform, Transform> child,
            ref int solids,
            ref int props,
            ref int partitions)
        {
            int number = index + 1;
            Transform room = child($"Interior {number}", root);
            room.position = centre;

            // Measured at its own size first, then scaled by height.
            //
            // The library fits to a footprint, and a footprint is what must not
            // be shared here. So it is asked for the model unscaled, and the
            // scale that makes its walls the standard height is worked out and
            // applied.
            Vector3 native = PlaceholderModelLibrary
                .TryInstantiateBuildingSized(
                    stem,
                    room,
                    centre,
                    0f,
                    0f,
                    1f);
            if (native.y > 0.001f)
            {
                float lift = WallHeight / native.y;
                foreach (Transform part in room)
                {
                    part.localScale *= lift;
                    part.position = centre
                        + (part.position - centre) * lift;
                }
            }

            Vector3 size = native * (native.y > 0.001f
                ? WallHeight / native.y
                : 1f);
            if (size.y <= 0f)
            {
                Debug.LogError(
                    $"[MAP-008] Interior {number} could not be built: "
                    + $"'{stem}' did not instantiate.");
                Object.DestroyImmediate(room.gameObject);
                return false;
            }

            Renderer[] parts =
                room.GetComponentsInChildren<Renderer>(true);
            if (!TryMeasure(
                    parts,
                    out float floorTop,
                    out Bounds inner,
                    out Vector3 frontDoor,
                    out Vector3 backDoor))
            {
                Debug.LogError(
                    $"[MAP-008] Interior {number} ({stem}) measured "
                    + $"{inner.size.x:0.0} x {inner.size.z:0.0} m inside, "
                    + "which is not a room. It is missing the walls or doors "
                    + "it is measured from.");
                Object.DestroyImmediate(room.gameObject);
                return false;
            }

            Transform colliders = child($"Interior {number} Colliders", room);
            colliders.position = centre;

            // The room as it actually stands, not as it was authored.
            //
            // This was being built from the size TryInstantiateBuildingSized
            // reports, which is the model's own measurement before it is fitted
            // — about a metre. The collision copy was therefore scaled to a
            // metre and left as a small lump in the middle of the floor, and
            // every probe ray sailed straight over it: the walls read as one
            // continuous fourteen-metre doorway.
            if (!TryGetWorldBounds(room, out Bounds roomShell))
            {
                roomShell = new Bounds(
                    new Vector3(inner.center.x, floorTop, inner.center.z),
                    new Vector3(inner.size.x, 3f, inner.size.z));
            }
            solids += AddSolidColliders(
                stem,
                colliders,
                parts.Length > 0 ? parts[0].transform : null,
                out Transform collisionRoot);

            // The floor a player stands on, not the bottom of the model.
            //
            // floorTop was the underside of the whole thing, which is where the
            // slab and the entry points were put. The model's own floor plate
            // is some way above that, so arriving "on the floor" put the
            // character's feet under it and their legs through it — exactly the
            // buried look that was reported.
            floorTop = MeasureFloorTop(collisionRoot, roomShell, floorTop);

            // The furniture and the partitions came from the same part-name
            // lookup and find nothing for the same reason: these rooms are one
            // welded mesh. The collision copy above already carries both — a
            // sofa in it is a sofa-shaped lump of collision — so there is
            // nothing left for them to add.
            props += AddFurnitureColliders(parts, colliders);
            partitions += AddHalfHeightPartitions(
                parts,
                colliders,
                floorTop,
                cube,
                partitionMaterial);

            // Asked of the collision mesh, which only exists as of a moment
            // ago and is not in the physics scene until it is pushed there.
            // All four walls, not two.
            //
            // The probe used to ask only about the front and the back, so a
            // room whose door is in a side wall — which the house is — reported
            // its front as open on the strength of a window, and the player was
            // put down against the outside of a wall they could not walk
            // through. Which wall the door is in is a property of the model and
            // has to be asked of the model.
            Vector3 doorway = Vector3.forward;
            float widest = -1f;
            foreach (Vector3 side in new[]
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left
            })
            {
                float gap = WidestGap(collisionRoot, inner, floorTop, side);
                if (gap > widest)
                {
                    widest = gap;
                    doorway = side;
                }
            }

            if (DoorWallForModel.TryGetValue(stem, out Vector3 named))
            {
                Debug.Log(
                    $"[MAP-008] Interior {number} ({stem}): probe said "
                    + $"{doorway} ({widest:0.00} m), overridden to {named}.");
                doorway = named;
            }
            else
            {
                Debug.Log(
                    $"[MAP-008] Interior {number} ({stem}) opens {doorway} "
                    + $"with a {widest:0.00} m gap.");
            }

            // A thick slab under the whole room, on top of the model's own floor.
            // The foundation is a 1.1 m plate at this scale, and a character who has
            // picked up fall speed crosses a thin surface between two frames — which
            // is how the sinking started (ISSUE-036).
            cube(
                $"Interior {number} Floor Slab",
                new Vector3(centre.x, floorTop - 1f, centre.z),
                new Vector3(size.x, 2f, size.z),
                floorMaterial,
                room,
                true);

            // Well inside each door, clear of its trigger.
            //
            // 1.6 m was not: the way out is a trigger now, and arriving inside it
            // means the door reads the arrival as a departure and throws the player
            // straight back into the street. 3.2 m is past it with room to spare, and
            // still close enough that the door is behind you when you turn round.
            // Inside the room, just clear of its own doorway, on whichever
            // wall the doorway turned out to be in.
            float half = Mathf.Abs(Vector3.Dot(inner.extents, doorway));
            Vector3 mouth = new Vector3(inner.center.x, floorTop, inner.center.z)
                + doorway * half;

            Transform frontEntry = child($"Interior {number} Entry Front", room);
            frontEntry.position = SpawnInPlan.TryGetValue(stem, out Vector2 mark)
                ? FromPlan(inner, floorTop, mark)
                : ClearSpotInside(
                    collisionRoot,
                    inner,
                    floorTop,
                    mouth,
                    doorway);

            // Looking into the room, away from the door they came through.
            // Arriving nose to a wall costs the player the first second of
            // every visit working out which way is in.
            frontEntry.rotation = Quaternion.LookRotation(-doorway, Vector3.up);

            // The shelves and cabinets a room's treasure can appear on.
            //
            // Markers only: empty transforms that name a place. What stands on
            // them is decided when a match starts, by the host, so the room is
            // laid out differently every time and neither player can learn it.
            // Authoring the pieces here instead would fix the layout in the
            // scene, which is the thing this is for avoiding.
            if (LootSpotsInPlan.TryGetValue(stem, out Vector2[] lootMarks))
            {
                for (int place = 0; place < lootMarks.Length; place++)
                {
                    child(
                            $"Interior {number} Loot Spot {place + 1}",
                            room)
                        .position = FromPlan(inner, floorTop, lootMarks[place]);
                }

                Debug.Log(
                    $"[LOOT-SPOT] Interior {number} ({stem}): "
                    + $"{lootMarks.Length} treasure places marked.");

                StockRoom(room, stem, number, matchRuntime);
            }

            // The alarmed piece onto the case the model already draws.
            //
            // The jeweller's interior has a glass cabinet painted into the
            // middle of its floor, and the working display case was standing out
            // in the street in front of the shop — a case nobody could reach
            // guarding a ring nobody could take, with a picture of a case
            // indoors doing nothing. This puts the working one on the drawn one.
            //
            // The case is moved with the ring rather than found again from the
            // scene: it is a separate object that happens to sit where the ring
            // sits, so leaving it behind would put the glass in the street and
            // the ring in the room.
            // The first jeweller's only. There is one alarmed ring in the town and
            // one glass case holding it, so a second shop calling this would take
            // both out of the first — the last jeweller built would end up with
            // the ring and every earlier one with an empty plinth, and the error
            // would look like the case failing rather than like it moving.
            if (CaseInPlan.TryGetValue(stem, out Vector2 caseMark)
                && _casedStems.Add(stem))
            {
                MoveAlarmedPieceInto(
                    FromPlan(inner, floorTop, caseMark),
                    stem,
                    number);
            }
            Transform backEntry = child($"Interior {number} Entry Back", room);
            backEntry.position = frontEntry.position;

            // Out at the real building. One way in means one way out, and both
            // names still have to be filled because HouseInterior wants a pair.
            Transform frontExit = child($"Interior {number} Exit Front", room);
            frontExit.position = OutsidePoint(house, HouseDoorSide.Front, 5.4f);
            Transform backExit = child($"Interior {number} Exit Back", room);
            backExit.position = frontExit.position;

            HouseInterior interior =
                room.gameObject.AddComponent<HouseInterior>();
            interior.Configure(
                number,
                frontEntry,
                backEntry,
                frontExit,
                backExit,
                new Vector2(
                    inner.extents.x - 1.2f,
                    inner.extents.z - 1.2f),
                floorTop);

            // One door. Where the plan says, when the plan says; otherwise in
            // the middle of the wall the opening was found in.
            Vector3 doorAt = DoorInPlan.TryGetValue(stem, out Vector2 spot)
                ? FromPlan(inner, floorTop, spot)
                : mouth - doorway * 0.2f;

            // Pushed out against the wall.
            //
            // A door in the middle of a room is a door with room on both sides,
            // and the side that is not the street is where the player is put
            // down. Sitting it on the wall gives the arrival somewhere to be.
            bool bothMarked = DoorInPlan.ContainsKey(stem)
                && SpawnInPlan.ContainsKey(stem);
            float toWall = bothMarked
                ? 0f
                : Mathf.Abs(Vector3.Dot(inner.extents, doorway))
                - Vector3.Dot(doorAt - inner.center, doorway)
                - 0.4f;
            if (toWall > 0f)
            {
                doorAt += doorway * toWall;
            }

            // And the arrival is moved off it.
            //
            // The way out is a trigger you walk into, and the way in used to
            // land inside that trigger: entering fired the exit on the same
            // frame and the player bounced straight back into the street. The
            // same mistake as ISSUE-043, one room further in.
            if (DoorInPlan.ContainsKey(stem) && !SpawnInPlan.ContainsKey(stem))
            {
                // The doorway was marked and the arrival was not, so the
                // arrival is straight in from it. Lateral position comes from
                // the door too — moving only along the wall normal would leave
                // it wherever the wall's midpoint happened to be, which on an
                // L-shaped plan is the other arm of the house.
                frontEntry.position = doorAt - doorway * ExitClearance;
                backEntry.position = frontEntry.position;
            }
            else
            {
                float toExit = bothMarked
                    ? ExitClearance
                    : Vector3.Dot(doorAt - frontEntry.position, doorway);
                if (toExit < ExitClearance)
                {
                    frontEntry.position -= doorway * (ExitClearance - toExit);
                    backEntry.position = frontEntry.position;
                }
            }
            CreateInsideDoor(
                room,
                interior,
                matchRuntime,
                HouseDoorSide.Front,
                doorAt,
                number);

            BuildPerimeter(colliders, inner, floorTop, doorway);

            // Whichever face the camera is behind comes away as a whole. Given the
            // room's measured inside rather than a list of parts: it works out which
            // face each piece belongs to from where the piece is, so windows and
            // siding leave with the wall they are bolted to.
            room.gameObject.AddComponent<InteriorShellScreen>()
                .Configure(
                    inner.center - room.position,
                    new Vector2(inner.extents.x, inner.extents.z),
                    floorTop);

            // Only where the room actually opens. A building whose inside is
            // drawn with one door gets one door outside; the other used to be a
            // prompt on a solid wall that led into the middle of a bookcase.
            CreateEntrance(house, interior, matchRuntime, HouseDoorSide.Front);
            return true;
        }

        /// <summary>
        /// Reads the room's floor height, its inside extents and where its two doors
        /// are, from the model rather than from constants.
        /// </summary>
        /// <summary>
        /// How far inside the shell a doorway sits.
        ///
        /// Far enough to be through the wall rather than in it, close enough
        /// that walking out of one is walking out of the building.
        /// </summary>
        private const float DoorInset = 1.2f;

        /// <summary>
        /// How much of the shell's width the walls take, as a fraction.
        ///
        /// Used to find the space between the walls when the walls are not
        /// separate objects to measure. A tenth each side is what the models
        /// that *are* separable measure at.
        /// </summary>
        private const float WallShare = 0.1f;

        /// <summary>
        /// Reads the room's floor height, its inside extents and where its two
        /// doors go, from the model's shape rather than from its part names.
        ///
        /// It used to look up seven parts by name — `BD_House1F_Wall_Left` and
        /// so on — which worked for exactly one model, the hand-built one it
        /// was written against. Every interior that has arrived since is a
        /// single scanned mesh with no named parts at all, so the lookup found
        /// nothing and every room reported itself as missing its walls.
        ///
        /// Shape survives that. A room is a box open at the top: its floor is
        /// the bottom of its bounds, its walls are the outside of them, and its
        /// doors go in the middle of the two long edges. Nothing here can be
        /// broken by an exporter renaming a mesh.
        ///
        /// The doors are placed rather than found. None of these models has a
        /// door in it — they are rooms, drawn open — and a doorway is a trigger
        /// and a destination rather than a thing to look at.
        /// </summary>
        private static bool TryMeasure(
            IReadOnlyList<Renderer> parts,
            out float floorTop,
            out Bounds inner,
            out Vector3 frontDoor,
            out Vector3 backDoor)
        {
            floorTop = 0f;
            inner = new Bounds();
            frontDoor = Vector3.zero;
            backDoor = Vector3.zero;

            bool any = false;
            var shell = new Bounds();
            foreach (Renderer part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                if (!any)
                {
                    shell = part.bounds;
                    any = true;
                }
                else
                {
                    shell.Encapsulate(part.bounds);
                }
            }

            if (!any)
            {
                return false;
            }

            floorTop = shell.min.y;

            float wallX = shell.size.x * WallShare;
            float wallZ = shell.size.z * WallShare;
            inner = new Bounds(
                new Vector3(shell.center.x, floorTop, shell.center.z),
                new Vector3(
                    Mathf.Max(0f, shell.size.x - wallX * 2f),
                    0f,
                    Mathf.Max(0f, shell.size.z - wallZ * 2f)));

            frontDoor = new Vector3(
                shell.center.x,
                floorTop,
                shell.min.z + DoorInset);
            backDoor = new Vector3(
                shell.center.x,
                floorTop,
                shell.max.z - DoorInset);

            return inner.size.x > 1f && inner.size.z > 1f;
        }

        /// <summary>
        /// Mesh colliders for the walls and the floor plate.
        ///
        /// Built as separate objects rather than by adding components to the model's
        /// own parts. The model comes in as a prefab instance, so a component added
        /// to one of its children is written into the scene as an override — several
        /// hundred of them across nineteen rooms, for nothing. These are plain
        /// colliders with no renderer, so the batching pass has no opinion on them.
        /// </summary>
        /// <summary>
        /// Makes the room solid, from a collision mesh built for the purpose.
        ///
        /// The old version picked out named parts — walls, floor — and gave
        /// each one a mesh collider. Every interior that has arrived since is a
        /// single welded mesh with no parts to pick, so it matched nothing and
        /// added nothing: the rooms were furnished, lit, enterable and made
        /// entirely of air.
        ///
        /// One mesh means the choice is a mesh collider or nothing. The room's
        /// own mesh is a hundred thousand triangles and there are thirteen
        /// rooms, which is 1.3 million triangles of collision for a build that
        /// is meant to run in a browser. So a second, coarse copy of each room
        /// is decimated to four thousand and used for collision only —
        /// fifty-two thousand across the town, and a wall is a wall whether it
        /// is described by four triangles or four hundred.
        /// </summary>
        private static int AddSolidColliders(
            string stem,
            Transform holder,
            Transform visual,
            out Transform collisionRoot)
        {
            collisionRoot = null;
            GameObject collision = PlaceholderModelLibrary.TryInstantiate(
                $"{BuildingDirectory}/{stem}{CollisionSuffix}.fbx",
                holder,
                $"{stem} Collision");
            if (collision == null)
            {
                Debug.LogWarning(
                    $"[MAP-008] '{stem}' has no collision mesh "
                    + $"('{stem}{CollisionSuffix}'), so that room is made of "
                    + "air. Run the collision bake.");
                return 0;
            }

            // Given the visible model's own transform, exactly.
            //
            // The two are the same model at different triangle counts, so the
            // placement that is right for one is right for the other. Fitting
            // the coarse copy to the room by measuring and scaling was three
            // lines of arithmetic that had to agree with a fourth somewhere
            // else, and it did not: the collision came out half the height of
            // the room it was meant to be, which is a wall you can see and
            // walk through.
            if (visual == null)
            {
                Object.DestroyImmediate(collision);
                Debug.LogWarning(
                    $"[MAP-008] '{stem}' has no visible model to match, so "
                    + "its collision copy has nowhere to be.");
                return 0;
            }

            collision.transform.SetPositionAndRotation(
                visual.position,
                visual.rotation);
            collision.transform.localScale = visual.lossyScale;

            collisionRoot = collision.transform;
            int added = 0;
            foreach (MeshFilter filter in
                collision.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                // The renderer goes; only the shape is wanted. Leaving it on
                // would draw a coarse grey copy of the room inside the room.
                Renderer drawn = filter.GetComponent<Renderer>();
                if (drawn != null)
                {
                    Object.DestroyImmediate(drawn);
                }

                MeshCollider collider =
                    filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                added++;
            }

            return added;
        }

        /// <summary>
        /// The height of the surface a player actually stands on.
        ///
        /// Read off the collision geometry rather than by raycasting into it.
        /// A collider added moments ago in the editor is not reliably in the
        /// physics scene, which is what made the wall probe report a fourteen
        /// metre doorway in a thirteen metre wall. Triangles are in memory the
        /// moment the mesh is.
        ///
        /// The floor is the upward-facing geometry in the bottom third of the
        /// room. Upward-facing alone would also collect table tops and shelves;
        /// the bottom third rules those out without needing to know what a
        /// table is.
        /// </summary>
        private static float MeasureFloorTop(
            Transform collision,
            Bounds shell,
            float fallback)
        {
            if (collision == null)
            {
                return fallback;
            }

            float ceiling = shell.min.y + shell.size.y * 0.34f;
            float best = float.MinValue;

            foreach (MeshFilter filter in
                collision.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Transform space = filter.transform;
                for (int index = 0;
                    index < vertices.Length && index < normals.Length;
                    index++)
                {
                    if (space.TransformDirection(normals[index]).y < 0.85f)
                    {
                        continue;
                    }

                    float y = space.TransformPoint(vertices[index]).y;
                    if (y <= ceiling && y > best)
                    {
                        best = y;
                    }
                }
            }

            return best > float.MinValue ? best : fallback;
        }

        /// <summary>
        /// Which sides of a room have a way through, measured rather than
        /// assumed.
        ///
        /// Rooms were being given a front and a back door whichever they had.
        /// Half of these models are drawn with one opening, so the other
        /// doorway was a hole punched in a solid wall: a door that led into the
        /// street through no gap at all, and from the street into the middle of
        /// a bookcase.
        ///
        /// Found by firing across each wall from inside at door height. Where
        /// the wall is there, the ray stops; where the opening is, it does not.
        /// A run of misses wide enough to walk through is a door.
        /// </summary>
        /// <summary>
        /// Turns a mark on the plan into a spot on the floor.
        ///
        /// The plan is framed to a square of the room's larger side plus a
        /// margin, centred on the room. Reversing that is the whole conversion,
        /// and it is written once here so the picture and the town cannot drift
        /// apart.
        /// </summary>
        /// <summary>
        /// Puts the room's alarmed piece, and the glass around it, where the
        /// model draws its cabinet.
        ///
        /// Found by the definition rather than by name, because the object's
        /// name is decided by whichever generator made it and has been renamed
        /// twice already. The definition's stable id is the one handle that is
        /// meant to survive.
        ///
        /// Says so when it finds nothing. A ring left in the street is a ring
        /// the thief cannot take and a room with a picture of a cabinet in it —
        /// neither of which looks like an error from inside the game.
        /// </summary>
        private static void MoveAlarmedPieceInto(
            Vector3 target,
            string stem,
            int number)
        {
            foreach (LootItem piece in
                UnityEngine.Object.FindObjectsByType<LootItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None))
            {
                if (piece.Definition == null
                    || !piece.Definition.RaisesAlarm
                    || !piece.Definition.StableId.StartsWith("jewel-"))
                {
                    continue;
                }

                // The glass first, while it is still standing on the ring: it is
                // a sibling that shares the position, not a child, so it has to
                // be carried across deliberately.
                foreach (LootDisplayCase glass in
                    UnityEngine.Object.FindObjectsByType<LootDisplayCase>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None))
                {
                    if (glass.Contents == piece)
                    {
                        glass.transform.position = target;
                    }
                }

                piece.transform.position = target;
                Debug.Log(
                    $"[LOOT-CASE] Interior {number} ({stem}): "
                    + $"'{piece.Definition.StableId}' and its glass moved to "
                    + $"{target}.");
                return;
            }

            Debug.LogError(
                $"[LOOT-CASE] Interior {number} ({stem}) has a cabinet marked "
                + "and there is no alarmed jewellery piece in the scene to put "
                + "in it. The ring is still wherever it was authored.");
        }

        /// <summary>
        /// Which of a shop's pieces its own room deals out, by stable id prefix.
        ///
        /// Prefixes rather than a list of ids, so the pieces docs/17 still has
        /// waiting join their own shop's draw the moment they are authored. A
        /// list would have to be edited every time, and the edit that gets
        /// forgotten is the one that leaves a new piece standing in the street.
        ///
        /// Each house stocks the first instance of its model and no others.
        /// There are several of each and they are the same room; four pieces
        /// spread over every copy would make a town where the thief walks house
        /// to house collecting one thing, which is the least interesting route
        /// this map can offer.
        /// </summary>
        /// <summary>
        /// The first room of each kind, which owns the pieces the map staged in
        /// the street. Every later room of that kind copies them.
        ///
        /// Cleared at the start of every Build, or a second rebuild in one editor
        /// session would find every kind already claimed and copy the copies.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<
            string, LootItem[]> _stemTemplates = new();

        /// <summary>
        /// How many fewer pieces a room holds than it has places to put them.
        ///
        /// Three. Fewer pieces than places is what makes a room fall differently
        /// every match, and it is also what guarantees two pieces never land on
        /// the same shelf — the draw removes each place it uses, so it can only
        /// run short when the counts are equal. Fill a room to capacity and the
        /// draw still succeeds and still logs a cheerful number while quietly
        /// becoming a fixed layout.
        /// </summary>
        private const int PlacesLeftEmpty = 3;

        private static readonly System.Collections.Generic.Dictionary<
            string, string> RoomStockPrefix = new()
        {
            { "interior_jewelry", "jewel-" },
            { "interior_supermarket", "market-" },
            { "interior_bookstore", "book-" },
            { "interior_house01", "house1-" },
            { "interior_house02", "house2-" }
        };

        /// <summary>
        /// Gives a room its own treasure and hands it the job of laying it out.
        ///
        /// Two things happen and they are not the same thing. The pieces are put
        /// indoors now, at build time, so the saved scene is honest about where a
        /// shop's stock is — leaving them in the street until the first match
        /// started would mean a scene that looks wrong to anybody opening it and a
        /// lobby that shows treasure on the pavement. Then <c>LootSpotDraw</c>
        /// reshuffles them across the marked places when a match begins, which is
        /// what stops either player learning the room.
        ///
        /// **Every** interior is stocked, not one per kind. The first room of a
        /// kind takes the pieces the map staged in the plaza; the rest get copies
        /// of them. It used to be one room per kind, and the town had thirteen
        /// marked interiors and five with anything in them — a player who walked
        /// into a house found seven empty shelves and no way to tell that from a
        /// house the thief had already emptied. The reason it was written that way
        /// was sound (two rooms shuffling one set of objects is two writers on one
        /// set of positions) and copying is the fix that keeps it sound: each room
        /// owns its own objects and no draw can reach another room's.
        ///
        /// The alarmed piece is left alone. It is fixed in its cabinet by design,
        /// and it has already been put there.
        /// </summary>
        private static void StockRoom(
            Transform room,
            string stem,
            int number,
            MatchRuntimeState matchRuntime)
        {
            if (!RoomStockPrefix.TryGetValue(stem, out string prefix))
            {
                return;
            }

            Transform[] places = room
                .GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.Contains("Loot Spot"))
                .OrderBy(t => t.name)
                .ToArray();

            LootItem[] pool = ResolveStemTemplates(stem, prefix);
            if (pool.Length == 0 || places.Length == 0)
            {
                Debug.LogError(
                    $"[LOOT-STOCK] Interior {number} ({stem}) has "
                    + $"{pool.Length} kinds for {places.Length} places, so "
                    + "the room has nothing to steal.");
                return;
            }

            // Three fewer than there are places, and never the same three kinds
            // twice when the shop has more kinds than that. Shuffled per room, so
            // two supermarkets are not the same supermarket.
            int wanted = Mathf.Max(1, places.Length - PlacesLeftEmpty);
            LootItem[] chosen = ChooseKinds(pool, wanted);
            if (chosen.Length < wanted)
            {
                Debug.LogWarning(
                    $"[LOOT-STOCK] Interior {number} ({stem}) wants {wanted} "
                    + $"kinds for {places.Length} places and the shop only "
                    + $"defines {pool.Length}. The room falls the same way every "
                    + "match to the extent it is short.");
            }

            // The first room of a kind adopts the pieces the map staged in the
            // plaza; every later one gets copies. Adopting rather than copying
            // for all of them matters: the staged originals are deliberately put
            // somewhere loud, so anything left standing in the square is the
            // shop's stock that no room claimed, and that is a fault worth
            // seeing rather than hiding.
            LootItem[] stock = _claimedStems.Add(stem)
                ? chosen
                : chosen.Select(item => CopyPiece(item, number)).ToArray();

            // Parked on the first places in order. The draw scatters them
            // properly at match start; this is only so the scene is not saved
            // with a room's stock lying in the road.
            for (int index = 0; index < stock.Length; index++)
            {
                stock[index].transform.position =
                    places[index % places.Length].position;
            }

            room.gameObject.AddComponent<LootSpotDraw>()
                .Configure(matchRuntime, places, stock);

            Debug.Log(
                $"[LOOT-STOCK] Interior {number} ({stem}): {stock.Length} "
                + $"pieces over {places.Length} places — "
                + string.Join(
                    ", ",
                    stock.Select(item => item.Definition.StableId)));
        }

        /// <summary>
        /// Which kinds a shop can produce, found once and remembered.
        ///
        /// Found once because the second room of a kind must not find the first
        /// room's copies and copy those: the search is by definition prefix and a
        /// copy has the same definition, so re-running it would return every piece
        /// already placed and the town's stock would double per interior.
        /// </summary>
        private static LootItem[] ResolveStemTemplates(
            string stem,
            string prefix)
        {
            if (_stemTemplates.TryGetValue(stem, out LootItem[] cached))
            {
                return cached;
            }

            LootItem[] found = UnityEngine.Object
                .FindObjectsByType<LootItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(item => item.Definition != null
                    && item.Definition.StableId.StartsWith(prefix)
                    && !item.Definition.RaisesAlarm)
                .OrderBy(item => item.Definition.StableId)
                .ToArray();
            _stemTemplates[stem] = found;
            return found;
        }

        /// <summary>
        /// Which rooms have already taken the staged originals rather than copies.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string>
            _claimedStems = new();

        /// <summary>
        /// Which kinds have already been given the town's one alarmed piece.
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string>
            _casedStems = new();

        /// <summary>
        /// Picks distinct kinds at random, up to <paramref name="wanted"/>.
        ///
        /// Distinct rather than a weighted roll with replacement: two of the same
        /// thing on two shelves is a room with one thing in it as far as the
        /// player's decision goes, and the whole point of the draw is that walking
        /// in does not tell you what is here.
        /// </summary>
        private static LootItem[] ChooseKinds(LootItem[] pool, int wanted)
        {
            var remaining = new List<LootItem>(pool);
            var chosen = new List<LootItem>();
            while (chosen.Count < wanted && remaining.Count > 0)
            {
                int index = Random.Range(0, remaining.Count);
                chosen.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            return chosen.ToArray();
        }

        /// <summary>
        /// A room's own copy of a piece.
        ///
        /// The session components are stripped and re-added by the map builder
        /// after every interior is done. An in-scene <c>NetworkObject</c> is
        /// identified by a hash Unity derives from the object's own scene
        /// identity, and a duplicated one carries the original's — two objects
        /// claiming to be the same object, which NGO resolves by dropping one of
        /// them at random.
        /// </summary>
        private static LootItem CopyPiece(LootItem original, int number)
        {
            var copy = UnityEngine.Object.Instantiate(
                original.gameObject,
                original.transform.parent);
            copy.name = $"Interior {number} {original.Definition.StableId}";

            var link = copy.GetComponent<
                PawsAndLoot.Integration.Network.NetworkLootLink>();
            if (link != null)
            {
                UnityEngine.Object.DestroyImmediate(link);
            }

            var netObject = copy.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObject != null)
            {
                UnityEngine.Object.DestroyImmediate(netObject);
            }

            return copy.GetComponent<LootItem>();
        }

        private static Vector3 FromPlan(
            Bounds inner,
            float floorTop,
            Vector2 mark)
        {
            float half = Mathf.Max(inner.extents.x, inner.extents.z)
                * PlanMargin;
            return new Vector3(
                inner.center.x + (mark.x - 0.5f) * 2f * half,
                floorTop,
                inner.center.z + (0.5f - mark.y) * 2f * half);
        }

        /// <summary>
        /// Finds somewhere inside the door with room to stand.
        ///
        /// A fixed step in from the doorway is not enough. These rooms are
        /// partitioned, and three metres past the front door of the house is
        /// the inside of a dividing wall — the player arrived wedged in it,
        /// which reads as the game being broken before they have moved.
        ///
        /// Candidates are tried from just inside the door and worked inward,
        /// with a little left and right, and the first one with clear air
        /// around it wins. Clear is measured against the collision mesh's own
        /// vertices in the band a body occupies, not by raycasting: a collider
        /// added moments ago is not reliably in the editor's physics scene, and
        /// vertices are in memory the moment the mesh is.
        /// </summary>
        private static Vector3 ClearSpotInside(
            Transform collision,
            Bounds inner,
            float floorTop,
            Vector3 mouth,
            Vector3 doorway)
        {
            const float Clearance = 0.85f;
            const float LowBand = 0.35f;
            const float HighBand = 1.6f;

            Vector3 fallback = mouth - doorway * EntryStandoff;
            if (collision == null)
            {
                return fallback;
            }

            var blockers = new List<Vector3>();
            foreach (MeshFilter filter in
                collision.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Transform space = filter.transform;
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 point = space.TransformPoint(local);
                    float height = point.y - floorTop;
                    if (height >= LowBand && height <= HighBand)
                    {
                        blockers.Add(
                            new Vector3(point.x, 0f, point.z));
                    }
                }
            }

            Vector3 across = new Vector3(-doorway.z, 0f, doorway.x);
            for (float inward = 1.6f; inward <= 9f; inward += 0.6f)
            {
                foreach (float sideways in new[]
                {
                    0f, 1f, -1f, 2f, -2f, 3f, -3f
                })
                {
                    Vector3 candidate = mouth
                        - doorway * inward
                        + across * sideways;
                    if (!inner.Contains(new Vector3(
                            candidate.x,
                            inner.center.y,
                            candidate.z)))
                    {
                        continue;
                    }

                    var flat = new Vector3(candidate.x, 0f, candidate.z);
                    bool clear = true;
                    foreach (Vector3 blocker in blockers)
                    {
                        if ((blocker - flat).sqrMagnitude
                            < Clearance * Clearance)
                        {
                            clear = false;
                            break;
                        }
                    }

                    if (clear)
                    {
                        return new Vector3(candidate.x, floorTop, candidate.z);
                    }
                }
            }

            Debug.LogWarning(
                "[MAP-008] No clear floor inside a doorway; falling back to a "
                + "fixed step in, which may be inside a wall.");
            return fallback;
        }

        /// <summary>
        /// How high the invisible wall round a room stands, in metres.
        ///
        /// Far taller than the wall you can see. The visible walls are cut low
        /// so the camera can look in, which also means a player who climbs onto
        /// a shelf is standing level with the top of the room and can walk off
        /// the edge of it. The barrier is what the room is actually made of;
        /// the model is what it looks like.
        /// </summary>
        private const float PerimeterHeight = 12f;

        /// <summary>
        /// How wide a hole is left at the door.
        /// </summary>
        private const float PerimeterGap = 3.2f;

        /// <summary>
        /// Boxes the room in so nothing can leave except through the door.
        ///
        /// Four slabs on the four sides, and the side with the door gets two
        /// with a gap between them. Invisible: this is collision, and a visible
        /// version would be a second set of walls inside the first.
        /// </summary>
        private static void BuildPerimeter(
            Transform holder,
            Bounds inner,
            float floorTop,
            Vector3 doorway)
        {
            const float Thickness = 0.6f;

            foreach (Vector3 side in new[]
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.left
            })
            {
                Vector3 across = new Vector3(-side.z, 0f, side.x);
                float span = Mathf.Abs(Vector3.Dot(inner.size, across))
                    + Thickness * 2f;
                float reach = Mathf.Abs(Vector3.Dot(inner.extents, side))
                    + Thickness * 0.5f;
                Vector3 middle =
                    new Vector3(inner.center.x, floorTop, inner.center.z)
                    + side * reach
                    + Vector3.up * (PerimeterHeight * 0.5f);

                bool hasDoor = Vector3.Dot(side, doorway) > 0.5f;
                if (!hasDoor)
                {
                    AddSlab(holder, middle, across, span, Thickness);
                    continue;
                }

                // Two pieces with the doorway between them.
                float wing = (span - PerimeterGap) * 0.5f;
                if (wing <= 0.1f)
                {
                    continue;
                }

                AddSlab(
                    holder,
                    middle + across * ((PerimeterGap + wing) * 0.5f),
                    across,
                    wing,
                    Thickness);
                AddSlab(
                    holder,
                    middle - across * ((PerimeterGap + wing) * 0.5f),
                    across,
                    wing,
                    Thickness);
            }
        }

        private static void AddSlab(
            Transform holder,
            Vector3 centre,
            Vector3 across,
            float span,
            float thickness)
        {
            var slab = new GameObject("Perimeter");
            slab.transform.SetParent(holder, false);
            slab.transform.position = centre;

            BoxCollider box = slab.AddComponent<BoxCollider>();
            bool alongX = Mathf.Abs(across.x) > 0.5f;
            box.size = alongX
                ? new Vector3(span, PerimeterHeight, thickness)
                : new Vector3(thickness, PerimeterHeight, span);
        }

        /// <summary>
        /// The widest continuous gap in one of a room's walls, in metres.
        ///
        /// Read off the collision mesh's triangles rather than by raycasting
        /// into it. A collider added moments ago in the editor is not reliably
        /// in the physics scene — that is what made an earlier version of this
        /// report a fourteen metre doorway in a thirteen metre wall — and
        /// triangles are in memory the moment the mesh is.
        ///
        /// The wall is divided into slots across its length. A slot with a
        /// triangle standing in it at door height is wall; a run of slots with
        /// nothing in them is a way through.
        /// </summary>
        private static float WidestGap(
            Transform collision,
            Bounds inner,
            float floorTop,
            Vector3 direction)
        {
            const int Slots = 40;
            const float LowGuard = 0.35f;
            const float HighGuard = 1.6f;

            if (collision == null)
            {
                return 0f;
            }

            Vector3 across = new Vector3(-direction.z, 0f, direction.x);
            float span = Mathf.Abs(Vector3.Dot(inner.size, across));
            float outEdge = Mathf.Abs(Vector3.Dot(inner.extents, direction));
            float slotWidth = span / Slots;
            var filled = new bool[Slots];

            foreach (MeshFilter filter in
                collision.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Transform space = filter.transform;
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 point = space.TransformPoint(local);
                    float height = point.y - floorTop;
                    if (height < LowGuard || height > HighGuard)
                    {
                        continue;
                    }

                    Vector3 offset = point
                        - new Vector3(inner.center.x, point.y, inner.center.z);

                    // Only the band along this wall, not the whole room.
                    if (Vector3.Dot(offset, direction) < outEdge - 1.2f)
                    {
                        continue;
                    }

                    float along = Vector3.Dot(offset, across) + span * 0.5f;
                    int slot = Mathf.FloorToInt(along / slotWidth);
                    if (slot >= 0 && slot < Slots)
                    {
                        filled[slot] = true;
                    }
                }
            }

            float run = 0f;
            float best = 0f;
            foreach (bool solid in filled)
            {
                run = solid ? 0f : run + slotWidth;
                best = Mathf.Max(best, run);
            }

            // A gap that reaches an end of the wall is the corner, not a door.
            return filled[0] && filled[Slots - 1] ? best : 0f;
        }

        /// <summary>
        /// Replaces each partition segment with a knee-to-chest-high one.
        ///
        /// A box per segment is safe here and would not have been for the shell: the
        /// model already splits its partitions either side of every doorway — five
        /// pieces named for the room and the side — so there is no hole to preserve.
        /// Measured rather than assumed, which is what the model probe is for.
        ///
        /// The original renderer is switched off rather than deleted. It belongs to a
        /// prefab instance, and deleting a child of one is recorded in the scene as an
        /// override; disabling one is a single boolean.
        /// </summary>
        private static int AddHalfHeightPartitions(
            IReadOnlyList<Renderer> parts,
            Transform holder,
            float floorTop,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cube,
            Material material)
        {
            int rebuilt = 0;
            foreach (Renderer part in parts)
            {
                if (!PartitionPrefixes.Any(prefix =>
                        part.name.StartsWith(prefix)))
                {
                    continue;
                }

                Bounds bounds = part.bounds;
                part.enabled = false;

                // Only the walls come back. The door frames and trim were there to
                // finish a full-height wall and have nothing to finish now.
                if (!part.name.StartsWith("IN_House1F_Wall"))
                {
                    continue;
                }

                GameObject low = cube(
                    $"{part.name} Low",
                    new Vector3(
                        bounds.center.x,
                        floorTop + PartitionHeight * 0.5f,
                        bounds.center.z),
                    new Vector3(
                        bounds.size.x,
                        PartitionHeight,
                        bounds.size.z),
                    material,
                    holder,
                    true);
                low.transform.SetParent(holder, true);
                rebuilt++;
            }

            return rebuilt;
        }

        private static int AddFurnitureColliders(
            IReadOnlyList<Renderer> parts,
            Transform holder)
        {
            var pieces = new Dictionary<string, Bounds>();
            foreach (Renderer part in parts)
            {
                string key = FurnitureKey(part.name);
                if (key == null)
                {
                    continue;
                }

                if (pieces.TryGetValue(key, out Bounds existing))
                {
                    existing.Encapsulate(part.bounds);
                    pieces[key] = existing;
                }
                else
                {
                    pieces[key] = part.bounds;
                }
            }

            int added = 0;
            foreach (KeyValuePair<string, Bounds> piece in pieces)
            {
                if (piece.Value.size.y < BlockingHeight)
                {
                    continue;
                }

                var box = new GameObject($"{piece.Key} Collider");
                box.transform.SetParent(holder, false);
                box.transform.position = piece.Value.center;
                BoxCollider collider = box.AddComponent<BoxCollider>();
                collider.size = piece.Value.size;
                added++;
            }

            return added;
        }

        /// <summary>
        /// Which piece of furniture a part belongs to, or null if it is not one.
        ///
        /// The numbered suffix is kept when there is one, so that six dining chairs
        /// stay six chairs. Merging them would produce a single box covering the
        /// whole table and everything round it.
        /// </summary>
        private static string FurnitureKey(string partName)
        {
            string prefix = FurniturePrefixes
                .Where(candidate => partName.StartsWith(candidate))
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault();
            if (prefix == null)
            {
                return null;
            }

            string rest = partName.Substring(prefix.Length).TrimStart('_');
            int cut = rest.IndexOf('_');
            string next = cut < 0 ? rest : rest.Substring(0, cut);
            return next.Length > 0 && next.All(char.IsDigit)
                ? $"{prefix}_{next}"
                : prefix;
        }

        /// <summary>
        /// A point outside the real house on one side, in the house's own space.
        ///
        /// The first version added a world-space <c>+Z</c> offset, which on a house
        /// turned to face the other way is the back garden. Local space is the only
        /// version that is right for every house, and it is the same mistake the
        /// door hinge made (ISSUE-039).
        /// </summary>
        private static Vector3 OutsidePoint(
            Transform house,
            HouseDoorSide side,
            float distance)
        {
            float sign = side == HouseDoorSide.Back ? -1f : 1f;

            // Walked inward until there is ground to stand on.
            //
            // A fixed offset is not enough. The houses on the far north row stand
            // against the boundary, so five metres in front of one is inside the
            // town wall: the exit put the player past the edge of the map with
            // nothing underneath, and they fell 21 m. It only showed up sometimes
            // because which house is "the first one" is instance-id order.
            //
            // Ground means a surface at about the height the house stands on. The
            // top of a 2 m wall is a hit too, and standing on the town wall is not
            // what this is for.
            foreach (float step in new[]
            {
                distance, distance - 0.8f, distance - 1.6f, distance - 2.4f
            })
            {
                if (step <= 1.5f)
                {
                    continue;
                }

                // Along the building's facing, at a distance in metres.
                //
                // TransformPoint would scale the distance by the building's
                // own scale, and the town's buildings are one-metre models
                // blown up twelvefold: "two point six metres in front" came out
                // as thirty-one, past the far kerb. Direction from the
                // transform, distance in world units.
                Vector3 candidate = house.position
                    + house.forward * (sign * step);
                if (!Physics.Raycast(
                        candidate + Vector3.up * 3f,
                        Vector3.down,
                        out RaycastHit hit,
                        8f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (Mathf.Abs(hit.point.y - house.position.y) > 0.6f)
                {
                    continue;
                }

                return new Vector3(
                    candidate.x,
                    house.position.y,
                    candidate.z);
            }

            Vector3 fallback = house.position
                + house.forward * (sign * 2.6f);
            Debug.LogWarning(
                $"[MAP-008] No ground in front of the {side} door of "
                + $"'{house.name}'. Falling back to 2.6 m, which may be inside "
                + "the porch.");
            return new Vector3(
                fallback.x,
                house.position.y,
                fallback.z);
        }

        private static void CreateInsideDoor(
            Transform room,
            HouseInterior interior,
            MatchRuntimeState matchRuntime,
            HouseDoorSide side,
            Vector3 position,
            int number)
        {
            var outward = new GameObject(
                $"Interior {number} Exit Door {side}");
            outward.transform.SetParent(room, false);
            outward.transform.position = position;

            // A slab in the doorway, and only in the doorway.
            //
            // The opening is a real hole in the wall: walking through it used to put
            // the player outside the room, where there is no floor, and they fell. So
            // the trigger has to be impossible to walk past — as tall as a jump, and
            // thick enough that a dash at 9 m/s cannot cross it between two frames
            // (1.2 m against 0.15 m of travel).
            //
            // No wider than the opening, though. The first version was 4.5 m across
            // and 2.2 m deep, which reached along the wall on both sides and out into
            // the room: walking past the inside of the front wall threw the player
            // into the street, and the entry point itself landed inside it, so coming
            // in bounced straight back out.
            BoxCollider trigger = outward.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.6f, 5f, 1.2f);
            trigger.center = new Vector3(0f, 1.5f, 0f);

            // Walked into, not pressed.
            //
            // Indoors the press belongs to the drawers and the display cases,
            // which is what a thief is in there for; spending it on the door
            // would mean standing in a doorway competing with the furniture.
            //
            // Safe to walk through now for a reason it was not before: the room
            // is boxed in by a barrier with a hole only where the door is, so
            // the only way to reach this trigger is to be leaving.
            outward.AddComponent<HouseDoorway>()
                .Configure(interior, false, matchRuntime, null, side, true);
        }

        /// <summary>
        /// The door on the real building: something to press, and a leaf that swings
        /// when it is used.
        /// </summary>
        private static void CreateEntrance(
            Transform house,
            HouseInterior interior,
            MatchRuntimeState matchRuntime,
            HouseDoorSide side)
        {
            HouseDoorLeaf leaf = CreateLeaf(house, side);

            var entrance = new GameObject($"House Entrance {side}");

            // Parented above the house, not to it, and placed in world space.
            //
            // These offsets used to be local, which was fine while every house
            // was one hand-built model at its authored size. The town's
            // buildings are one-metre models scaled up to fit their plots — by
            // twelve, in the usual case — and a local offset of 4.4 m became
            // fifty-three. Every doorway in the town was sitting off the edge
            // of the map with a twenty-metre trigger, which is why no door
            // could be pressed: not because the prompt was missing, but because
            // it was nowhere near the door.
            //
            // Nothing here moves, so hanging the doorways beside the buildings
            // rather than inside them costs nothing and takes the scale out of
            // the arithmetic entirely.
            entrance.transform.SetParent(house.parent, false);

            if (!TryGetWorldBounds(house, out Bounds shell))
            {
                Object.DestroyImmediate(entrance);
                Debug.LogWarning(
                    $"[MAP-008] '{house.name}' has nothing drawn, so its "
                    + $"{side} door has nowhere to be.");
                return;
            }

            // Along the building's own facing rather than along world Z. The
            // town turns buildings to face their street, and a door placed on
            // the south face of one that faces west is a door in a side wall.
            Vector3 out_ = side == HouseDoorSide.Back
                ? -house.forward
                : house.forward;
            float reach = Mathf.Abs(Vector3.Dot(shell.extents, out_))
                + DoorStandoff;
            entrance.transform.position = new Vector3(
                shell.center.x,
                shell.min.y + 0.5f,
                shell.center.z) + out_ * reach;

            SphereCollider trigger =
                entrance.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = DoorPromptRadius;
            entrance.AddComponent<HouseDoorway>()
                .Configure(interior, true, matchRuntime, leaf, side);
        }

        /// <summary>
        /// The swinging door itself.
        ///
        /// Nothing is reparented. Gathering the leaf's parts onto a hinge object
        /// needed the prefab unpacked, and unpacking writes all 186 parts of a house
        /// into the scene as real objects — it took the scene from 3.2 MB to 9.2 MB
        /// and put a fresh copy in history on every rebuild (ISSUE-038).
        /// <c>HouseDoorLeaf</c> turns the parts about a point instead.
        /// </summary>
        private static HouseDoorLeaf CreateLeaf(
            Transform house,
            HouseDoorSide side)
        {
            var door = new GameObject($"{side} Door");
            door.transform.SetParent(house, false);
            door.transform.localPosition = Vector3.zero;

            HouseDoorLeaf leaf = door.AddComponent<HouseDoorLeaf>();

            // Opposite signs, because the doors are on opposite walls. A positive
            // turn about up carries the free edge from +X toward -Z, which is
            // outward for the back door and into the house for the front one.
            float degrees = side == HouseDoorSide.Back ? 95f : -95f;
            leaf.Configure(house, degrees, side);

            if (leaf.PartCount == 0)
            {
                // Resolution fails silently, so the one thing the animation depends
                // on is asserted rather than assumed.
                Debug.LogError(
                    $"[MAP-008] '{house.name}' has no {side} door parts, so that "
                    + "door will not open.");
                Object.DestroyImmediate(door);
                return null;
            }

            return leaf;
        }
    }
}
