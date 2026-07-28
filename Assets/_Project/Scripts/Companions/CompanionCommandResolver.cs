using PawsAndLoot.Gameplay.Loot;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Turns an accepted command into a destination and a reportable outcome.
    ///
    /// DOG-003 reads the scent trail rather than the live thief, so the police
    /// never get perfect information. CAT-004 raises an information-only signal
    /// and is refused while one is already running. Both effects live here
    /// instead of inside the agent so they can be tested without a scene and so
    /// the agent keeps owning only movement and lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionCommandResolver : MonoBehaviour
    {
        [SerializeField]
        private ThiefScentTrail scentTrail;

        [SerializeField]
        private DistractionBoard distractionBoard;

        [SerializeField]
        private Transform policeTransform;

        [SerializeField]
        private Transform thiefTransform;

        [SerializeField, Min(1f)]
        private float distractionMaximumRange = 26f;

        [Header("Stage 10 ranges")]
        [SerializeField, Min(1f)]
        private float barkRevealRange = 9f;

        [SerializeField, Min(1f)]
        private float scoutRange = 16f;

        [SerializeField, Min(1f)]
        private float stealSearchRange = 14f;

        [SerializeField, Min(1f)]
        private float hideSearchRange = 12f;

        /// <summary>
        /// CAT-003 and CAT-005 report what the cat noticed so the HUD can name
        /// it. Set on the last resolve.
        /// </summary>
        public string LastScoutReport { get; private set; } = string.Empty;

        /// <summary>
        /// Where the last scout found things, so the thief can be shown a
        /// direction instead of only the words "보물과 경찰".
        ///
        /// Null when that thing was not found. The report text alone told the
        /// player something existed without telling them where, which is the
        /// half of the command that was missing.
        /// </summary>
        public Vector3? LastScoutLootPosition { get; private set; }
        public Vector3? LastScoutPolicePosition { get; private set; }
        public float LastScoutAtSeconds { get; private set; } = -1f;

        public readonly struct Resolution
        {
            public Resolution(
                bool accepted,
                CompanionCommandOutcome outcome,
                Vector3? destination)
            {
                Accepted = accepted;
                Outcome = outcome;
                Destination = destination;
            }

            public bool Accepted { get; }
            public CompanionCommandOutcome Outcome { get; }
            public Vector3? Destination { get; }
        }

        public void Configure(
            ThiefScentTrail configuredTrail,
            DistractionBoard configuredBoard,
            Transform configuredPolice,
            float configuredDistractionRange,
            Transform configuredThief = null)
        {
            scentTrail = configuredTrail;
            distractionBoard = configuredBoard;
            policeTransform = configuredPolice;
            thiefTransform = configuredThief;
            distractionMaximumRange =
                Mathf.Max(1f, configuredDistractionRange);
            LastScoutReport = string.Empty;
        }

        /// <summary>
        /// Resolves a command. A refusal here is a gameplay result, not a
        /// validation error, so it still consumes the companion's turn and gets
        /// reported to the player.
        /// </summary>
        public Resolution Resolve(
            in CompanionCommandRequest request,
            Vector3 companionPosition,
            float nowSeconds)
        {
            switch (request.CommandId)
            {
                case CompanionCommandId.Track:
                    return ResolveTrack(nowSeconds);
                case CompanionCommandId.Distract:
                    return ResolveDistract(request, nowSeconds);
                case CompanionCommandId.Search:
                    return ResolveSimpleMove(
                        request,
                        CompanionCommandOutcome.SearchStarted);
                case CompanionCommandId.Guard:
                    return ResolveSimpleMove(
                        request,
                        CompanionCommandOutcome.GuardStarted);
                case CompanionCommandId.Bark:
                    return ResolveBark(companionPosition);
                case CompanionCommandId.Scout:
                    return ResolveScout(companionPosition);
                case CompanionCommandId.Steal:
                    return ResolveSteal(companionPosition);
                case CompanionCommandId.Hide:
                    return ResolveHide(companionPosition);
                default:
                    request.TryGetDestination(out Vector3 fallback);
                    return new Resolution(
                        true,
                        CompanionCommandOutcome.Completed,
                        request.HasTarget ? fallback : null);
            }
        }

        private static Resolution ResolveSimpleMove(
            in CompanionCommandRequest request,
            CompanionCommandOutcome outcome)
        {
            return request.TryGetDestination(out Vector3 destination)
                ? new Resolution(true, outcome, destination)
                : new Resolution(
                    false,
                    CompanionCommandOutcome.Abandoned,
                    null);
        }

        /// <summary>
        /// DOG-006. Barking is a proximity check performed where the dog
        /// already stands, so it costs a turn and reveals nothing when the
        /// thief is elsewhere.
        /// </summary>
        private Resolution ResolveBark(Vector3 companionPosition)
        {
            bool near = thiefTransform != null
                && PlanarDistance(
                    companionPosition,
                    thiefTransform.position) <= barkRevealRange;
            return new Resolution(
                true,
                near
                    ? CompanionCommandOutcome.BarkRevealedThief
                    : CompanionCommandOutcome.BarkFoundNobody,
                null);
        }

        /// <summary>
        /// CAT-003. Reports what is nearby without changing anything. The cat
        /// walks to whatever it noticed so the thief can see where it went.
        /// </summary>
        private Resolution ResolveScout(Vector3 companionPosition)
        {
            LootItem loot = FindNearestAvailableLoot(
                companionPosition,
                scoutRange);
            bool policeNear = policeTransform != null
                && PlanarDistance(
                    companionPosition,
                    policeTransform.position) <= scoutRange;

            if (loot == null && !policeNear)
            {
                LastScoutReport = string.Empty;
                LastScoutLootPosition = null;
                LastScoutPolicePosition = null;
                LastScoutAtSeconds = Time.time;
                return new Resolution(
                    true,
                    CompanionCommandOutcome.ScoutFoundNothing,
                    null);
            }

            LastScoutLootPosition = loot != null
                ? loot.transform.position
                : (Vector3?)null;
            LastScoutPolicePosition = policeNear
                ? policeTransform.position
                : (Vector3?)null;
            LastScoutAtSeconds = Time.time;
            LastScoutReport = loot != null && policeNear
                ? "보물과 경찰"
                : loot != null
                    ? "보물"
                    : "경찰";
            Vector3? destination = loot != null
                ? loot.transform.position
                : null;
            return new Resolution(
                true,
                CompanionCommandOutcome.ScoutReported,
                destination);
        }

        /// <summary>
        /// CAT-005. The cat fetches loot toward the thief but never sells it.
        /// Selling stays a player action, so the cat only shortens the walk.
        /// </summary>
        private Resolution ResolveSteal(Vector3 companionPosition)
        {
            if (thiefTransform != null)
            {
                LootCarrier carrier =
                    thiefTransform.GetComponent<LootCarrier>();
                if (carrier != null && carrier.HasLoot)
                {
                    return new Resolution(
                        false,
                        CompanionCommandOutcome.StealOwnerBusy,
                        null);
                }
            }

            LootItem loot = FindNearestAvailableLoot(
                companionPosition,
                stealSearchRange);
            if (loot == null)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.StealNoLoot,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.StealStarted,
                loot.transform.position);
        }

        /// <summary>
        /// CAT-006. Sends the cat to the nearest hiding spot when the thief is
        /// carrying something worth stashing.
        /// </summary>
        private Resolution ResolveHide(Vector3 companionPosition)
        {
            LootCarrier carrier = thiefTransform != null
                ? thiefTransform.GetComponent<LootCarrier>()
                : null;
            if (carrier == null || !carrier.HasLoot)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.HideUnavailable,
                    null);
            }

            LootHidingSpot best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (LootHidingSpot spot in
                Object.FindObjectsByType<LootHidingSpot>(
                    FindObjectsSortMode.None))
            {
                if (spot.HasStoredLoot || !spot.IsAvailable)
                {
                    continue;
                }

                float distance = PlanarDistance(
                    companionPosition,
                    spot.transform.position);
                if (distance < bestDistance && distance <= hideSearchRange)
                {
                    bestDistance = distance;
                    best = spot;
                }
            }

            if (best == null)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.HideUnavailable,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.HideStored,
                best.transform.position);
        }

        private static LootItem FindNearestAvailableLoot(
            Vector3 origin,
            float range)
        {
            LootItem best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (LootItem loot in
                Object.FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None))
            {
                if (!loot.IsAvailable)
                {
                    continue;
                }

                float distance = PlanarDistance(
                    origin,
                    loot.transform.position);
                if (distance < bestDistance && distance <= range)
                {
                    bestDistance = distance;
                    best = loot;
                }
            }

            return best;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private Resolution ResolveTrack(float nowSeconds)
        {
            if (scentTrail == null
                || !scentTrail.TryGetFreshestPoint(
                    nowSeconds,
                    out Vector3 point))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.TrailMissing,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.TrailFound,
                point);
        }

        private Resolution ResolveDistract(
            in CompanionCommandRequest request,
            float nowSeconds)
        {
            if (distractionBoard == null)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionTargetTooFar,
                    null);
            }

            if (distractionBoard.IsActive)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionAlreadyActive,
                    null);
            }

            if (!request.TryGetDestination(out Vector3 destination))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionTargetTooFar,
                    null);
            }

            // Distracting someone who cannot possibly notice is a wasted order,
            // so distance to the police gates it.
            if (policeTransform != null)
            {
                Vector3 police = policeTransform.position;
                police.y = destination.y;
                if (Vector3.Distance(police, destination)
                    > distractionMaximumRange)
                {
                    return new Resolution(
                        false,
                        CompanionCommandOutcome.DistractionTargetTooFar,
                        null);
                }
            }

            if (!distractionBoard.TryStart(destination, nowSeconds))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.DistractionAlreadyActive,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.DistractionStarted,
                destination);
        }
    }
}
