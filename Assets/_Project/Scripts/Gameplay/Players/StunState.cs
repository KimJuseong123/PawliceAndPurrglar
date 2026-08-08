using System;
using PawsAndLoot.Logging;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// Holds a player still for a moment after a thrown object lands or a trap
    /// goes off.
    ///
    /// Movement is the only thing suppressed. The rules never read this: a
    /// stunned thief can still be arrested and a stunned player still owns
    /// whatever they were carrying, so a stun changes the chase without
    /// changing who wins.
    ///
    /// The re-stun guard matters more than the duration. Without it two thrown
    /// rocks in a row take control away for as long as the attacker has ammo,
    /// which stops being funny immediately.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StunState : MonoBehaviour
    {
        /// <summary>
        /// Shortest gap between two stuns landing on the same player. A hit
        /// inside this window is ignored rather than queued.
        /// </summary>
        [SerializeField, Min(0f)]
        private float minimumGapSeconds = 1.2f;

        private float _remainingSeconds;
        private float _cooldownSeconds;

        public event Action<float> Stunned;

        public bool IsStunned => _remainingSeconds > 0f;
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// What put the player here, for the screen to draw.
        ///
        /// Kept after the stun runs out rather than reset, because the views that
        /// read it fade out over the following half second and a cause that
        /// snapped back to <see cref="StunCause.Impact"/> on the last frame would
        /// pop four stars onto a character who had just finished sliding.
        /// </summary>
        public StunCause Cause { get; private set; } = StunCause.Impact;

        /// <summary>
        /// True while another stun would be refused. Exposed so the thrower can
        /// be told the hit did nothing instead of silently wasting a rock.
        /// </summary>
        public bool IsImmune => _cooldownSeconds > 0f;

        /// <summary>
        /// Returns false when the stun was refused, either because one is
        /// already running or because the last one ended too recently.
        /// </summary>
        public bool TryApply(float seconds)
        {
            return TryApply(seconds, StunCause.Impact);
        }

        /// <summary>
        /// Returns false when the stun was refused, either because one is
        /// already running or because the last one ended too recently.
        ///
        /// The cause only reaches the screen. Refusing on the same terms
        /// whatever the cause is deliberate: a banana that could re-trip a
        /// player a rock could not would be a rule change wearing a costume.
        /// </summary>
        public bool TryApply(float seconds, StunCause cause)
        {
            if (seconds <= 0f || IsStunned || IsImmune)
            {
                return false;
            }

            _remainingSeconds = seconds;
            _cooldownSeconds = seconds + minimumGapSeconds;
            Cause = cause;
            GameLogger.Info(
                GameLogCategory.Player,
                $"'{name}' stunned for {seconds:0.0}s ({cause}).",
                this);
            Stunned?.Invoke(seconds);
            return true;
        }

        /// <summary>
        /// Clears the stun without waiting it out. Used when a match ends or
        /// restarts so nobody starts the next round frozen.
        /// </summary>
        public void Clear()
        {
            _remainingSeconds = 0f;
            _cooldownSeconds = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds =
                Mathf.Max(0f, _remainingSeconds - deltaTime);
            _cooldownSeconds =
                Mathf.Max(0f, _cooldownSeconds - deltaTime);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
