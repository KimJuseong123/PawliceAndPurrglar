using System.Collections.Generic;
using PawsAndLoot.Animation;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Builds the insides of the enterable houses, away from the town.
    ///
    /// Its own file because <c>GreyboxMapSetup</c> is already four thousand lines
    /// and this is a self-contained piece of the map: given a list of houses it
    /// produces a room for each, a door on each house, and a door back out.
    ///
    /// The rooms are twice the floor plan of the real buildings and have no
    /// ceiling. A house is about 8 m across, which is four character-widths and
    /// far too small to chase anybody through. The alternative — shrinking the
    /// characters on the way in — would have changed what every tuned distance in
    /// the game means at once: the 1.75 m arrest range would cover half a room, a
    /// 12 m throw would cross the whole house and the torch would light all of it.
    /// Growing the room instead leaves every rule alone.
    ///
    /// Not a separate scene, either. Scene-placed <c>NetworkObject</c>s do not
    /// survive a scene load (ISSUE-016), so an interior scene would mean
    /// rebuilding the session layer. A room that is simply somewhere else in the
    /// same scene needs none of that, and the two players being genuinely far
    /// apart in world space is what stops the officer arresting through a wall.
    /// </summary>
    internal static class HouseInteriorSetup
    {
        /// <summary>
        /// Twice the 8 m exterior. The walls stay 4 m rather than doubling with
        /// everything else: a 10 m ceiling reads as a warehouse and buys nothing
        /// when the camera sits above the player.
        /// </summary>
        private const float RoomSize = 16f;

        /// <summary>
        /// Tall enough that the camera cannot see over it.
        ///
        /// Four metres was not: the indoor camera sits above the player, so it
        /// looked straight over the walls and the neighbouring rooms were all
        /// visible at once. Nine metres puts the wall top well above the camera,
        /// which is what makes a room feel closed without a ceiling to clip
        /// through.
        /// </summary>
        private const float WallHeight = 9f;
        private const float WallThickness = 0.4f;
        private const float DoorGap = 3.2f;

        /// <summary>
        /// Well south of the map, which runs to z = -22. Far enough that nothing
        /// in the town can see or reach it.
        /// </summary>
        private const float RowZ = -70f;
        private const float RowStartX = -28f;

        /// <summary>
        /// Well clear of each other. The tall walls are what actually hide the
        /// neighbours; this is margin on top of that.
        /// </summary>
        private const float Spacing = 40f;

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

            Material floor = materialFactory(
                "Greybox_InteriorFloor",
                new Color(0.42f, 0.36f, 0.31f));
            Material wall = materialFactory(
                "Greybox_InteriorWall",
                new Color(0.74f, 0.71f, 0.66f));
            Material furniture = materialFactory(
                "Greybox_InteriorFurniture",
                new Color(0.32f, 0.29f, 0.34f));

            int built = 0;
            for (int index = 0; index < houses.Count; index++)
            {
                if (houses[index] == null)
                {
                    continue;
                }

                BuildOne(
                    root,
                    matchRuntime,
                    index,
                    houses[index],
                    new Vector3(
                        RowStartX + index * Spacing,
                        0f,
                        RowZ),
                    floor,
                    wall,
                    furniture,
                    cubeFactory,
                    childFactory);
                built++;
            }

            Debug.Log(
                $"[MAP-008] {built} house interiors built, {RoomSize:0.#}m "
                + "square with no ceiling.");
        }

        private static void BuildOne(
            Transform root,
            MatchRuntimeState matchRuntime,
            int index,
            Transform house,
            Vector3 centre,
            Material floorMaterial,
            Material wallMaterial,
            Material furnitureMaterial,
            System.Func<string, Vector3, Vector3, Material, Transform, bool,
                GameObject> cube,
            System.Func<string, Transform, Transform> child)
        {
            int number = index + 1;
            Transform room = child($"Interior {number}", root);
            room.position = centre;

            float half = RoomSize * 0.5f;
            // A thick slab, not a sheet. A 0.2 m floor is thin enough for a fast
            // enough fall to cross in one frame, and a character who has picked up
            // speed does exactly that.
            cube(
                $"Interior {number} Floor",
                centre + new Vector3(0f, -0.75f, 0f),
                new Vector3(RoomSize, 1.5f, RoomSize),
                floorMaterial,
                room,
                true);

            float wallY = WallHeight * 0.5f;
            cube(
                $"Interior {number} Wall North",
                centre + new Vector3(0f, wallY, half),
                new Vector3(RoomSize, WallHeight, WallThickness),
                wallMaterial,
                room,
                true);
            cube(
                $"Interior {number} Wall East",
                centre + new Vector3(half, wallY, 0f),
                new Vector3(WallThickness, WallHeight, RoomSize),
                wallMaterial,
                room,
                true);
            cube(
                $"Interior {number} Wall West",
                centre + new Vector3(-half, wallY, 0f),
                new Vector3(WallThickness, WallHeight, RoomSize),
                wallMaterial,
                room,
                true);

            // The south wall has a gap in it for the way out, so the exit is
            // where a player would look for it rather than an invisible spot.
            float sideWidth = (RoomSize - DoorGap) * 0.5f;
            float sideOffset = (DoorGap + sideWidth) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                cube(
                    $"Interior {number} Wall South "
                    + (side < 0f ? "L" : "R"),
                    centre + new Vector3(side * sideOffset, wallY, -half),
                    new Vector3(sideWidth, WallHeight, WallThickness),
                    wallMaterial,
                    room,
                    true);
            }

            // Blocks to break up the floor. Furniture is what makes a room
            // somewhere to be chased through rather than an empty box, and it
            // gives the scattered loot something to sit behind.
            (Vector3 offset, Vector3 scale)[] props =
            {
                (new Vector3(-4.5f, 0.5f, 3.5f), new Vector3(3f, 1f, 1.6f)),
                (new Vector3(4.2f, 0.6f, 4f), new Vector3(2f, 1.2f, 2f)),
                (new Vector3(5f, 0.4f, -3f), new Vector3(1.4f, 0.8f, 3.4f)),
                (new Vector3(-5f, 0.9f, -3.5f), new Vector3(2.4f, 1.8f, 1f))
            };
            foreach ((Vector3 offset, Vector3 scale) in props)
            {
                cube(
                    $"Interior {number} Furniture",
                    centre + offset,
                    scale,
                    furnitureMaterial,
                    room,
                    true);
            }

            Transform entry = child($"Interior {number} Entry", room);
            entry.position = centre + new Vector3(0f, 0f, -half + 2f);

            // Out in front of the real house, clear of its own door trigger so
            // stepping out does not immediately offer to go back in.
            // Out at the front of the real house.
            //
            // Measured, not assumed: on this model the porch floor is at z = +3.5
            // and the front door leaf at z = +3.1, so the front is +Z. The first
            // version used -Z and quietly put both the entrance and the exit at
            // the back door.
            Transform exit = child($"Interior {number} Exit", room);
            exit.position = house.position + new Vector3(0f, 0f, 6f);

            HouseInterior interior =
                room.gameObject.AddComponent<HouseInterior>();
            interior.Configure(
                number,
                entry,
                exit,
                new Vector2(half - 1.5f, half - 1.5f));

            var outward = new GameObject($"Interior {number} Exit Door");
            outward.transform.SetParent(room, false);
            outward.transform.position =
                centre + new Vector3(0f, 0.5f, -half + 0.7f);
            SphereCollider outTrigger =
                outward.AddComponent<SphereCollider>();
            outTrigger.isTrigger = true;
            outTrigger.radius = 1.5f;
            outward.AddComponent<HouseDoorway>()
                .Configure(interior, false, matchRuntime);

            CreateEntrance(house, interior, matchRuntime);
        }

        /// <summary>
        /// The door on the real building: something to press, and a leaf that
        /// swings when it is used.
        /// </summary>
        private static void CreateEntrance(
            Transform house,
            HouseInterior interior,
            MatchRuntimeState matchRuntime)
        {
            HouseDoorLeaf leaf = TryCreateHinge(house);

            var entrance = new GameObject("House Entrance");
            entrance.transform.SetParent(house, false);
            // On the porch, which on these models faces +Z.
            entrance.transform.localPosition = new Vector3(0f, 0.5f, 4.6f);
            SphereCollider trigger =
                entrance.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.7f;
            entrance.AddComponent<HouseDoorway>()
                .Configure(interior, true, matchRuntime, leaf);
        }

        /// <summary>
        /// Hands the door animator the house to find its own parts in.
        ///
        /// It used to gather the five door parts onto a hinge object here, which
        /// meant unpacking the prefab first — a prefab instance refuses to have its
        /// children reparented. That worked and cost 6 MB of scene per rebuild,
        /// because unpacking writes all 186 parts of every house into the scene as
        /// real objects. <c>HouseDoorLeaf</c> now turns the parts about a point
        /// instead, so the houses stay packed and this hands over a reference.
        /// </summary>
        private static HouseDoorLeaf TryCreateHinge(Transform house)
        {
            var door = new GameObject("Front Door");
            door.transform.SetParent(house, false);
            door.transform.localPosition = Vector3.zero;

            HouseDoorLeaf leaf = door.AddComponent<HouseDoorLeaf>();
            // Negative, so it opens outward. A positive rotation about up carries
            // the free edge from +X toward -Z, which for a door on the +Z wall is
            // into the house.
            leaf.Configure(house, -95f);

            if (leaf.PartCount == 0)
            {
                // Resolution fails silently, so the one thing the animation
                // depends on is asserted rather than assumed.
                Debug.LogError(
                    $"[MAP-008] '{house.name}' has no front door parts, so its "
                    + "door will not open.");
                Object.DestroyImmediate(door);
                return null;
            }

            return leaf;
        }

    }
}
