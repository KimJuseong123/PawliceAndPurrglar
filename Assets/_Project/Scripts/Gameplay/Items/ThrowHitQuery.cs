using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// The authoritative character-only part of a throw query.
    ///
    /// World collision is solved by <see cref="ThrowTrajectorySolver"/>. This
    /// class only asks which opposing player lies in the already valid flight
    /// segment, keeping the officer's forgiveness bonus away from walls,
    /// furniture and other world colliders.
    /// </summary>
    public static class ThrowHitQuery
    {
        private const float DirectionEpsilon = 0.0001f;

        public static PlayerRoleIdentity FindClosest(
            PlayerRoleIdentity thrower,
            Vector3 origin,
            Vector3 direction,
            float fromDistance,
            float toDistance)
        {
            if (thrower == null)
            {
                return null;
            }

            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= DirectionEpsilon)
            {
                return null;
            }

            flatDirection.Normalize();
            float from = Mathf.Max(0f, fromDistance);
            float to = Mathf.Max(from, toDistance);
            PlayerRoleIdentity closest = null;
            float closestAlong = float.PositiveInfinity;

            foreach (PlayerRoleIdentity candidate in
                Object.FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (!IsValidTarget(thrower, candidate))
                {
                    continue;
                }

                Vector3 toTarget = candidate.transform.position - origin;
                toTarget.y = 0f;
                float along = Vector3.Dot(toTarget, flatDirection);
                if (along < from || along > to)
                {
                    continue;
                }

                float offAxis = (
                    toTarget - flatDirection * along).magnitude;
                float hitRadius = ThrowableCatalog.GetThrowHitRadius(
                    thrower.Role,
                    candidate.Role);
                if (offAxis > hitRadius || along >= closestAlong)
                {
                    continue;
                }

                closestAlong = along;
                closest = candidate;
            }

            return closest;
        }

        private static bool IsValidTarget(
            PlayerRoleIdentity thrower,
            PlayerRoleIdentity candidate)
        {
            return candidate != null
                && candidate != thrower
                && candidate.isActiveAndEnabled
                && candidate.Role != thrower.Role;
        }
    }
}
