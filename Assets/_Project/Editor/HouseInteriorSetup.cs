using System.Collections.Generic;
using System.Linq;
using PawsAndLoot.Animation;
using PawsAndLoot.Gameplay.Interiors;
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
        private const string InteriorStem =
            "building_house_1f_with_interior";

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
        /// A grid rather than a row. Nineteen rooms in a line reach 700 m from the
        /// town and put coordinates far from everything else for no benefit.
        /// </summary>
        private const int Columns = 5;
        private const float SpacingX = 36f;
        private const float SpacingZ = 36f;

        /// <summary>
        /// Well south of the map, which runs to z = -22.
        /// </summary>
        private const float RowZ = -70f;
        private const float RowStartX = -28f;

        /// <summary>
        /// Parts that make the room solid. Given mesh colliders, not boxes: these
        /// walls have door-shaped holes in them, and a box from their bounds would
        /// seal every room off from the next. A mesh collider keeps the holes.
        /// </summary>
        private static readonly string[] SolidPrefixes =
        {
            "BD_House1F_Wall_",
            "IN_House1F_Wall",
            "BD_House1F_Foundation"
        };

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
            IReadOnlyList<Transform> houses,
            System.Func<string, Color, Material> materialFactory,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cubeFactory,
            System.Func<string, Transform, Transform> childFactory)
        {
            if (houses == null || houses.Count == 0)
            {
                return;
            }

            Transform root = childFactory("House Interiors", parent);
            root.localPosition = Vector3.zero;

            Material floorMaterial = materialFactory(
                "Greybox_InteriorFloor",
                new Color(0.42f, 0.36f, 0.31f));

            int built = 0;
            int solids = 0;
            int props = 0;
            for (int index = 0; index < houses.Count; index++)
            {
                if (houses[index] == null)
                {
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
                        houses[index],
                        centre,
                        floorMaterial,
                        cubeFactory,
                        childFactory,
                        ref solids,
                        ref props))
                {
                    built++;
                }
            }

            Debug.Log(
                $"[MAP-008] {built} house interiors built from the model at "
                + $"{InteriorScale:0.#}x, with {solids} wall colliders and "
                + $"{props} furniture colliders, front and back doors on each.");
        }

        private static bool BuildOne(
            Transform root,
            MatchRuntimeState matchRuntime,
            int index,
            Transform house,
            Vector3 centre,
            Material floorMaterial,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cube,
            System.Func<string, Transform, Transform> child,
            ref int solids,
            ref int props)
        {
            int number = index + 1;
            Transform room = child($"Interior {number}", root);
            room.position = centre;

            Vector3 size = PlaceholderModelLibrary
                .TryInstantiateBuildingSized(
                    InteriorStem,
                    room,
                    centre,
                    0f,
                    0f,
                    InteriorScale);
            if (size.y <= 0f)
            {
                Debug.LogError(
                    $"[MAP-008] Interior {number} could not be built: "
                    + $"'{InteriorStem}' did not instantiate.");
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
                    $"[MAP-008] Interior {number} is missing the walls or doors "
                    + "it is measured from.");
                Object.DestroyImmediate(room.gameObject);
                return false;
            }

            Transform colliders = child($"Interior {number} Colliders", room);
            colliders.position = centre;
            solids += AddSolidColliders(parts, colliders);
            props += AddFurnitureColliders(parts, colliders);

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

            // Just inside each door, far enough in that the doorway is behind you.
            Transform frontEntry = child($"Interior {number} Entry Front", room);
            frontEntry.position = new Vector3(
                frontDoor.x,
                floorTop,
                inner.max.z - 1.6f);
            Transform backEntry = child($"Interior {number} Entry Back", room);
            backEntry.position = new Vector3(
                backDoor.x,
                floorTop,
                inner.min.z + 1.6f);

            // Out at the real house, on the matching side.
            Transform frontExit = child($"Interior {number} Exit Front", room);
            frontExit.position = OutsidePoint(house, HouseDoorSide.Front, 5.4f);
            Transform backExit = child($"Interior {number} Exit Back", room);
            backExit.position = OutsidePoint(house, HouseDoorSide.Back, 4.6f);

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

            CreateInsideDoor(
                room,
                interior,
                matchRuntime,
                HouseDoorSide.Front,
                new Vector3(frontDoor.x, floorTop + 1f, inner.max.z - 0.7f),
                number);
            CreateInsideDoor(
                room,
                interior,
                matchRuntime,
                HouseDoorSide.Back,
                new Vector3(backDoor.x, floorTop + 1f, inner.min.z + 0.7f),
                number);

            CreateEntrance(house, interior, matchRuntime, HouseDoorSide.Front);
            CreateEntrance(house, interior, matchRuntime, HouseDoorSide.Back);
            return true;
        }

        /// <summary>
        /// Reads the room's floor height, its inside extents and where its two doors
        /// are, from the model rather than from constants.
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

            Renderer Find(string name) =>
                parts.FirstOrDefault(part => part.name == name);

            Renderer foundation = Find("BD_House1F_Foundation");
            Renderer left = Find("BD_House1F_Wall_Left");
            Renderer right = Find("BD_House1F_Wall_Right");
            Renderer front = Find("BD_House1F_Wall_Front");
            Renderer back = Find("BD_House1F_Wall_Back");
            Renderer frontLeaf = Find("BD_House1F_Door_Front_Leaf");
            Renderer backLeaf = Find("BD_House1F_Door_Back_Leaf");
            if (foundation == null || left == null || right == null
                || front == null || back == null
                || frontLeaf == null || backLeaf == null)
            {
                return false;
            }

            floorTop = foundation.bounds.max.y;
            frontDoor = frontLeaf.bounds.center;
            backDoor = backLeaf.bounds.center;

            // The space between the walls, not the space they occupy.
            float innerMinX = Mathf.Min(left.bounds.min.x, right.bounds.min.x);
            float innerMaxX = Mathf.Max(left.bounds.max.x, right.bounds.max.x);
            float innerMinZ = Mathf.Min(front.bounds.min.z, back.bounds.min.z);
            float innerMaxZ = Mathf.Max(front.bounds.max.z, back.bounds.max.z);
            float wallX = Mathf.Min(
                left.bounds.size.x,
                right.bounds.size.x);
            float wallZ = Mathf.Min(
                front.bounds.size.z,
                back.bounds.size.z);
            inner = new Bounds(
                new Vector3(
                    (innerMinX + innerMaxX) * 0.5f,
                    floorTop,
                    (innerMinZ + innerMaxZ) * 0.5f),
                new Vector3(
                    Mathf.Max(0f, innerMaxX - innerMinX - wallX * 2f),
                    0f,
                    Mathf.Max(0f, innerMaxZ - innerMinZ - wallZ * 2f)));
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
        private static int AddSolidColliders(
            IReadOnlyList<Renderer> parts,
            Transform holder)
        {
            int added = 0;
            foreach (Renderer part in parts)
            {
                if (!SolidPrefixes.Any(prefix =>
                        part.name.StartsWith(prefix)))
                {
                    continue;
                }

                var filter = part.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var shell = new GameObject($"{part.name} Collider");
                shell.transform.SetParent(holder, false);
                shell.transform.SetPositionAndRotation(
                    part.transform.position,
                    part.transform.rotation);
                shell.transform.localScale = part.transform.lossyScale;
                MeshCollider collider =
                    shell.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                added++;
            }

            return added;
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
            var local = new Vector3(0f, 0f, sign * distance);
            Vector3 world = house.TransformPoint(local);
            return new Vector3(world.x, house.position.y, world.z);
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
            SphereCollider trigger =
                outward.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            outward.AddComponent<HouseDoorway>()
                .Configure(interior, false, matchRuntime, null, side);
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
            entrance.transform.SetParent(house, false);

            // On the step, in the house's own space. The porch is at +Z and the back
            // step at -Z — measured, after the first version put both the way in and
            // the way out at the back door (ISSUE-034).
            float sign = side == HouseDoorSide.Back ? -1f : 1f;
            entrance.transform.localPosition = new Vector3(
                0f,
                0.5f,
                sign * (side == HouseDoorSide.Back ? 3.7f : 4.4f));
            SphereCollider trigger =
                entrance.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.7f;
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
