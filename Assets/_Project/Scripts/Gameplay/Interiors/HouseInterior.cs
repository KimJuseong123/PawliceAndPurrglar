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
    /// Twice the floor plan, 16 m square. The walls stay 4 m rather than doubling
    /// with everything else — a 10 m ceiling reads as a warehouse, and the extra
    /// height buys nothing when the camera sits above the player. There is no
    /// ceiling at all, which is what stops a third-person camera clipping through
    /// a roof.
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
        /// Where somebody appears when they come in, just inside the doorway.
        /// </summary>
        [SerializeField]
        private Transform entryPoint;

        /// <summary>
        /// Where they appear when they leave: outside the real house in the town,
        /// clear of the door so they do not immediately re-enter.
        /// </summary>
        [SerializeField]
        private Transform exitPoint;

        /// <summary>
        /// Half-extents of the walkable floor, measured from the room centre.
        /// Loot is scattered inside this and nowhere else.
        /// </summary>
        [SerializeField]
        private Vector2 floorHalfExtents = new(7f, 7f);

        public int InteriorId => interiorId;
        public Vector3 EntryPosition =>
            entryPoint != null ? entryPoint.position : transform.position;
        public Vector3 ExitPosition =>
            exitPoint != null ? exitPoint.position : transform.position;
        public Vector2 FloorHalfExtents => floorHalfExtents;

        public void Configure(
            int configuredInteriorId,
            Transform configuredEntryPoint,
            Transform configuredExitPoint,
            Vector2 configuredFloorHalfExtents)
        {
            interiorId = configuredInteriorId;
            entryPoint = configuredEntryPoint;
            exitPoint = configuredExitPoint;
            floorHalfExtents = configuredFloorHalfExtents;
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
            return transform.position + offset;
        }
    }
}
