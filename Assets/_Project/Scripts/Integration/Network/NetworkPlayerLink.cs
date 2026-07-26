using PawsAndLoot.Gameplay.Players;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// NET-003. Moves one player across the network under host authority.
    ///
    /// Input flows one way and state flows back:
    ///
    ///     client input -> RPC -> host simulates -> position replicates
    ///
    /// The host is the only machine that runs the movement motor, so a client
    /// cannot place itself anywhere it likes. Each machine submits input only
    /// for its own role, which is what stops one player driving the other.
    ///
    /// Positions are interpolated on the receiving side rather than snapped, so
    /// a late packet reads as a short catch-up instead of a teleport.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerLink : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> _position =
            new(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _yaw =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// Planar speed as a fraction of run speed, so the receiving side can
        /// drive the same locomotion blend the owner sees.
        /// </summary>
        private readonly NetworkVariable<float> _normalizedSpeed =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private PlayerMovementMotor motor;

        [SerializeField]
        private PlayerKeyboardInput keyboardInput;

        [SerializeField]
        private CharacterController characterController;

        [SerializeField, Min(1f)]
        private float catchUpSpeed = 14f;

        /// <summary>
        /// Beyond this the gap is closed instantly. A long stall would otherwise
        /// show the character sliding across the map.
        /// </summary>
        [SerializeField, Min(1f)]
        private float snapDistance = 4f;

        private Vector2 _submittedMove;
        private bool _submittedDash;
        private bool _remoteDriven;

        public PlayerRole Role =>
            identity != null ? identity.Role : PlayerRole.Police;
        public Vector3 ReplicatedPosition => _position.Value;
        public float ReplicatedNormalizedSpeed => _normalizedSpeed.Value;
        public bool IsRemoteDriven => _remoteDriven;

        public void Configure(
            PlayerRoleIdentity configuredIdentity,
            PlayerMovementMotor configuredMotor,
            PlayerKeyboardInput configuredInput,
            CharacterController configuredController)
        {
            identity = configuredIdentity;
            motor = configuredMotor;
            keyboardInput = configuredInput;
            characterController = configuredController;
        }

        public override void OnNetworkSpawn()
        {
            // The host simulates both players. Every other machine only
            // displays them, so its motor and controller are switched off to
            // keep local physics from fighting the replicated position.
            _remoteDriven = !IsServer;
            if (_remoteDriven)
            {
                if (motor != null)
                {
                    motor.enabled = false;
                }

                if (characterController != null)
                {
                    characterController.enabled = false;
                }
            }

            if (IsServer)
            {
                _position.Value = transform.position;
                _yaw.Value = transform.eulerAngles.y;
            }
        }

        /// <summary>
        /// Sends this machine's input for its own role. Called by the input
        /// bridge, which knows which role is local.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitInputRpc(Vector2 move, bool dashPressed)
        {
            _submittedMove = Vector2.ClampMagnitude(move, 1f);
            if (dashPressed)
            {
                _submittedDash = true;
            }
        }

        /// <summary>
        /// Consumes the queued remote input. The host applies it through the
        /// same motor the local player uses, so both roles obey identical rules.
        /// </summary>
        public void ApplySubmittedInput(float deltaTime)
        {
            if (!IsServer || motor == null)
            {
                return;
            }

            if (_submittedDash)
            {
                _submittedDash = false;
                motor.TryStartDash(_submittedMove);
            }

            motor.Move(_submittedMove, deltaTime);
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                // The host is the only simulator, so it drives both roles from
                // whatever input each machine last submitted.
                ApplySubmittedInput(Time.deltaTime);
                _position.Value = transform.position;
                _yaw.Value = transform.eulerAngles.y;
                _normalizedSpeed.Value = motor != null
                    && motor.EffectiveMoveSpeed > 0.01f
                        ? Mathf.Clamp01(
                            motor.LastPlanarVelocity.magnitude
                            / motor.EffectiveMoveSpeed)
                        : 0f;
                return;
            }

            ApplyReplicatedTransform(Time.deltaTime);
        }

        private void ApplyReplicatedTransform(float deltaTime)
        {
            Vector3 target = _position.Value;
            float distance = Vector3.Distance(transform.position, target);
            transform.position = distance > snapDistance
                ? target
                : Vector3.MoveTowards(
                    transform.position,
                    target,
                    Mathf.Max(catchUpSpeed, distance) * deltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(0f, _yaw.Value, 0f),
                Mathf.Clamp01(deltaTime * 12f));
        }
    }
}
