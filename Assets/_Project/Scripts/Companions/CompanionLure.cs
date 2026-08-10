using System;
using PawliceAndPurrglar.Logging;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// A smell the animal cannot ignore.
    ///
    /// The two props that use this are the only way either player can touch the
    /// other's animal, and that is the point: until now a companion could be
    /// outrun but never interfered with, so the animal half of the game had no
    /// counterplay in it at all.
    ///
    /// Deliberately not a stun. The animal is not disabled — it walks somewhere
    /// it would rather be, which is both funnier and easier to read than a
    /// frozen dog. It also means the lure has a cost to the thrower: the animal
    /// ends up wherever the prop landed, and a badly aimed treat pulls the dog
    /// closer to you.
    ///
    /// Host side. The companions are simulated on the host and their positions
    /// replicate, so a client running its own lure clock would fight that.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionLure : MonoBehaviour
    {
        private float _remainingSeconds;

        public event Action<Vector3> LureStarted;
        public event Action LureEnded;

        public bool IsActive => _remainingSeconds > 0f;
        public Vector3 Point { get; private set; }
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>
        /// How many times this animal has been pulled away this match, for the
        /// probe and for anybody balancing the props later.
        /// </summary>
        public int LuredCount { get; private set; }

        public bool HasAuthority { get; private set; } = true;

        public void SetAuthority(bool hasAuthority)
        {
            HasAuthority = hasAuthority;
        }

        /// <summary>
        /// Starts or extends a lure.
        ///
        /// A second prop replaces the first rather than queueing behind it: two
        /// treats on opposite sides of the street should send the dog to the
        /// newer one, not make it serve both sentences.
        /// </summary>
        public bool TryLure(Vector3 point, float seconds)
        {
            if (!HasAuthority || seconds <= 0f)
            {
                return false;
            }

            bool wasActive = IsActive;
            Point = point;
            _remainingSeconds = seconds;
            if (!wasActive)
            {
                LuredCount++;
            }

            GameLogger.Info(
                GameLogCategory.Companion,
                $"{name} lured for {seconds:0.0}s.",
                this);
            LureStarted?.Invoke(point);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!HasAuthority || !IsActive)
            {
                return;
            }

            _remainingSeconds -= deltaTime;
            if (_remainingSeconds > 0f)
            {
                return;
            }

            _remainingSeconds = 0f;
            LureEnded?.Invoke();
        }

        public void Clear()
        {
            _remainingSeconds = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
