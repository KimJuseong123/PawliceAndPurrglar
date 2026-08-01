using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Shared ballistic calculation for the throw preview and the authoritative
    /// flight simulation. Keeping the calculation here prevents the common bug
    /// where the marker shows one landing point and the projectile uses another.
    /// </summary>
    public static class ThrowTrajectorySolver
    {
        public const float Gravity = -9.81f;
        public const float LaunchAngleDegrees = 35f;
        public const float HorizontalSpeed = 16f;
        public const float SampleInterval = 0.04f;

        public readonly struct Solution
        {
            public Solution(
                Vector3 origin,
                Vector3 direction,
                Vector3 velocity,
                float flightTime,
                Vector3 landing,
                bool blocked,
                float travelledTime)
            {
                Origin = origin;
                Direction = direction;
                Velocity = velocity;
                FlightTime = flightTime;
                Landing = landing;
                Blocked = blocked;
                TravelledTime = travelledTime;
            }

            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public Vector3 Velocity { get; }
            public float FlightTime { get; }
            public Vector3 Landing { get; }
            public bool Blocked { get; }
            public float TravelledTime { get; }
        }

        public static Solution Solve(
            Vector3 origin,
            Vector3 direction,
            float range,
            int obstacleLayers = 0,
            float landingY = 0f,
            float radius = ThrowableCatalog.PropDiameterMeters * 0.5f)
        {
            Vector3 flat = direction;
            flat.y = 0f;
            if (flat.sqrMagnitude <= 0.0001f)
            {
                flat = Vector3.forward;
            }

            flat.Normalize();
            float horizontal = Mathf.Max(0.05f, range);
            float heightDelta = landingY - origin.y;
            float flightTime = horizontal / HorizontalSpeed;
            float verticalSpeed = (heightDelta
                - 0.5f * Gravity * flightTime * flightTime)
                / Mathf.Max(0.01f, flightTime);
            Vector3 velocity = flat * HorizontalSpeed
                + Vector3.up * verticalSpeed;

            float travelledTime = flightTime;
            bool blocked = false;
            if (obstacleLayers != 0)
            {
                float previousTime = 0f;
                Vector3 previous = PositionAt(origin, velocity, previousTime);
                for (float time = SampleInterval;
                     time <= flightTime + SampleInterval * 0.5f;
                     time += SampleInterval)
                {
                    float currentTime = Mathf.Min(time, flightTime);
                    Vector3 current = PositionAt(origin, velocity, currentTime);
                    Vector3 segment = current - previous;
                    float length = segment.magnitude;
                    if (length > 0.0001f
                        && Physics.SphereCast(
                            previous,
                            radius,
                            segment / length,
                            out RaycastHit hit,
                            length,
                            obstacleLayers,
                            QueryTriggerInteraction.Ignore)
                        && !(hit.collider is CharacterController))
                    {
                        travelledTime = previousTime
                            + (currentTime - previousTime)
                            * Mathf.Clamp01(hit.distance / length);
                        blocked = true;
                        break;
                    }

                    previous = current;
                    previousTime = currentTime;
                    if (currentTime >= flightTime)
                    {
                        break;
                    }
                }
            }

            Vector3 landing = PositionAt(
                origin,
                velocity,
                travelledTime);
            if (!blocked)
            {
                landing.y = landingY;
            }

            return new Solution(
                origin,
                flat,
                velocity,
                flightTime,
                landing,
                blocked,
                travelledTime);
        }

        public static Vector3 PositionAt(
            Vector3 origin,
            Vector3 velocity,
            float time)
        {
            return origin
                + velocity * Mathf.Max(0f, time)
                + Vector3.up * (0.5f * Gravity * time * time);
        }
    }
}
