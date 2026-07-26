using System;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// CAT-004 support. Holds the one active distraction signal.
    ///
    /// A distraction is information only. It never moves the thief and never
    /// touches the loot or arrest rules, so the worst it can do is waste police
    /// attention. Only one may be active at a time, which is what stops the cat
    /// from spamming markers across the map.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistractionBoard : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        private float durationSeconds = 4f;

        private float _expiresAtSeconds;

        public event Action<Vector3> DistractionStarted;
        public event Action DistractionEnded;

        public bool IsActive { get; private set; }
        public Vector3 ActivePosition { get; private set; }
        public float DurationSeconds => durationSeconds;
        public int StartedCount { get; private set; }

        public float RemainingSeconds(float nowSeconds)
        {
            return IsActive
                ? Mathf.Max(0f, _expiresAtSeconds - nowSeconds)
                : 0f;
        }

        public void Configure(float configuredDurationSeconds)
        {
            durationSeconds = Mathf.Max(0.5f, configuredDurationSeconds);
            Clear();
        }

        public void Clear()
        {
            if (IsActive)
            {
                IsActive = false;
                DistractionEnded?.Invoke();
            }

            _expiresAtSeconds = 0f;
        }

        /// <summary>
        /// Starts a signal. Refused while one is already running so a second
        /// order cannot stack duration or create a second marker.
        /// </summary>
        public bool TryStart(Vector3 position, float nowSeconds)
        {
            if (IsActive)
            {
                return false;
            }

            IsActive = true;
            ActivePosition = position;
            _expiresAtSeconds = nowSeconds + durationSeconds;
            StartedCount++;
            DistractionStarted?.Invoke(position);
            return true;
        }

        public void Tick(float nowSeconds)
        {
            if (!IsActive || nowSeconds < _expiresAtSeconds)
            {
                return;
            }

            IsActive = false;
            DistractionEnded?.Invoke();
        }

        private void Update()
        {
            Tick(Time.time);
        }
    }
}
