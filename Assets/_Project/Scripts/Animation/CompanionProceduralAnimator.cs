using System;
using PawsAndLoot.Companions;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// Temporary movement animation for the animals.
    ///
    /// The authored dog and cat are quadrupeds with no animation clips, and
    /// humanoid clips cannot retarget onto them, so movement is faked with a
    /// hop and a lean instead. This is a placeholder, not authored animation;
    /// it is replaced when MODEL-005 and MODEL-006 ship real clips.
    ///
    /// Only the visual child is offset. The agent's own transform and its
    /// CharacterController are never touched, so collision stays exact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionProceduralAnimator : MonoBehaviour
    {
        [SerializeField]
        private CompanionAgent agent;

        [SerializeField]
        private Transform visual;

        // Once the legs carry the stride, the body only needs a slight vertical
        // settle. The earlier large hop is what made the animals look like they
        // were bouncing rather than walking.
        [SerializeField, Min(0f)]
        private float hopHeight = 0.03f;

        [SerializeField, Min(0.1f)]
        private float hopsPerSecond = 3.8f;

        [SerializeField, Min(0f)]
        private float leanDegrees = 2f;

        private Vector3 _visualRestPosition;
        private Vector3 _lastPosition;
        private float _phase;
        private float _movingBlend;

        public float MovingBlend => _movingBlend;

        public void Configure(
            CompanionAgent configuredAgent,
            Transform configuredVisual)
        {
            agent = configuredAgent;
            visual = configuredVisual;
            ValidateOrThrow();
            _visualRestPosition = visual.localPosition;
            _lastPosition = transform.position;
            _phase = 0f;
            _movingBlend = 0f;
        }

        public void ValidateOrThrow()
        {
            if (visual == null)
            {
                throw new InvalidOperationException(
                    $"CompanionProceduralAnimator '{name}' requires a visual "
                    + "transform.");
            }
        }

        public void Tick(float deltaTime)
        {
            if (visual == null || deltaTime <= 0f)
            {
                return;
            }

            Vector3 current = transform.position;
            Vector3 delta = current - _lastPosition;
            delta.y = 0f;
            _lastPosition = current;

            float speed = delta.magnitude / deltaTime;
            bool disabled = agent != null && !agent.IsActive;
            float target = disabled || speed < 0.15f ? 0f : 1f;
            _movingBlend = Mathf.MoveTowards(
                _movingBlend,
                target,
                8f * deltaTime);

            if (_movingBlend <= 0.001f)
            {
                visual.localPosition = _visualRestPosition;
                visual.localRotation = Quaternion.identity;
                return;
            }

            _phase += deltaTime * hopsPerSecond * Mathf.PI * 2f;
            float hop = Mathf.Abs(Mathf.Sin(_phase))
                * hopHeight * _movingBlend;
            visual.localPosition =
                _visualRestPosition + new Vector3(0f, hop, 0f);
            visual.localRotation = Quaternion.Euler(
                -leanDegrees * _movingBlend,
                0f,
                0f);
        }

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }
    }
}
