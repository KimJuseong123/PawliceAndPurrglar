using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// One house's inside, built at full character scale away from the town.
    ///
    /// The houses are about 8 m across, which is four character-widths — too
    /// small to chase anybody through. The alternative was shrinking the
    /// characters when they step inside, and that would have re-scaled the
    /// meaning of every tuned distance in the game at once: the 1.75 m arrest
    /// range would cover half a room, a 12 m throw would cross the whole house,
    /// and the torch would light all of it. So the room grows instead and nothing
    /// else has to change.
    ///
    /// The room is the house model itself, enlarged. It was a greybox box with four
    /// blocks in it until the model was actually measured and turned out to have a
    /// furnished interior already — a bathroom, a bedroom, a kitchen, a living room
    /// and a dining room, 111 parts of it, behind its own partition walls. Building
    /// a worse room by hand next to a better one that already existed was the wrong
    /// trade, and the enlarged model has no roof, which is what stops a
    /// third-person camera clipping through one.
    ///
    /// Two doors, front and back. One door makes a room a trap: the thief who goes
    /// in has one way out and the officer only has to wait on the porch. Both ends
    /// of the room are therefore reachable, and which door you came in by decides
    /// which end you appear at.
    ///
    /// Placed outside the map rather than in a separate scene. Scene-placed
    /// <c>NetworkObject</c>s do not survive a scene load (ISSUE-016), so an
    /// interior scene per house would mean rebuilding the session layer; a room
    /// that is simply somewhere else in the same scene needs none of that, and
    /// the players' world positions being far apart is exactly what stops the
    /// officer arresting through a wall.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseInterior : MonoBehaviour
    {
        [SerializeField]
        private int interiorId;

        /// <summary>
        /// Where somebody appears when they come in the front, just inside the
        /// doorway.
        /// </summary>
        [SerializeField]
        private Transform frontEntryPoint;

        /// <summary>
        /// The same for the back door, at the other end of the room.
        /// </summary>
        [SerializeField]
        private Transform backEntryPoint;

        /// <summary>
        /// Where they appear when they leave by the front: outside the real house in
        /// the town, clear of the door so they do not immediately re-enter.
        /// </summary>
        [SerializeField]
        private Transform frontExitPoint;

        [SerializeField]
        private Transform backExitPoint;

        /// <summary>
        /// Half-extents of the walkable floor, measured from the room centre.
        /// Loot is scattered inside this and nowhere else.
        /// </summary>
        [SerializeField]
        private Vector2 floorHalfExtents = new(7f, 7f);

        /// <summary>
        /// The height of the floor the furniture stands on, so scattered loot lands
        /// on it rather than at the room object's own origin.
        /// </summary>
        [SerializeField]
        private float floorHeight;

        public int InteriorId => interiorId;
        public Vector2 FloorHalfExtents => floorHalfExtents;
        public float FloorHeight => floorHeight;

        /// <summary>
        /// The front door's points. Kept as plain properties because most callers —
        /// the loot scatter, the dog's pointer — only need somewhere in the room and
        /// have no opinion about which door.
        /// </summary>
        public Vector3 EntryPosition => EntryPositionFor(HouseDoorSide.Front);
        public Vector3 ExitPosition => ExitPositionFor(HouseDoorSide.Front);

        /// <summary>
        /// Which way a player faces on arriving inside.
        ///
        /// Taken from the entry marker, which the generator turns to look at
        /// the far wall. It used to be left to whatever the player happened to
        /// be facing in the street, so the same door gave a different view
        /// every time and the room had to be found again on each visit.
        /// </summary>
        public Quaternion EntryFacingFor(HouseDoorSide side)
        {
            Transform point = side == HouseDoorSide.Back
                ? backEntryPoint
                : frontEntryPoint;
            point ??= frontEntryPoint;
            return point != null ? point.rotation : transform.rotation;
        }

        public Vector3 EntryPositionFor(HouseDoorSide side)
        {
            Transform point = side == HouseDoorSide.Back
                ? backEntryPoint
                : frontEntryPoint;
            return point != null
                ? point.position
                : (frontEntryPoint != null
                    ? frontEntryPoint.position
                    : transform.position);
        }

        public Vector3 ExitPositionFor(HouseDoorSide side)
        {
            Transform point = side == HouseDoorSide.Back
                ? backExitPoint
                : frontExitPoint;
            return point != null
                ? point.position
                : (frontExitPoint != null
                    ? frontExitPoint.position
                    : transform.position);
        }

        public void Configure(
            int configuredInteriorId,
            Transform configuredFrontEntry,
            Transform configuredBackEntry,
            Transform configuredFrontExit,
            Transform configuredBackExit,
            Vector2 configuredFloorHalfExtents,
            float configuredFloorHeight)
        {
            interiorId = configuredInteriorId;
            frontEntryPoint = configuredFrontEntry;
            backEntryPoint = configuredBackEntry;
            frontExitPoint = configuredFrontExit;
            backExitPoint = configuredBackExit;
            floorHalfExtents = configuredFloorHalfExtents;
            floorHeight = configuredFloorHeight;
        }

        /// <summary>
        /// A spot on the floor for a random scatter.
        ///
        /// The caller supplies the random source, because who rolls it matters:
        /// the host decides and tells the other machine, so that two screens
        /// cannot disagree about where a piece of loot is lying.
        /// </summary>
        public Vector3 SampleFloorPoint(System.Random random, float margin)
        {
            float usableX = Mathf.Max(0.5f, floorHalfExtents.x - margin);
            float usableZ = Mathf.Max(0.5f, floorHalfExtents.y - margin);
            var offset = new Vector3(
                (float)(random.NextDouble() * 2.0 - 1.0) * usableX,
                0f,
                (float)(random.NextDouble() * 2.0 - 1.0) * usableZ);
            return new Vector3(
                transform.position.x + offset.x,
                floorHeight,
                transform.position.z + offset.z);
        }
    }
}
