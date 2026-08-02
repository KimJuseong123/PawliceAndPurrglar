using System;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// Something is stuck to this player's face.
    ///
    /// Deliberately not a stun. The rock already takes time away, and a second
    /// prop that also took time away would only be a rock with a different
    /// model. This takes information instead: the victim keeps every bit of
    /// their speed and loses the ability to see where they are spending it.
    ///
    /// That makes it the only prop whose value depends on where it lands. A
    /// blinded player in an open square shrugs and keeps running; a blinded
    /// player at a junction has to guess. Nothing else in the set rewards
    /// timing that way.
    ///
    /// Kept alongside <see cref="StunState"/> rather than folded into it,
    /// because the two stack and mean different things — a player can be held
    /// still and able to see, or running blind, or both.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlindedState : MonoBehaviour
    {
        private float _remainingSeconds;

        public event Action<float> Blinded;
        public event Action Cleared;

        public bool IsBlinded => _remainingSeconds > 0f;
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// How many times this has landed. Latched, so a probe can tell "it
        /// never hit" from "it hit and has worn off".
        /// </summary>
        public int AppliedCount { get; private set; }

        /// <summary>
        /// Applies it, extending rather than replacing.
        ///
        /// Two octopuses in a row should be worse than one. A stun refuses to
        /// stack because being held indefinitely is not a game; being unable to
        /// see for longer is survivable, so it is allowed to add up.
        /// </summary>
        public bool TryApply(float seconds)
        {
            if (seconds <= 0f)
            {
                return false;
            }

            _remainingSeconds = Mathf.Max(_remainingSeconds, 0f) + seconds;
            AppliedCount++;
            Blinded?.Invoke(_remainingSeconds);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_remainingSeconds <= 0f || deltaTime <= 0f)
            {
                return;
            }

            _remainingSeconds -= deltaTime;
            if (_remainingSeconds <= 0f)
            {
                _remainingSeconds = 0f;
                Cleared?.Invoke();
            }
        }

        public void Clear()
        {
            if (_remainingSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds = 0f;
            Cleared?.Invoke();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
