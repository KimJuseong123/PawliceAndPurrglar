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
            { "TwoStorey", "interior_house03" }
        };

        /// <summary>
        /// Where a caught thief waits out the sentence.
        ///
        /// Its own room rather than a corner of the police station, so the
        /// sentence is somewhere with nothing in it and no way out until the
        /// clock says so.
        /// </summary>
        internal const string JailStem = "interior_jail";

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
        private const float SpacingX = 36f;
        private const float SpacingZ = 36f;

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

            Vector3 size = PlaceholderModelLibrary
                .TryInstantiateBuildingSized(
                    stem,
                    room,
                    centre,
                    RoomX,
                    RoomZ);
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
            solids += AddSolidColliders(parts, colliders);
            props += AddFurnitureColliders(parts, colliders);
            partitions += AddHalfHeightPartitions(
                parts,
                colliders,
                floorTop,
                cube,
                partitionMaterial);

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
            Transform frontEntry = child($"Interior {number} Entry Front", room);
            frontEntry.position = new Vector3(
                frontDoor.x,
                floorTop,
                inner.max.z - 3.2f);
            Transform backEntry = child($"Interior {number} Entry Back", room);
            backEntry.position = new Vector3(
                backDoor.x,
                floorTop,
                inner.min.z + 3.2f);

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
                new Vector3(frontDoor.x, floorTop, inner.max.z - 0.2f),
                number);
            CreateInsideDoor(
                room,
                interior,
                matchRuntime,
                HouseDoorSide.Back,
                new Vector3(backDoor.x, floorTop, inner.min.z + 0.2f),
                number);

            // Whichever face the camera is behind comes away as a whole. Given the
            // room's measured inside rather than a list of parts: it works out which
            // face each piece belongs to from where the piece is, so windows and
            // siding leave with the wall they are bolted to.
            room.gameObject.AddComponent<InteriorShellScreen>()
                .Configure(
                    inner.center - room.position,
                    new Vector2(inner.extents.x, inner.extents.z),
                    floorTop);

            CreateEntrance(house, interior, matchRuntime, HouseDoorSide.Front);
            CreateEntrance(house, interior, matchRuntime, HouseDoorSide.Back);
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

            // Automatic, so getting out is walking out. Indoors the press belongs to
            // the wardrobes and drawers.
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
