using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// Uses the held prop: throws it, or puts it on the ground.
    ///
    /// This is the single entry point for both, so the network layer has one
    /// method to route and the key binding does not have to know what is held.
    ///
    /// It only runs where the simulation runs. On a client the network input
    /// bridge turns the local key off and sends a request instead, and the host
    /// calls this — the same arrangement loot pickup already uses. Resolving a
    /// throw on both machines would let them disagree about whether it landed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToolUseAction : MonoBehaviour
    {
        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private ToolCarrier carrier;

        [SerializeField]
        private LayerMask obstacleLayers = ~0;

        /// <summary>
        /// Raised when a prop is thrown, whether or not it connected, so the
        /// visual arc can be played and the network layer can tell the other
        /// machine to play it too.
        /// </summary>
        public event Action<ThrowableKind, ThrowResolver.Result> Thrown;

        /// <summary>
        /// Raised when a prop is set down. The world object itself is created by
        /// whoever listens, because on a host that also means telling the client.
        /// </summary>
        public event Action<ThrowableKind, PlayerRole, Vector3> Placed;

        public bool HasThrowableSelected =>
            carrier != null
            && carrier.HasTool
            && carrier.HeldUse == ThrowableUse.Thrown;

        public Vector3 ThrowOrigin =>
            identity != null
                ? identity.transform.position + Vector3.up * 0.9f
                : transform.position + Vector3.up * 0.9f;

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            ToolCarrier configuredCarrier,
            LayerMask configuredObstacleLayers)
        {
            identity = configuredIdentity;
            carrier = configuredCarrier;
            obstacleLayers = configuredObstacleLayers;
        }

        /// <summary>
        /// Uses the held prop in the direction the player is facing.
        ///
        /// Kept so a throw with no aim information still works: the offline
        /// playtest, the tests and any caller that has no cursor all land here.
        /// </summary>
        public bool TryUse()
        {
            return TryUse(null, 1f);
        }

        /// <summary>
        /// Returns false when there was nothing to use or the match is not
        /// running, so a wasted press costs nothing.
        ///
        /// <paramref name="aimDirection"/> is where the player pointed. Null
        /// falls back to their facing. A placed prop ignores it — a banana goes
        /// under your own feet wherever the cursor is.
        /// </summary>
        public bool TryUse(Vector3? aimDirection)
        {
            return TryUse(aimDirection, 1f);
        }

        public bool TryUse(Vector3? aimDirection, float charge01)
        {
            if (identity == null || carrier == null)
            {
                return false;
            }

            ThrowableKind kind = carrier.HeldKind;
            ThrowableUse use = ThrowableCatalog.GetUse(kind);
            if (!carrier.TryConsume(out kind))
            {
                return false;
            }

            if (use == ThrowableUse.Placed)
            {
                Vector3 spot = identity.transform.position;
                spot.y = 0f;
                GameLogger.Info(
                    GameLogCategory.Player,
                    $"{identity.Role} placed {kind}.",
                    this);
                Placed?.Invoke(kind, identity.Role, spot);
                return true;
            }

            // The aim is flattened and sanity-checked inside the resolver, so a
            // client sending nonsense gets its own facing rather than a throw
            // straight up. The host decides, as always.
            //
            // Only the path is worked out here. Who it hits is decided while the
            // rock is in the air by ThrowFlightTracker — settling it now would
            // mean the arc the players watch is a replay of a hit that already
            // happened, and a rock you can see coming but cannot dodge is worse
            // than no rock at all.
            ThrowResolver.Result result = ThrowResolver.Resolve(
                identity,
                aimDirection ?? identity.transform.forward,
                Mathf.Lerp(
                    ThrowableCatalog.MinimumThrowRangeMeters,
                    ThrowableCatalog.ThrowRangeMeters,
                    Mathf.Clamp01(charge01)),
                obstacleLayers);

            // Face the throw. Without this the officer hurls a rock over their
            // shoulder while still running the other way, and the arm swing
            // plays on a body pointing somewhere else entirely.
            Vector3 facing = result.Landing - result.Origin;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                identity.transform.rotation =
                    Quaternion.LookRotation(facing.normalized);
                ThrowFlightTracker.Launch(
                    identity,
                    kind,
                    result.Origin,
                    facing.normalized,
                    facing.magnitude);
            }

            GameLogger.Info(
                GameLogCategory.Player,
                $"{identity.Role} threw {kind} "
                + $"{(result.Landing - result.Origin).magnitude:0.0}m.",
                this);
            Thrown?.Invoke(kind, result);
            return true;
        }
    }
}
