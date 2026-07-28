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
        private StunState _stun;
        private bool _lookedForStun;
        private float _verticalVelocity;
        private Vector3 _dashDirection;
        private float _dashRemainingSeconds;
        private float _dashCooldownRemainingSeconds;
        private bool _lootCarryPenaltyActive;

        public Vector3 LastPlanarVelocity { get; private set; }

        /// <summary>
        /// A stun suppresses movement here rather than in the input layer, so it
        /// applies however the input arrived: local keys, a network RPC or a
        /// test. Blocking it at the keyboard would leave a networked player
        /// still walking on the host.
        ///
        /// Resolved lazily because the stun component is optional; a player
        /// without one simply never gets stunned.
        /// </summary>
        public bool CanMove =>
            _matchState?.IsGameplayActive == true
            && !IsStunned;

        /// <summary>
        /// False when there is no stun component at all, so a player without one
        /// moves normally rather than being frozen forever.
        /// </summary>
        public bool IsStunned
        {
            get
            {
                StunState stun = ResolveStun();
                return stun != null && stun.IsStunned;
            }
        }
        /// <summary>
        /// Cached after the first look so the lookup is not repeated every
        /// frame, and so a player deliberately built without a stun component
        /// is not searched for one over and over.
        /// </summary>
        private StunState ResolveStun()
        {
            if (_lookedForStun)
            {
                return _stun;
            }

            _lookedForStun = true;
            _stun = GetComponent<StunState>();
            return _stun;
        }

        public bool IsDashing => _dashRemainingSeconds > 0f;
        public float DashCooldownRemainingSeconds =>
            _dashCooldownRemainingSeconds;
        public float DashCooldownNormalized =>
            playerConfig != null && playerConfig.DashCooldownSeconds > 0f
                ? Mathf.Clamp01(
                    _dashCooldownRemainingSeconds
                    / playerConfig.DashCooldownSeconds)
                : 0f;
        public bool IsLootCarryPenaltyActive =>
            _lootCarryPenaltyActive;
        public float MovementSpeedMultiplier =>
            _lootCarryPenaltyActive && playerConfig != null
                ? playerConfig.LootCarrySpeedMultiplier
                : 1f;
        public float EffectiveMoveSpeed =>
            playerConfig != null
                ? playerConfig.MoveSpeed * MovementSpeedMultiplier
                : 0f;

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
            _lootCarryPenaltyActive = false;
        }

        public void SetLootCarryPenalty(bool active)
        {
            _lootCarryPenaltyActive = active;
        }

        public void Move(Vector2 input, float deltaTime)
        {
            ValidateDependencies();
            if (deltaTime <= 0f)
            {
                LastPlanarVelocity = Vector3.zero;
                return;
            }

            // A disabled controller means this character is driven from
            // somewhere else — in a session the host owns it and the transform
            // arrives by replication. Moving it here is meaningless, and
            // CharacterController logs an error per frame if asked, which
            // buries every other message in the console.
            //
            // The companion agent already guards the same way.
            if (characterController == null
                || !characterController.enabled)
            {
                LastPlanarVelocity = Vector3.zero;
                return;
            }

            _dashCooldownRemainingSeconds = Mathf.Max(
                0f,
                _dashCooldownRemainingSeconds - deltaTime);
            if (!CanMove)
            {
                _dashRemainingSeconds = 0f;
            }

            Vector3 direction = Vector3.zero;
            float speed = playerConfig.MoveSpeed;
            if (CanMove && IsDashing)
            {
                direction = _dashDirection;
                speed = playerConfig.DashSpeed;
            }
            else if (CanMove)
            {
                direction = GetWorldDirection(input);
            }

            speed *= MovementSpeedMultiplier;
            LastPlanarVelocity = direction * speed;

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

            if (IsDashing)
            {
                _dashRemainingSeconds = Mathf.Max(
                    0f,
                    _dashRemainingSeconds - deltaTime);
            }
        }

        public bool TryStartDash(Vector2 input)
        {
            ValidateDependencies();
            if (!CanMove
                || IsDashing
                || _dashCooldownRemainingSeconds > 0f)
            {
                return false;
            }

            Vector3 requestedDirection = GetWorldDirection(input);
            if (requestedDirection.sqrMagnitude <= 0.0001f)
            {
                requestedDirection = transform.forward;
                requestedDirection.y = 0f;
                if (requestedDirection.sqrMagnitude <= 0.0001f)
                {
                    requestedDirection = Vector3.forward;
                }

                requestedDirection.Normalize();
            }

            _dashDirection = requestedDirection;
            _dashRemainingSeconds = playerConfig.DashDurationSeconds;
            _dashCooldownRemainingSeconds =
                playerConfig.DashCooldownSeconds;
            return true;
        }

        private void Awake()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            ValidateDependencies();
        }

        private void OnDisable()
        {
            _lootCarryPenaltyActive = false;
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
