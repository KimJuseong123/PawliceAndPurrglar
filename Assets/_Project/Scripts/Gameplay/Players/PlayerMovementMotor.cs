using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Match;
using PawsAndLoot.Gameplay.Loot;
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
        private float _airborneSince;

        /// <summary>
        /// Clears the accumulated fall speed.
        ///
        /// Has to be called by anything that places the character rather than
        /// walking them. Gravity accumulates every frame the controller is not
        /// grounded, and a teleport leaves it momentarily airborne — so going
        /// through a door repeatedly built the speed up until it was fast enough
        /// to punch straight through the floor between two frames. That is the
        /// "sinking into the ground" a few doors in.
        /// </summary>
        public void ResetVerticalVelocity()
        {
            _verticalVelocity = 0f;
        }
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
            && !IsStunned
            && !IsJailed;

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
        /// Held in the cells after an arrest.
        ///
        /// Kept separate from the stun rather than reusing it. A stun has an
        /// immunity window afterwards so a thief cannot be chain-stunned, and
        /// borrowing that here would hand the thief immunity for coming out of
        /// jail. They are also different on screen — one is a few seconds of
        /// stars, the other is being taken off the map.
        ///
        /// Only the thief carries the component, so the police never resolve one
        /// and never stop moving.
        /// </summary>
        public bool IsJailed
        {
            get
            {
                ThiefJailState jail = ResolveJail();
                return jail != null && jail.IsJailed;
            }
        }

        private ThiefJailState _jail;
        private bool _lookedForJail;

        private ThiefJailState ResolveJail()
        {
            if (!_lookedForJail)
            {
                _jail = GetComponent<ThiefJailState>();
                _lookedForJail = true;
            }

            return _jail;
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


        /// <summary>

        /// Off the ground. Read by the pose so a jump looks like one, and

        /// replicated by the session so it looks like one on both screens.

        /// </summary>

        public bool IsAirborne { get; private set; }


        /// <summary>

        /// Seconds since leaving the ground, so a pose can ease in rather than

        /// snapping on the frame a foot lifts off a kerb.

        /// </summary>

        public float AirborneSeconds => _airborneSince;
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

        /// <summary>
        /// What the thief is carrying, in terms of how much it slows them.
        ///
        /// A pocket item is carried and costs nothing, so this is asked
        /// alongside the flag rather than instead of it: something can be held
        /// without being heavy, and code that wants to know "are your hands
        /// full" is asking a different question from "are you slow".
        /// </summary>
        public LootCarryType CarriedWeight { get; private set; } =
            LootCarryType.OneHand;

        /// <summary>
        /// A temporary change to this character's pace, on top of whatever they
        /// are carrying.
        ///
        /// Multiplied with the carry penalty rather than replacing it, because
        /// the two are different facts: an alarm makes the officer faster and a
        /// gold bar makes the thief slower, and a thief who is both alarmed and
        /// laden is both. Written as a multiplier so nothing has to know what
        /// else is already applied.
        /// </summary>
        public float BoostMultiplier { get; private set; } = 1f;
        public float BoostRemainingSeconds { get; private set; }

        public float MovementSpeedMultiplier =>
            (_lootCarryPenaltyActive && playerConfig != null
                ? LootCarryRules.SpeedMultiplier(
                    CarriedWeight,
                    playerConfig.LootCarrySpeedMultiplier)
                : 1f)
            * BoostMultiplier;
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
            ClearBoost();
        }

        public void SetLootCarryPenalty(bool active)
        {
            SetLootCarryPenalty(active, LootCarryType.OneHand);
        }

        public void SetLootCarryPenalty(
            bool active,
            LootCarryType carryType)
        {
            _lootCarryPenaltyActive = active;
            CarriedWeight = carryType;
        }

        /// <summary>
        /// Speeds this character up, or slows them down, for a while.
        ///
        /// Replaces rather than stacks. Two alarms going off should not make
        /// the officer twice as fast, and the second one should not be ignored
        /// either — it restarts the clock, which is what an alarm going off
        /// again means.
        /// </summary>
        public void ApplyBoost(float multiplier, float seconds)
        {
            if (seconds <= 0f || multiplier <= 0f)
            {
                return;
            }

            BoostMultiplier = multiplier;
            BoostRemainingSeconds = seconds;
        }

        public void ClearBoost()
        {
            BoostMultiplier = 1f;
            BoostRemainingSeconds = 0f;
        }

        public void TickBoost(float deltaTime)
        {
            if (BoostRemainingSeconds <= 0f || deltaTime <= 0f)
            {
                return;
            }

            BoostRemainingSeconds -= deltaTime;
            if (BoostRemainingSeconds <= 0f)
            {
                ClearBoost();
            }
        }

        public void Move(Vector2 input, float deltaTime)
        {
            ValidateDependencies();

            // Counted down here rather than in Update, so a character whose
            // simulation is paused does not quietly burn through an alarm they
            // were never able to run during.
            TickBoost(deltaTime);
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

                // Cleared, not left as it was. A motor that is not simulating is not
                // airborne, and a stale true here is read by the jump pose: the
                // character freezes in a star jump and stops walking, which is
                // exactly what happened to the walk test the moment the pose
                // existed. Anything switching the controller off is teleporting or
                // stopping the character, never mid-jump.
                IsAirborne = false;
                _airborneSince = 0f;
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

            // Off the ground, and for how long. A jump lasts under half a second, so
            // the pose that goes with it needs to know within a frame or two — and
            // the ground-stick term above means a grounded character always has a
            // small negative vertical speed, which is why this asks the controller
            // instead of looking at the number.
            IsAirborne = !characterController.isGrounded;
            _airborneSince = IsAirborne
                ? _airborneSince + deltaTime
                : 0f;

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

        /// <summary>
        /// Leaves the ground, if there is ground to leave.
        ///
        /// Only from standing on something: a jump in mid-air would let a player
        /// climb anything by pressing it repeatedly. Grounded is asked of the
        /// controller rather than tracked here, because the controller is what
        /// resolves the contact.
        ///
        /// Returns whether it happened, so the caller can tell a jump from a press
        /// that did nothing.
        /// </summary>
        public bool TryJump()
        {
            ValidateDependencies();
            if (!CanMove
                || characterController == null
                || !characterController.enabled
                || !characterController.isGrounded)
            {
                return false;
            }

            // Not indoors.
            //
            // There is nothing in a room worth jumping over, and a jump is how
            // a player gets onto a doorstep, from there onto a partition, and
            // over it into a back room the model draws as sealed. Lowering the
            // step height stopped them walking up; this stops them hopping up.
            var indoors =
                GetComponent<Gameplay.Interiors.PlayerInteriorState>();
            if (indoors != null && indoors.IsIndoors)
            {
                return false;
            }

            // Set, not added. Falling speed at the moment of the press is whatever
            // the ground-stick term left behind, and adding to it would make the
            // jump height depend on which frame the key landed on.
            _verticalVelocity = playerConfig.JumpSpeed;
            _airborneSince = 0f;
            return true;
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
