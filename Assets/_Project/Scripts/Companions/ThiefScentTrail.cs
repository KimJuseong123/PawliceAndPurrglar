using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// DOG-003 support. Records where the thief has been so the dog can track a
    /// trail instead of the live position.
    ///
    /// This is what keeps the police from having perfect information: the dog
    /// follows a stale point that expires, so a thief who keeps moving stays
    /// ahead of it. The trail is only written by the thief and only read by the
    /// tracking command.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThiefScentTrail : MonoBehaviour
    {
        private readonly List<ScentPoint> _points = new();

        [SerializeField, Min(0.1f)]
        private float sampleIntervalSeconds = 0.5f;

        [SerializeField, Min(0.5f)]
        private float pointLifetimeSeconds = 12f;

        [SerializeField, Min(0.1f)]
        private float minimumSampleDistance = 0.75f;

        [SerializeField, Min(2)]
        private int maximumPoints = 64;

        private float _nextSampleTime;

        public readonly struct ScentPoint
        {
            public ScentPoint(Vector3 position, float recordedAtSeconds)
            {
                Position = position;
                RecordedAtSeconds = recordedAtSeconds;
            }

            public Vector3 Position { get; }
            public float RecordedAtSeconds { get; }
        }

        public int PointCount => _points.Count;

        /// <summary>
        /// The trail, oldest first, for anything that needs to draw it.
        ///
        /// Read only. The trail is gameplay data the dog follows, and a view
        /// that could edit it would be able to move the dog. Exposed at all
        /// because the points were already being recorded and followed while
        /// staying completely invisible to the player — the tracking worked and
        /// nobody could see it.
        /// </summary>
        public IReadOnlyList<ScentPoint> Points => _points;
        public float PointLifetimeSeconds => pointLifetimeSeconds;

        public void Configure(
            float interval,
            float lifetime,
            float minimumDistance)
        {
            sampleIntervalSeconds = Mathf.Max(0.1f, interval);
            pointLifetimeSeconds = Mathf.Max(0.5f, lifetime);
            minimumSampleDistance = Mathf.Max(0.1f, minimumDistance);
            Clear();
        }

        public void Clear()
        {
            _points.Clear();
            _nextSampleTime = 0f;
        }

        /// <summary>
        /// Records the current position when enough time and distance have
        /// passed. Driven explicitly so tests do not depend on frame timing.
        /// </summary>
        public void Sample(float nowSeconds)
        {
            if (nowSeconds < _nextSampleTime)
            {
                return;
            }

            _nextSampleTime = nowSeconds + sampleIntervalSeconds;
            Vector3 position = transform.position;
            if (_points.Count > 0)
            {
                Vector3 last = _points[_points.Count - 1].Position;
                if (Vector3.Distance(last, position)
                    < minimumSampleDistance)
                {
                    return;
                }
            }

            _points.Add(new ScentPoint(position, nowSeconds));
            if (_points.Count > maximumPoints)
            {
                _points.RemoveAt(0);
            }
        }

        public void PruneExpired(float nowSeconds)
        {
            for (int index = _points.Count - 1; index >= 0; index--)
            {
                if (nowSeconds - _points[index].RecordedAtSeconds
                    > pointLifetimeSeconds)
                {
                    _points.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// Newest point still within its lifetime. Returns false when the trail
        /// has gone cold, which is the "no trail to follow" case the dog has to
        /// report rather than guess.
        /// </summary>
        public bool TryGetFreshestPoint(
            float nowSeconds,
            out Vector3 position)
        {
            PruneExpired(nowSeconds);
            if (_points.Count == 0)
            {
                position = default;
                return false;
            }

            position = _points[_points.Count - 1].Position;
            return true;
        }

        private void Update()
        {
            Sample(Time.time);
        }
    }
}
