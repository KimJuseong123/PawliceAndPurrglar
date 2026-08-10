using System;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// Drives the locomotion blend from the movement motor.
    ///
    /// This layer only reads gameplay state. It never moves the character and
    /// never changes match state, so animation can be swapped or removed
    /// without affecting the rules. Root motion stays off because the
    /// CharacterController owns displacement.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotionAnimator : MonoBehaviour
    {
        public const string SpeedParameter =
            CharacterAnimatorParameters.Speed;

        [SerializeField]
        private PlayerMovementMotor movementMotor;

        [SerializeField]
        private Animator animator;

        [SerializeField, Min(0.01f)]
        private float referenceSpeed = 5f;

        [SerializeField, Min(0f)]
        private float smoothingPerSecond = 12f;

        private static readonly int SpeedId =
            Animator.StringToHash(SpeedParameter);

        private float _normalizedSpeed;

        /// <summary>
        /// Planar speed as a 0..1 fraction of the reference speed, after
        /// smoothing. Exposed so tests can assert the blend without an
        /// Animator asset.
        /// </summary>
        public float NormalizedSpeed => _normalizedSpeed;

        public void Configure(
            PlayerMovementMotor configuredMotor,
            Animator configuredAnimator,
            float configuredReferenceSpeed)
        {
            movementMotor = configuredMotor;
            animator = configuredAnimator;
            referenceSpeed = Mathf.Max(0.01f, configuredReferenceSpeed);
            _normalizedSpeed = 0f;
            ValidateOrThrow();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
        }

        public void ValidateOrThrow()
        {
            if (movementMotor == null)
            {
                throw new InvalidOperationException(
                    $"PlayerLocomotionAnimator '{name}' requires a "
                    + "PlayerMovementMotor.");
            }
        }

        public void Tick(float deltaTime)
        {
            ValidateOrThrow();
            Vector3 planar = movementMotor.LastPlanarVelocity;
            planar.y = 0f;
            float target = Mathf.Clamp01(
                planar.magnitude / referenceSpeed);

            _normalizedSpeed = smoothingPerSecond <= 0f
                ? target
                : Mathf.MoveTowards(
                    _normalizedSpeed,
                    target,
                    smoothingPerSecond * Mathf.Max(0f, deltaTime));

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedId, _normalizedSpeed);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
