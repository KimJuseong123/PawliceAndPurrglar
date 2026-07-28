using System;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Items
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
        /// Returns false when there was nothing to use or the match is not
        /// running, so a wasted press costs nothing.
        /// </summary>
        public bool TryUse()
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

            ThrowResolver.Result result = ThrowResolver.Resolve(
                identity,
                identity.transform.forward,
                ThrowableCatalog.ThrowRangeMeters,
                obstacleLayers);

            if (result.Connected)
            {
                StunState stun =
                    result.Hit.GetComponent<StunState>();
                // A refused stun still spends the prop. The alternative is
                // giving the rock back, which lets a player hold one press
                // against an opponent who is briefly immune.
                stun?.TryApply(
                    ThrowableCatalog.GetStunSeconds(kind));
            }

            GameLogger.Info(
                GameLogCategory.Player,
                $"{identity.Role} threw {kind}, "
                + $"hit={(result.Connected ? "yes" : "no")}.",
                this);
            Thrown?.Invoke(kind, result);
            return true;
        }
    }
}
