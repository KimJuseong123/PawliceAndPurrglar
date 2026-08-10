using System.Collections.Generic;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Map;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
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

        [SerializeField, Min(1f)]
        private float roofSearchRange = 28f;

        /// <summary>
        /// CAT-010. How far the cat will run at the officer.
        ///
        /// Kept beside the other reach limits rather than on
        /// <c>CompanionConfig</c>: adding it to <see cref="Configure"/> would
        /// mean editing the scene generator, and regenerating `Game.unity`
        /// rewrites 130 `GlobalObjectIdHash` values — a build-mismatch risk far
        /// larger than one number's filing cabinet. The duration, which the
        /// agent applies and balance cares about, is on the config.
        /// </summary>
        [SerializeField, Min(1f)]
        private float biteSearchRange = 18f;

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
                    return ResolveRoofClimb(companionPosition);
                case CompanionCommandId.Hide:
                    return ResolveHide(companionPosition);
                case CompanionCommandId.Bite:
                    return ResolveBite(companionPosition);
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

        private Resolution ResolveRoofClimb(Vector3 companionPosition)
        {
            if (!TryFindNearestRooftop(
                    companionPosition,
                    out Vector3 destination))
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.RoofClimbUnavailable,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.RoofClimbStarted,
                destination);
        }

        private bool TryFindNearestRooftop(
            Vector3 companionPosition,
            out Vector3 destination)
        {
            destination = Vector3.zero;
            float bestDistance = float.PositiveInfinity;

            foreach (GreyboxMapDefinition map in
                Object.FindObjectsByType<GreyboxMapDefinition>(
                    FindObjectsSortMode.None))
            {
                foreach (Transform rooftop in map.Rooftops)
                {
                    TryUseRooftopCandidate(
                        companionPosition,
                        rooftop,
                        ref bestDistance,
                        ref destination);
                }
            }

            if (!float.IsPositiveInfinity(bestDistance))
            {
                return true;
            }

            foreach (Transform candidate in RooftopsByName())
            {
                TryUseRooftopCandidate(
                    companionPosition,
                    candidate,
                    ref bestDistance,
                    ref destination);
            }

            return !float.IsPositiveInfinity(bestDistance);
        }

        /// <summary>
        /// Rooftops found by name, swept once and kept.
        ///
        /// The sweep is <c>FindObjectsByType&lt;Transform&gt;</c> — every
        /// transform in every loaded scene, and this one has thousands once the
        /// houses and the nineteen interiors are in it — followed by a
        /// case-insensitive <c>Contains</c> on each name, which allocates the
        /// name string every time. Run inline it was a visible freeze on the
        /// host at the exact moment a cat was told to climb, and the player who
        /// gave the order is on the other machine watching a world that had
        /// stopped replicating.
        ///
        /// Cached because rooftops are built by the map generator and do not
        /// move or multiply during a match. Re-swept only while the answer is
        /// empty, so a scene that has not finished loading is not remembered as
        /// having no roofs.
        /// </summary>
        private static Transform[] _rooftopsByName;

        private static Transform[] RooftopsByName()
        {
            if (_rooftopsByName is { Length: > 0 })
            {
                bool intact = true;
                foreach (Transform cached in _rooftopsByName)
                {
                    if (cached == null)
                    {
                        intact = false;
                        break;
                    }
                }

                if (intact)
                {
                    return _rooftopsByName;
                }
            }

            var found = new List<Transform>();
            foreach (Transform candidate in
                Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (candidate != null
                    && candidate.name.Contains(
                        "Rooftop",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(candidate);
                }
            }

            _rooftopsByName = found.ToArray();
            return _rooftopsByName;
        }

        private void TryUseRooftopCandidate(
            Vector3 companionPosition,
            Transform rooftop,
            ref float bestDistance,
            ref Vector3 destination)
        {
            if (rooftop == null)
            {
                return;
            }

            Vector3 point = ResolveRooftopLanding(rooftop);
            float distance = PlanarDistance(companionPosition, point);
            if (distance > roofSearchRange || distance >= bestDistance)
            {
                return;
            }

            bestDistance = distance;
            destination = point;
        }

        private static Vector3 ResolveRooftopLanding(Transform rooftop)
        {
            Collider roofCollider = rooftop.GetComponent<Collider>();
            if (roofCollider != null)
            {
                Physics.SyncTransforms();
                Bounds bounds = roofCollider.bounds;
                return new Vector3(
                    rooftop.position.x,
                    bounds.max.y + 0.7f,
                    rooftop.position.z);
            }

            return rooftop.position + Vector3.up;
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
                // Busy means "no room left", not "holding something". The cat
                // fetches into the thief's bag, and the bag now takes more than
                // one piece — refusing while it has space would make the steal
                // command useless from the second treasure onwards.
                if (carrier != null && !carrier.CanCarryMore)
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
            // MAP-008. A thief who has gone indoors is reported as the house they
            // went into, not as where they actually are.
            //
            // Their real position is in a room built far outside the town, so
            // pointing at it would send the officer and the dog off the edge of
            // the map. The door is also the honest answer: the trail genuinely
            // ends there, and "he went in there" is what a dog at a doorway
            // tells you.
            Vector3? indoors = TryResolveIndoorDoor();
            if (indoors.HasValue)
            {
                return new Resolution(
                    true,
                    CompanionCommandOutcome.TrailFound,
                    indoors.Value);
            }

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

        /// <summary>
        /// The doorway of whichever house the thief is inside, or null when they
        /// are out in the streets.
        ///
        /// Found by asking the world rather than being wired up, because the rooms
        /// and their doors are built by the map generator and the thief's location
        /// only becomes true at runtime.
        /// </summary>
        private static Vector3? TryResolveIndoorDoor()
        {
            PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState thiefState =
                null;
            foreach (PawliceAndPurrglar.Gameplay.Players.PlayerRoleIdentity candidate
                in Object.FindObjectsByType<
                    PawliceAndPurrglar.Gameplay.Players.PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate.Role
                    != PawliceAndPurrglar.Gameplay.Players.PlayerRole.Thief)
                {
                    continue;
                }

                thiefState = candidate.GetComponent<
                    PawliceAndPurrglar.Gameplay.Interiors.PlayerInteriorState>();
                break;
            }

            if (thiefState == null || !thiefState.IsIndoors)
            {
                return null;
            }

            foreach (PawliceAndPurrglar.Gameplay.Interiors.HouseDoorway door in
                Object.FindObjectsByType<
                    PawliceAndPurrglar.Gameplay.Interiors.HouseDoorway>(
                    FindObjectsSortMode.None))
            {
                // The street-side door of that house: the one the officer can
                // actually walk to.
                if (door.LeadsInside
                    && door.Interior != null
                    && door.Interior.InteriorId
                        == thiefState.CurrentInteriorId)
                {
                    return door.transform.position;
                }
            }

            return null;
        }

        /// <summary>
        /// CAT-010. Sends the cat at the officer.
        ///
        /// Only the run is decided here. The hold itself happens when the cat
        /// arrives — see <see cref="CompanionAgent"/> — because a stun applied
        /// at the moment of the order would let the thief freeze an officer
        /// halfway across the street, and the walk is the counterplay.
        ///
        /// The range refuses rather than sending the cat off at a distant
        /// officer: the walk is already the cost, and a cat that leaves for six
        /// seconds and returns having done nothing reads as a broken command.
        /// </summary>
        private Resolution ResolveBite(Vector3 companionPosition)
        {
            if (policeTransform == null
                || PlanarDistance(companionPosition, policeTransform.position)
                    > biteSearchRange)
            {
                return new Resolution(
                    false,
                    CompanionCommandOutcome.BiteNoTarget,
                    null);
            }

            return new Resolution(
                true,
                CompanionCommandOutcome.BiteStarted,
                policeTransform.position);
        }

        /// <summary>
        /// The officer, so the agent can find who it just caught up with.
        ///
        /// Read only. Handing out the transform rather than the stun keeps the
        /// numbers where the rest of the animal's numbers are — on
        /// <c>CompanionConfig</c>, which the agent already holds.
        /// </summary>
        public Transform PoliceTransform => policeTransform;

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
