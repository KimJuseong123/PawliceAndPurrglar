using System;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Logging;
using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// A prop left on the ground that slips whoever runs over it.
    ///
    /// Only the side that did not place it can trigger it, so a thief cannot
    /// slip on their own banana while backing away from it. It fires once and
    /// then reports itself spent; the host removes it.
    ///
    /// The host owns triggering. A trap that each machine judged for itself
    /// would slip the runner on one screen and not the other.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedTrap : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        private float triggerRadius = 0.85f;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        private IMatchStateReader _matchState;
        private PlayerRole _placedBy;
        private ThrowableKind _kind;
        private bool _armed;
        private float _fuseRemaining;

        /// <summary>
        /// Raised on the host when the trap catches somebody. The caller applies
        /// the stun and tells everyone the trap is gone.
        /// </summary>
        public event Action<PlacedTrap, PlayerRoleIdentity> Triggered;

        public bool IsArmed => _armed;

        /// <summary>
        /// Whether this one goes off by itself rather than waiting to be
        /// trodden on.
        /// </summary>
        public bool HasFuse =>
            ThrowableCatalog.GetFuseSeconds(_kind) > 0f;

        public float FuseRemainingSeconds => _fuseRemaining;
        public PlayerRole PlacedBy => _placedBy;
        public ThrowableKind Kind => _kind;

        /// <summary>
        /// Identifies this trap across machines. The host places and removes by
        /// id rather than by object reference, which avoids spawning a
        /// NetworkObject per banana.
        /// </summary>
        public int TrapId { get; private set; }

        public void Configure(
            int trapId,
            ThrowableKind kind,
            PlayerRole placedBy,
            IMatchStateReader configuredMatchState)
        {
            TrapId = trapId;
            _kind = kind;
            // Per kind: a sensor light detects at a distance, a thing underfoot
            // has to be trodden on. A detector with a doormat's reach would
            // never fire.
            triggerRadius = ThrowableCatalog.GetTriggerRadius(kind);
            _placedBy = placedBy;
            _matchState = configuredMatchState;
            matchStateSource = configuredMatchState as MonoBehaviour;
            _armed = true;
            _fuseRemaining = ThrowableCatalog.GetFuseSeconds(kind);
        }

        public void Disarm()
        {
            _armed = false;
        }

        /// <summary>
        /// Burns the fuse down and reports the one frame it reaches zero.
        ///
        /// A fuse does not care whether anybody came near, which is the whole
        /// difference between a firework and a trap: a trap is a bet that
        /// somebody will make a mistake, and a firework is a thing that happens.
        ///
        /// Stepped by the caller rather than by Update so the host is the only
        /// machine burning it and a test can run it without waiting.
        /// </summary>
        public bool TickFuse(float deltaTime)
        {
            if (!_armed
                || !HasFuse
                || deltaTime <= 0f
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return false;
            }

            _fuseRemaining -= deltaTime;
            if (_fuseRemaining > 0f)
            {
                return false;
            }

            _armed = false;
            GameLogger.Info(
                GameLogCategory.Player,
                $"{_kind} placed by {_placedBy} went off.",
                this);
            Triggered?.Invoke(this, null);
            return true;
        }

        /// <summary>
        /// Host-side check. Separated from Update so a test can step it without
        /// waiting on frames.
        /// </summary>
        public PlayerRoleIdentity FindVictim()
        {
            // A fuse prop has no victim to find. Asking anyway would let a
            // firework fire early because somebody walked past it, which is the
            // one thing it is not supposed to do.
            if (!_armed
                || HasFuse
                || ResolveMatchState()?.IsGameplayActive != true)
            {
                return null;
            }

            foreach (PlayerRoleIdentity candidate in
                FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled
                    || candidate.Role == _placedBy)
                {
                    continue;
                }

                Vector3 delta =
                    candidate.transform.position - transform.position;
                delta.y = 0f;
                if (delta.magnitude <= triggerRadius)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Fires once. Disarms before raising the event so a handler that takes
        /// a frame cannot let the same trap catch twice.
        /// </summary>
        public bool TryTrigger(out PlayerRoleIdentity victim)
        {
            victim = FindVictim();
            if (victim == null)
            {
                return false;
            }

            _armed = false;
            GameLogger.Info(
                GameLogCategory.Player,
                $"{_kind} placed by {_placedBy} caught {victim.Role}.",
                this);
            Triggered?.Invoke(this, victim);
            return true;
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null
                && matchStateSource is IMatchStateReader reader)
            {
                _matchState = reader;
            }

            return _matchState;
        }
    }
}
