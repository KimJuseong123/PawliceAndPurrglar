using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Works out where a thrown prop lands and who it hits.
    ///
    /// Host side only. A client asks to throw and the host answers, for the same
    /// reason it owns loot ownership: two machines resolving the same throw would
    /// disagree about whether it connected, and the loser of that disagreement
    /// would be stunned on one screen and running on the other.
    ///
    /// Resolved immediately rather than simulated as a flying object. A
    /// projectile would need its own NetworkObject and per-frame replication,
    /// and dynamic NetworkObjects are exactly what went wrong in ISSUE-016. The
    /// arc the players see is a local cosmetic replay of the launch the host
    /// already decided.
    /// </summary>
    public static class ThrowResolver
    {
        public readonly struct Result
        {
            public Result(
                Vector3 origin,
                Vector3 landing,
                PlayerRoleIdentity hit)
            {
                Origin = origin;
                Landing = landing;
                Hit = hit;
            }

            public Vector3 Origin { get; }
            public Vector3 Landing { get; }

            /// <summary>
            /// The player the prop struck, or null when it landed on nothing.
            /// </summary>
            public PlayerRoleIdentity Hit { get; }

            public bool Connected => Hit != null;
        }

        /// <summary>
        /// Traces a throw from a player along a direction.
        ///
        /// Walls stop it: a rock thrown at a building lands at the building, so
        /// the thief can break line of sight and be safe. That is what makes the
        /// alleys worth running down.
        /// </summary>
        public static Result Resolve(
            PlayerRoleIdentity thrower,
            Vector3 direction,
            float range,
            int obstacleLayers)
        {
            Vector3 origin = thrower.transform.position + Vector3.up * 0.9f;
            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude <= 0.0001f)
            {
                flat = thrower.transform.forward;
                flat.y = 0f;
            }

            flat.Normalize();
            float travel = ResolveTravel(
                origin,
                flat,
                range,
                obstacleLayers);
            Vector3 landing = origin + flat * travel;

            // Nearest opposing player within the hit radius of the flight path.
            PlayerRoleIdentity best = null;
            float bestDistance = float.MaxValue;
            foreach (PlayerRoleIdentity candidate in
                Object.FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate == thrower
                    || candidate.Role == thrower.Role
                    || !candidate.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3 toTarget = candidate.transform.position - origin;
                toTarget.y = 0f;
                float along = Vector3.Dot(toTarget, flat);
                if (along < 0f || along > travel)
                {
                    continue;
                }

                float offAxis =
                    (toTarget - flat * along).magnitude;
                if (offAxis > ThrowableCatalog.GetThrowHitRadius(
                        thrower.Role,
                        candidate.Role))
                {
                    continue;
                }

                if (along < bestDistance)
                {
                    bestDistance = along;
                    best = candidate;
                }
            }

            if (best != null)
            {
                // The prop stops at whoever it hit.
                landing = origin + flat * bestDistance;
            }

            return new Result(origin, landing, best);
        }

        /// <summary>
        /// How far the prop gets before something solid stops it.
        ///
        /// Two things had to be excluded, and missing either made throwing feel
        /// broken rather than difficult.
        ///
        /// Triggers are not walls. The map is full of invisible trigger spheres —
        /// every rock, stash, sale point and ladder is one — and Unity's raycasts
        /// hit triggers by default, so a throw down a street was stopping a metre
        /// from the thrower's hand at the nearest pickup with nothing on screen to
        /// explain it.
        ///
        /// Characters are not walls either. Both players and the companions move
        /// on a <see cref="CharacterController"/>, and the dog runs at the
        /// officer's heel — treating it as cover means the officer can never
        /// throw at all. Who a throw actually hits is decided by the corridor
        /// test below, which is the one place that should be answering it.
        ///
        /// Real geometry still stops the throw, which is what makes breaking line
        /// of sight worth doing.
        /// </summary>
        private static float ResolveTravel(
            Vector3 origin,
            Vector3 direction,
            float range,
            int obstacleLayers)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                range,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);

            float travel = range;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider is CharacterController)
                {
                    continue;
                }

                if (hit.distance < travel)
                {
                    travel = hit.distance;
                }
            }

            return travel;
        }
    }
}
