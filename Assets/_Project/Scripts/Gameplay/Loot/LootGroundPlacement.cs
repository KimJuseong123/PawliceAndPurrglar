using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    public static class LootGroundPlacement
    {
        private const int MaxHits = 32;
        private const float ProbeHeight = 3f;
        private const float ProbeDistance = 20f;

        private static readonly RaycastHit[] Hits =
            new RaycastHit[MaxHits];

        public static bool TryFindSurface(
            Vector3 requestedPosition,
            Transform ignoredRoot,
            out Vector3 surfacePosition)
        {
            Vector3 origin =
                requestedPosition + Vector3.up * ProbeHeight;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                Hits,
                ProbeDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            surfacePosition = default;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = Hits[index];
                if (hit.collider == null
                    || (ignoredRoot != null
                        && hit.collider.transform.IsChildOf(ignoredRoot))
                    || hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                surfacePosition = hit.point;
            }

            return !float.IsPositiveInfinity(nearestDistance);
        }
    }
}
