using System.Collections.Generic;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// Rocks that are still in the air, and who they end up hitting.
    ///
    /// The throw used to be settled the instant it left the hand: the host asked
    /// "is anybody in the corridor" once and applied the stun immediately, while
    /// the rock the players watched was a cosmetic replay of a decision already
    /// made. That makes the arc a lie — you can watch a rock coming and there is
    /// nothing you can do, because it hit you before it was drawn.
    ///
    /// Now the rock travels and the corridor is tested where it currently is, so
    /// stepping out of the way works. That is the whole point: the officer aims at
    /// where the thief will be rather than where they are, and the thief gets the
    /// flight time to move.
    ///
    /// Host side. A client watches an arc it cannot resolve, exactly as before —
    /// two machines judging the same rock would disagree about a near miss, which
    /// is now the interesting case rather than a rare one.
    /// </summary>
    public sealed class ThrowFlightTracker : MonoBehaviour
    {
        /// <summary>
        /// One rock, mid-air.
        /// </summary>
        private struct Flight
        {
            public PlayerRoleIdentity Thrower;
            public ThrowableKind Kind;
            public Vector3 Origin;
            public Vector3 Direction;
            public float Distance;
            public float Travelled;
        }

        private static ThrowFlightTracker _instance;
        private static int _nextRecoveryPickupId = -1;

        private readonly List<Flight> _flights = new();
        private readonly List<ThrowablePickup> _recoveryPickups = new();

        public int ActiveFlightCount => _flights.Count;

        /// <summary>
        /// Metres per second. Slow enough to be read and dodged at the far end of
        /// a throw, fast enough that a rock at close range still lands — a rock
        /// nobody can ever be hit by is not a threat.
        ///
        /// At 16 m/s the full 12 m throw takes 0.75 s, which is about the time it
        /// takes to react and change direction.
        /// </summary>
        public const float SpeedMetresPerSecond = 16f;

        /// <summary>
        /// Called on whichever machine simulates — the host, or the only machine
        /// offline. Returns the landing spot so the visual can be launched along
        /// the same path.
        /// </summary>
        public static Vector3 Launch(
            PlayerRoleIdentity thrower,
            ThrowableKind kind,
            Vector3 origin,
            Vector3 direction,
            float distance)
        {
            Vector3 landing = origin + direction * distance;
            if (_instance == null)
            {
                return landing;
            }

            _instance._flights.Add(new Flight
            {
                Thrower = thrower,
                Kind = kind,
                Origin = origin,
                Direction = direction,
                Distance = distance,
                Travelled = 0f
            });
            return landing;
        }

        /// <summary>
        /// Advances every rock and resolves the ones that reach somebody.
        ///
        /// Separated from Update so a test can step it without waiting on frames.
        /// </summary>
        public void Tick(float deltaTime)
        {
            for (int index = _flights.Count - 1; index >= 0; index--)
            {
                Flight flight = _flights[index];
                float previous = flight.Travelled;
                flight.Travelled += SpeedMetresPerSecond * deltaTime;

                PlayerRoleIdentity victim = FindVictim(
                    flight,
                    previous,
                    flight.Travelled);
                if (victim != null)
                {
                    _flights.RemoveAt(index);
                    ApplyHit(flight, victim);
                    continue;
                }

                if (flight.Travelled >= flight.Distance)
                {
                    _flights.RemoveAt(index);
                    CreateRecoveryPickup(flight);
                    continue;
                }

                _flights[index] = flight;
            }
        }

        /// <summary>
        /// Raised after a rock connects, for anything that only wants to watch.
        /// </summary>
        public event System.Action<
            ThrowableKind,
            PlayerRoleIdentity,
            PlayerRoleIdentity> Hit;

        /// <summary>
        /// What a connection does, applied where the flight was resolved.
        ///
        /// The same two effects the instant version applied, in the same order:
        /// the stun first, and money only if the stun actually landed. That gate
        /// matters — the re-stun gap is what stops an officer with a rock emptying
        /// the thief in a few seconds.
        /// </summary>
        private void ApplyHit(Flight flight, PlayerRoleIdentity victim)
        {
            StunState stun = victim.GetComponent<StunState>();
            bool landed = stun?.TryApply(
                ThrowableCatalog.GetStunSeconds(flight.Kind),
                ThrowableCatalog.GetStunCause(flight.Kind)) == true;

            // Sight is taken separately from time, because they are separate
            // things. The octopus stuns nobody and blinds; the rock blinds
            // nobody and stuns. Asking both questions of every hit means a prop
            // that did both would need no new branch here.
            float blindSeconds =
                ThrowableCatalog.GetBlindSeconds(flight.Kind);
            bool blinded = blindSeconds > 0f
                && victim.GetComponent<PawsAndLoot.Gameplay.Players.BlindedState>()
                    ?.TryApply(blindSeconds) == true;
            landed = landed || blinded;

            // Money moves on a hold, not on a blinding. Taking the thief's
            // purse for covering their eyes would make the octopus strictly
            // better than the rock, and the rock is what the officer has.
            if (stun?.IsStunned == true && blindSeconds <= 0f)
            {
                PawsAndLoot.Gameplay.Loot.LootConfiscationRule.Apply(
                    victim,
                    flight.Thrower);
            }

            PawsAndLoot.Logging.GameLogger.Info(
                PawsAndLoot.Logging.GameLogCategory.Player,
                $"{flight.Kind} from {flight.Thrower?.Role} hit "
                + $"{victim.Role} after "
                + $"{flight.Travelled:0.0}m.",
                this);
            Hit?.Invoke(flight.Kind, flight.Thrower, victim);
        }

        /// <summary>
        /// Anybody the rock swept past during this step.
        ///
        /// The whole travelled segment is tested rather than the single point the
        /// rock reached, because at 16 m/s a frame covers a third of a metre and
        /// a point test would let a rock skip through somebody at low frame
        /// rates.
        /// </summary>
        private static PlayerRoleIdentity FindVictim(
            Flight flight,
            float from,
            float to)
        {
            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (candidate == flight.Thrower
                    || !candidate.isActiveAndEnabled
                    || flight.Thrower == null
                    || candidate.Role == flight.Thrower.Role)
                {
                    continue;
                }

                Vector3 toTarget =
                    candidate.transform.position - flight.Origin;
                toTarget.y = 0f;
                float along = Vector3.Dot(toTarget, flight.Direction);
                if (along < from || along > to)
                {
                    continue;
                }

                float offAxis =
                    (toTarget - flight.Direction * along).magnitude;
                if (offAxis <= ThrowableCatalog.GetThrowHitRadius(
                        flight.Thrower.Role,
                        candidate.Role))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void CreateRecoveryPickup(Flight flight)
        {
            if (flight.Thrower == null)
            {
                return;
            }

            Vector3 landing = flight.Origin
                + flight.Direction.normalized * flight.Distance;
            landing.y = 0f;
            ThrowablePickup pickup = ThrownPickupFactory.Create(
                _nextRecoveryPickupId--,
                flight.Kind,
                landing);
            if (pickup != null)
            {
                _recoveryPickups.Add(pickup);
            }
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            foreach (ThrowablePickup pickup in _recoveryPickups)
            {
                if (pickup == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(pickup.gameObject);
                }
                else
                {
                    DestroyImmediate(pickup.gameObject);
                }
            }

            _recoveryPickups.Clear();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
