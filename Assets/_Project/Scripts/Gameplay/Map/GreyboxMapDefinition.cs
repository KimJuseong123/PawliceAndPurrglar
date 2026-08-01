using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Map
{
    public enum GreyboxLocationId
    {
        PoliceSpawn = 0,
        ThiefSpawn = 1,
        Supermarket = 2,
        Bookstore = 3,
        JewelryStore = 4,
        RaccoonMarket = 5,
        CentralPlaza = 6
    }

    [Serializable]
    public sealed class GreyboxLocationReference
    {
        [SerializeField]
        private GreyboxLocationId id;

        [SerializeField]
        private Transform anchor;

        public GreyboxLocationReference(
            GreyboxLocationId id,
            Transform anchor)
        {
            this.id = id;
            this.anchor = anchor;
        }

        public GreyboxLocationId Id => id;
        public Transform Anchor => anchor;
    }

    [Serializable]
    public sealed class GreyboxRouteReference
    {
        [SerializeField]
        private string routeId;

        [SerializeField]
        private GreyboxLocationId from;

        [SerializeField]
        private GreyboxLocationId to;

        [SerializeField, Min(0f)]
        private float minimumClearWidth;

        [SerializeField]
        private List<Transform> waypoints;

        public GreyboxRouteReference(
            string routeId,
            GreyboxLocationId from,
            GreyboxLocationId to,
            float minimumClearWidth,
            IEnumerable<Transform> waypoints)
        {
            this.routeId = routeId;
            this.from = from;
            this.to = to;
            this.minimumClearWidth = minimumClearWidth;
            this.waypoints = new List<Transform>(waypoints);
        }

        public string RouteId => routeId;
        public GreyboxLocationId From => from;
        public GreyboxLocationId To => to;
        public float MinimumClearWidth => minimumClearWidth;
        public IReadOnlyList<Transform> Waypoints => waypoints;

        public bool Connects(
            GreyboxLocationId first,
            GreyboxLocationId second)
        {
            return from == first && to == second
                || from == second && to == first;
        }

        public float CalculateDistance()
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                return 0f;
            }

            float distance = 0f;
            for (int index = 1; index < waypoints.Count; index++)
            {
                distance += Vector3.Distance(
                    waypoints[index - 1].position,
                    waypoints[index].position);
            }

            return distance;
        }
    }

    public sealed class GreyboxObstacle : MonoBehaviour
    {
    }

    public sealed class GreyboxMapDefinition : MonoBehaviour
    {
        public const float RequiredMinimumClearWidth = 2.4f;
        public const string CrossingRouteId = "PoliceToThief_MainLoop";

        private static readonly GreyboxLocationId[] RequiredLocations =
        {
            GreyboxLocationId.PoliceSpawn,
            GreyboxLocationId.ThiefSpawn,
            GreyboxLocationId.Supermarket,
            GreyboxLocationId.Bookstore,
            GreyboxLocationId.JewelryStore,
            GreyboxLocationId.RaccoonMarket,
            GreyboxLocationId.CentralPlaza
        };

        private static readonly (GreyboxLocationId First, GreyboxLocationId Second)[]
            RequiredAlternativePairs =
            {
                (GreyboxLocationId.PoliceSpawn, GreyboxLocationId.JewelryStore),
                (GreyboxLocationId.ThiefSpawn, GreyboxLocationId.JewelryStore),
                (GreyboxLocationId.JewelryStore, GreyboxLocationId.RaccoonMarket),
                (GreyboxLocationId.Supermarket, GreyboxLocationId.Bookstore)
            };

        [Header("Dimensions")]
        [SerializeField, Min(1f)]
        private float mapWidthMeters = 56f;

        [SerializeField, Min(1f)]
        private float mapDepthMeters = 44f;

        /// <summary>
        /// Whether this map was built without the village's environment content.
        ///
        /// MAP-002 is a bare greybox: roads, blocks and a coordinate grid, and none
        /// of the destinations, pickups or hiding places <c>Game.unity</c> carries. A
        /// map with no locations is otherwise indistinguishable from one whose builder
        /// failed halfway, so the builder says which it is rather than leaving the
        /// validator to guess from an empty list.
        /// </summary>
        [SerializeField]
        private bool environmentContentCleared;

        [Header("Required Locations")]
        [SerializeField]
        private List<GreyboxLocationReference> locations = new();

        [Header("Route Network")]
        [SerializeField]
        private List<GreyboxRouteReference> routes = new();

        [Header("Traversal Features")]
        [SerializeField]
        private List<Transform> rooftops = new();

        [SerializeField]
        private List<Transform> ladders = new();

        [SerializeField]
        private List<Transform> trashBins = new();

        public float MapWidthMeters => mapWidthMeters;
        public float MapDepthMeters => mapDepthMeters;
        public IReadOnlyList<GreyboxLocationReference> Locations => locations;
        public IReadOnlyList<GreyboxRouteReference> Routes => routes;
        public IReadOnlyList<Transform> Rooftops => rooftops;
        public IReadOnlyList<Transform> Ladders => ladders;
        public IReadOnlyList<Transform> TrashBins => trashBins;
        public bool EnvironmentContentCleared => environmentContentCleared;

        /// <summary>
        /// Sets the ground size without touching the location and route lists.
        ///
        /// <see cref="Configure"/> replaces everything at once, which is what the
        /// village builder wants. A map that has no locations to configure still has
        /// a size, and passing empty lists to say so would clear anything a later
        /// pass had already put there.
        /// </summary>
        public void ResizeDimensions(float widthMeters, float depthMeters)
        {
            mapWidthMeters = Mathf.Max(1f, widthMeters);
            mapDepthMeters = Mathf.Max(1f, depthMeters);
        }

        /// <summary>
        /// Records that this map deliberately carries no environment content.
        /// </summary>
        public void MarkEnvironmentContentCleared()
        {
            environmentContentCleared = true;
        }

        public void Configure(
            float widthMeters,
            float depthMeters,
            IEnumerable<GreyboxLocationReference> locationReferences,
            IEnumerable<GreyboxRouteReference> routeReferences,
            IEnumerable<Transform> rooftopReferences,
            IEnumerable<Transform> ladderReferences,
            IEnumerable<Transform> trashBinReferences)
        {
            mapWidthMeters = widthMeters;
            mapDepthMeters = depthMeters;
            locations = new List<GreyboxLocationReference>(locationReferences);
            routes = new List<GreyboxRouteReference>(routeReferences);
            rooftops = new List<Transform>(rooftopReferences);
            ladders = new List<Transform>(ladderReferences);
            trashBins = new List<Transform>(trashBinReferences);
        }

        public Transform GetLocation(GreyboxLocationId id)
        {
            GreyboxLocationReference location =
                locations.FirstOrDefault(candidate => candidate.Id == id);
            if (location?.Anchor == null)
            {
                throw new InvalidOperationException(
                    $"Greybox map is missing location '{id}'.");
            }

            return location.Anchor;
        }

        public GreyboxRouteReference GetRoute(string routeId)
        {
            GreyboxRouteReference route = routes.FirstOrDefault(
                candidate => string.Equals(
                    candidate.RouteId,
                    routeId,
                    StringComparison.Ordinal));
            return route ?? throw new InvalidOperationException(
                $"Greybox map is missing route '{routeId}'.");
        }

        public int CountRoutesBetween(
            GreyboxLocationId first,
            GreyboxLocationId second)
        {
            return routes.Count(route => route.Connects(first, second));
        }

        public float EstimateCrossingSeconds(float moveSpeedMetersPerSecond)
        {
            if (moveSpeedMetersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moveSpeedMetersPerSecond),
                    moveSpeedMetersPerSecond,
                    "Movement speed must be positive.");
            }

            return GetRoute(CrossingRouteId).CalculateDistance()
                / moveSpeedMetersPerSecond;
        }

        public void ValidateOrThrow(bool validatePhysicsClearance = true)
        {
            if (mapWidthMeters <= 0f || mapDepthMeters <= 0f)
            {
                throw new InvalidOperationException(
                    "Greybox map dimensions must be positive.");
            }

            // Everything below this line describes the village: its six destinations,
            // its rooftops and ladders, and the routes between them. A map that was
            // built without that content has none of it to check, and demanding three
            // rooftops of a bare greybox only reports that it is bare — which is what
            // it was asked to be.
            //
            // The size is still checked, because every map has one.
            if (environmentContentCleared)
            {
                return;
            }

            ValidateLocations();
            ValidateFeatures();
            ValidateRoutes(validatePhysicsClearance);
        }

        private void ValidateLocations()
        {
            if (locations == null)
            {
                throw new InvalidOperationException(
                    "Greybox map locations are not configured.");
            }

            foreach (GreyboxLocationId required in RequiredLocations)
            {
                int count = locations.Count(
                    location => location != null
                        && location.Id == required
                        && location.Anchor != null);
                if (count != 1)
                {
                    throw new InvalidOperationException(
                        $"Greybox map requires exactly one '{required}' anchor; found {count}.");
                }
            }
        }

        private void ValidateFeatures()
        {
            if (rooftops == null || rooftops.Count < 3 || rooftops.Any(item => item == null))
            {
                throw new InvalidOperationException(
                    "Greybox map requires at least three valid rooftop references.");
            }

            if (ladders == null || ladders.Count < 3 || ladders.Any(item => item == null))
            {
                throw new InvalidOperationException(
                    "Greybox map requires at least three valid ladders.");
            }

            if (trashBins == null || trashBins.Count < 4 || trashBins.Any(item => item == null))
            {
                throw new InvalidOperationException(
                    "Greybox map requires at least four valid trash-bin positions.");
            }
        }

        private void ValidateRoutes(bool validatePhysicsClearance)
        {
            if (routes == null || routes.Count == 0)
            {
                throw new InvalidOperationException(
                    "Greybox map route network is empty.");
            }

            int uniqueRouteIds = routes
                .Where(route => route != null)
                .Select(route => route.RouteId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (uniqueRouteIds != routes.Count)
            {
                throw new InvalidOperationException(
                    "Greybox route IDs must be non-empty and unique.");
            }

            foreach (GreyboxRouteReference route in routes)
            {
                ValidateRoute(route, validatePhysicsClearance);
            }

            foreach ((GreyboxLocationId first, GreyboxLocationId second)
                     in RequiredAlternativePairs)
            {
                int routeCount = CountRoutesBetween(first, second);
                if (routeCount < 2)
                {
                    throw new InvalidOperationException(
                        $"Locations '{first}' and '{second}' require at least two routes; found {routeCount}.");
                }
            }

            GetRoute(CrossingRouteId);
        }

        private void ValidateRoute(
            GreyboxRouteReference route,
            bool validatePhysicsClearance)
        {
            if (route == null || string.IsNullOrWhiteSpace(route.RouteId))
            {
                throw new InvalidOperationException(
                    "Greybox routes require non-empty IDs.");
            }

            if (route.MinimumClearWidth < RequiredMinimumClearWidth)
            {
                throw new InvalidOperationException(
                    $"Route '{route.RouteId}' clear width {route.MinimumClearWidth:0.##}m " +
                    $"is below {RequiredMinimumClearWidth:0.##}m.");
            }

            if (route.Waypoints == null
                || route.Waypoints.Count < 2
                || route.Waypoints.Any(waypoint => waypoint == null))
            {
                throw new InvalidOperationException(
                    $"Route '{route.RouteId}' requires at least two valid waypoints.");
            }

            Vector3 fromPosition = GetLocation(route.From).position;
            Vector3 toPosition = GetLocation(route.To).position;
            if (Vector3.Distance(route.Waypoints[0].position, fromPosition) > 0.1f
                || Vector3.Distance(
                    route.Waypoints[route.Waypoints.Count - 1].position,
                    toPosition) > 0.1f)
            {
                throw new InvalidOperationException(
                    $"Route '{route.RouteId}' endpoints do not match its location anchors.");
            }

            if (validatePhysicsClearance)
            {
                ValidateRouteClearance(route);
            }
        }

        private static void ValidateRouteClearance(GreyboxRouteReference route)
        {
            Physics.SyncTransforms();

            for (int index = 1; index < route.Waypoints.Count; index++)
            {
                Vector3 start = route.Waypoints[index - 1].position;
                Vector3 end = route.Waypoints[index].position;
                float distance = Vector3.Distance(start, end);
                int sampleCount = Mathf.Max(1, Mathf.CeilToInt(distance / 0.5f));

                for (int sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++)
                {
                    Vector3 sample = Vector3.Lerp(
                        start,
                        end,
                        sampleIndex / (float)sampleCount);
                    Collider[] overlaps = Physics.OverlapCapsule(
                        sample + Vector3.up * 0.46f,
                        sample + Vector3.up * 1.54f,
                        0.45f,
                        ~0,
                        QueryTriggerInteraction.Ignore);

                    Collider obstacle = overlaps.FirstOrDefault(
                        collider =>
                            collider.GetComponentInParent<GreyboxObstacle>() != null);
                    if (obstacle != null)
                    {
                        throw new InvalidOperationException(
                            $"Route '{route.RouteId}' is blocked near {sample} by '{obstacle.name}'.");
                    }
                }
            }
        }
    }
}
