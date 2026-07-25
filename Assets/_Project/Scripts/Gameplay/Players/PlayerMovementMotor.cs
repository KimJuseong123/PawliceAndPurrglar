using System;
using PawsAndLoot.Config;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Gameplay.Players
{
    public sealed class PlayerMovementMotor : MonoBehaviour
    {
        [SerializeField]
        private CharacterController characterController;

        [SerializeField]
        private PlayerConfig playerConfig;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private Transform orientationReference;

        private IMatchStateReader _matchState;
        private float _verticalVelocity;

        public Vector3 LastPlanarVelocity { get; private set; }
        public bool CanMove => _matchState?.IsGameplayActive == true;

        public void Configure(
            CharacterController controller,
            PlayerConfig config,
            IMatchStateReader matchStateReader,
            Transform movementOrientation)
        {
            characterController = controller
                ? controller
                : throw new ArgumentNullException(nameof(controller));
            playerConfig = config
                ? config
                : throw new ArgumentNullException(nameof(config));
            _matchState = matchStateReader
                ?? throw new ArgumentNullException(nameof(matchStateReader));
            matchStateSource = matchStateReader as MonoBehaviour;
            orientationReference = movementOrientation;
        }

        public void Move(Vector2 input, float deltaTime)
        {
            ValidateDependencies();
            if (deltaTime <= 0f)
            {
                LastPlanarVelocity = Vector3.zero;
                return;
            }

            Vector3 direction = CanMove
                ? GetWorldDirection(input)
                : Vector3.zero;
            LastPlanarVelocity = direction * playerConfig.MoveSpeed;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.forward = direction;
            }

            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = Physics.gravity.y * 0.1f;
            }
            else
            {
                _verticalVelocity += Physics.gravity.y * deltaTime;
            }

            Vector3 velocity =
                LastPlanarVelocity + Vector3.up * _verticalVelocity;
            characterController.Move(velocity * deltaTime);
        }

        private void Awake()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            ValidateDependencies();
        }

        private Vector3 GetWorldDirection(Vector2 input)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            Vector3 forward = orientationReference
                ? orientationReference.forward
                : Vector3.forward;
            Vector3 right = orientationReference
                ? orientationReference.right
                : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return Vector3.ClampMagnitude(
                forward * clampedInput.y + right * clampedInput.x,
                1f);
        }

        private void ValidateDependencies()
        {
            if (characterController == null)
            {
                throw new InvalidOperationException(
                    "PlayerMovementMotor requires a CharacterController.");
            }

            if (playerConfig == null)
            {
                throw new InvalidOperationException(
                    "PlayerMovementMotor requires a PlayerConfig.");
            }

            if (_matchState == null)
            {
                throw new InvalidOperationException(
                    "PlayerMovementMotor requires an IMatchStateReader source.");
            }
        }
    }
}
