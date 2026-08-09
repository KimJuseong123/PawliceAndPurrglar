using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// Turns a position inside a room into the street address it happened at.
    ///
    /// Interiors are not separate scenes — they are rooms parked far away in the
    /// same one. So a coordinate taken inside a jeweller's is somewhere nobody
    /// can walk to, and anything that measures a distance to it, or points an
    /// arrow at it, ends up talking about an empty field south of the map. That
    /// is `ISSUE-073`, and **nothing logs when it happens**: the arrow renders,
    /// the sound plays, every component reports success.
    ///
    /// Extracted from <c>LootAlarm</c> so the second thing that needs it — how
    /// loud a theft sounds from where the officer is standing — asks the same
    /// question and gets the same answer. Two copies of this would drift, and
    /// the drift would be invisible in exactly the same way.
    /// </summary>
    public static class InteriorAddress
    {
        /// <summary>
        /// How far outside a room's floor still counts as being in it. A shelf
        /// against a wall reads as a fraction past the edge.
        /// </summary>
        private const float RoomMarginMeters = 1f;

        /// <summary>
        /// The town coordinate for something that happened at <paramref name="at"/>,
        /// or <paramref name="at"/> itself when it happened outdoors.
        ///
        /// The room's extents are measured in the room's own space. Measured on
        /// world axes a room that was rotated has its width and depth swapped,
        /// and a position at one end of it falls outside a box that should
        /// contain it.
        /// </summary>
        public static Vector3 TownPositionOf(Vector3 at)
        {
            foreach (HouseInterior room in
                Object.FindObjectsByType<HouseInterior>(
                    FindObjectsSortMode.None))
            {
                // The jail is a room by construction and by nothing else:
                // nothing is stolen there and it has no door to point at.
                if (room == null || room.IsJail)
                {
                    continue;
                }

                Vector3 local = room.transform.InverseTransformPoint(at);
                Vector2 half = room.FloorHalfExtents;
                if (Mathf.Abs(local.x) > half.x + RoomMarginMeters
                    || Mathf.Abs(local.z) > half.y + RoomMarginMeters)
                {
                    continue;
                }

                Vector3 outside = room.ExitPosition;

                // `ExitPosition` falls back to the room's own origin when the
                // building has no exit marker, and the room's origin is the
                // off-map coordinate this whole method exists to avoid. Said
                // out loud: answering with it would put the caller back where it
                // started and look like the fix had simply not worked.
                if ((outside - room.transform.position).sqrMagnitude < 0.01f)
                {
                    GameLogger.Error(
                        GameLogCategory.Loot,
                        $"Interior {room.InteriorId} has no exit marker, so "
                        + "anything that happens inside it cannot be given a "
                        + "street address.",
                        room);
                    return at;
                }

                return outside;
            }

            return at;
        }
    }
}
