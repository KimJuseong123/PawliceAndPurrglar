using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Input;
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

        /// <summary>
        /// NET-006. The thief's running total, written only by the host.
        /// </summary>
        private readonly NetworkVariable<int> _soldAmount =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// NET-007. The police's arrest progress and its outcome.
        /// </summary>
        private readonly NetworkVariable<float> _arrestSeconds =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _arrestCompleted =
            new(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _arrestInterruptions =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _arrestInterruptReason =
            new(
                0,
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

        [SerializeField]
        private PlayerInteractionScanner scanner;

        [SerializeField]
        private PlayerInteractionInput interactionInput;

        [SerializeField]
        private LootCarrier carrier;

        [SerializeField]
        private LootDropInput dropInput;

        [SerializeField]
        private CompanionCommandKeyboardInput companionInput;

        [SerializeField]
        private ThiefLootWallet wallet;

        [SerializeField]
        private ArrestProgressController arrestProgress;

        [SerializeField]
        private PawsAndLoot.Gameplay.Items.ToolUseAction toolUse;

        [SerializeField]
        private PawsAndLoot.Gameplay.Items.ToolUseInput toolInput;

        [SerializeField]
        private StunState stun;

        /// <summary>
        /// The local replay of a throw. Presentation, so it is driven rather
        /// than consulted.
        /// </summary>
        [SerializeField]
        private PawsAndLoot.Animation.ThrowPresenter throwPresenter;

        [SerializeField]
        private PawsAndLoot.Gameplay.Items.ToolCarrier toolCarrier;

        [SerializeField]
        private PoliceWallet policeWallet;

        /// <summary>
        /// The procedural walk. Driven on machines that do not simulate this
        /// player, because a replicated transform is too stuttery to derive a
        /// speed from.
        /// </summary>
        [SerializeField]
        private PawsAndLoot.Animation.CompanionLegAnimator legAnimator;

        /// <summary>
        /// THROW-011. The officer's purse, written only by the host.
        ///
        /// Replicated for the same reason the tool slot is: the officer decides
        /// what to buy on their own screen, and a purse that only the host knew
        /// about would tell them they cannot afford something the host would
        /// happily sell them.
        /// </summary>
        private readonly NetworkVariable<int> _policeAmount =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// THROW-005. What this player is holding: -1 for nothing, otherwise the
        /// <c>ThrowableKind</c>.
        ///
        /// Replicated because the tool slot had no replication at all, and the
        /// result was reported as "the rock won't pick up". The press did reach
        /// the host and the host did take the rock — the other machine simply
        /// never heard, so its HUD kept saying the hand was empty and the rock
        /// stayed lying in the road. Nothing about the pickup was broken except
        /// that only one of the two players could see it.
        /// </summary>
        private readonly NetworkVariable<int> _heldTool =
            new(
                -1,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// THROW-007. Seconds of stun left, written only by the host.
        ///
        /// Replicated because the host is the only machine that decides a throw
        /// connected. Without this a client keeps running on its own screen
        /// while the host holds it still, and the two players see different
        /// chases.
        /// </summary>
        private readonly NetworkVariable<float> _stunSeconds =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// Counts stuns so a client can tell a fresh hit from a value that
        /// happens to be the same, the way arrest interruptions are counted.
        /// </summary>
        private readonly NetworkVariable<int> _stunCount =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private int _appliedStunCount;

        [SerializeField, Min(1f)]
        private float catchUpSpeed = 14f;

        /// <summary>
        /// Beyond this the gap is closed instantly. A long stall would otherwise
        /// show the character sliding across the map.
        /// </summary>
        [SerializeField, Min(1f)]
        private float snapDistance = 4f;

        /// <summary>
        /// Time constant for following the replicated position.
        ///
        /// Small enough that the character is never visibly behind, large enough
        /// that a late packet is absorbed instead of producing a lurch. This is
        /// the number that decides whether a guest's characters glide or judder.
        /// </summary>
        [SerializeField, Range(0.02f, 0.4f)]
        private float followSmoothSeconds = 0.09f;

        private Vector3 _followVelocity;

        private Vector2 _submittedMove;
        private bool _submittedDash;
        private bool _remoteDriven;

        public PlayerRole Role =>
            identity != null ? identity.Role : PlayerRole.Police;
        public Vector3 ReplicatedPosition => _position.Value;
        public float ReplicatedNormalizedSpeed => _normalizedSpeed.Value;
        public bool IsRemoteDriven => _remoteDriven;
        public int ReplicatedSoldAmount => _soldAmount.Value;
        public float ReplicatedArrestSeconds => _arrestSeconds.Value;
        public bool ReplicatedArrestCompleted => _arrestCompleted.Value;

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

        /// <summary>
        /// NET-005 to NET-007. Supplies the gameplay components this link routes
        /// requests into and replicates results from. Kept separate from
        /// <see cref="Configure"/> so movement stays usable on its own.
        /// </summary>
        public void ConfigureGameplay(
            PlayerInteractionScanner configuredScanner,
            PlayerInteractionInput configuredInteractionInput,
            LootCarrier configuredCarrier,
            LootDropInput configuredDropInput,
            CompanionCommandKeyboardInput configuredCompanionInput,
            ThiefLootWallet configuredWallet,
            ArrestProgressController configuredArrestProgress,
            PawsAndLoot.Gameplay.Items.ToolUseAction configuredToolUse = null,
            PawsAndLoot.Gameplay.Items.ToolUseInput configuredToolInput = null,
            StunState configuredStun = null,
            PawsAndLoot.Animation.ThrowPresenter configuredThrowPresenter =
                null,
            PawsAndLoot.Gameplay.Items.ToolCarrier configuredToolCarrier =
                null,
            PoliceWallet configuredPoliceWallet = null,
            PawsAndLoot.Animation.CompanionLegAnimator configuredLegAnimator =
                null)
        {
            policeWallet = configuredPoliceWallet;
            legAnimator = configuredLegAnimator;
            throwPresenter = configuredThrowPresenter;
            toolCarrier = configuredToolCarrier;
            scanner = configuredScanner;
            interactionInput = configuredInteractionInput;
            carrier = configuredCarrier;
            dropInput = configuredDropInput;
            companionInput = configuredCompanionInput;
            wallet = configuredWallet;
            arrestProgress = configuredArrestProgress;
            toolUse = configuredToolUse;
            toolInput = configuredToolInput;
            stun = configuredStun;
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

            // NET-005/007. The same rule as movement: only the host runs the
            // rules. A client's loot and arrest components become displays.
            if (arrestProgress != null)
            {
                arrestProgress.SetRemoteControlled(_remoteDriven);
            }

            if (IsServer)
            {
                _position.Value = transform.position;
                _yaw.Value = transform.eulerAngles.y;
                // Counted on the host so a client can tell one hit from the
                // next; the duration alone repeats and would be missed.
                if (stun != null)
                {
                    stun.Stunned += HandleHostStunApplied;
                }

                // Only the host ever raises this, because only the host
                // resolves a throw. The other machine is told.
                if (toolUse != null)
                {
                    toolUse.Thrown += HandleHostThrew;
                }
            }
        }

        private void HandleHostStunApplied(float seconds)
        {
            if (!IsServer)
            {
                return;
            }

            _stunCount.Value++;
            _stunSeconds.Value = seconds;
        }

        public override void OnNetworkDespawn()
        {
            if (arrestProgress != null)
            {
                arrestProgress.SetRemoteControlled(false);
            }

            if (stun != null)
            {
                stun.Stunned -= HandleHostStunApplied;
            }

            if (toolUse != null)
            {
                toolUse.Thrown -= HandleHostThrew;
            }
        }

        /// <summary>
        /// NET-005. Asks the host to run this player's interact key.
        ///
        /// The host re-scans on its own simulation, so it — not the requester —
        /// decides what is in range and whether the loot is still free. Two
        /// players pressing at once produce two server calls in some order, and
        /// the second finds the item already carried.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitInteractRpc()
        {
            if (scanner != null)
            {
                scanner.TryInteractCurrent();
            }
        }

        /// <summary>
        /// NET-005. Asks the host to drop whatever this player carries.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitDropRpc()
        {
            if (carrier != null)
            {
                carrier.TryDrop();
            }
        }

        /// <summary>
        /// THROW-007. Asks the host to use this player's held prop, aimed where
        /// the requester's cursor was.
        ///
        /// The host resolves the throw against its own simulation, so it — not
        /// the thrower — decides whether the rock connected. Two machines
        /// judging the same throw would disagree, and the loser of that
        /// disagreement would be stunned on one screen and running on the other.
        ///
        /// The aim travels with the request because it is the one thing the host
        /// genuinely cannot know: it comes from a cursor on somebody else's
        /// screen. Everything decided from it — range, walls, who was in the
        /// corridor — is still decided here.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitUseToolRpc(Vector3 aimDirection)
        {
            if (toolUse == null)
            {
                return;
            }

            Vector3 flat = aimDirection;
            flat.y = 0f;
            toolUse.TryUse(
                flat.sqrMagnitude > 0.0001f
                    ? flat.normalized
                    : null);
        }

        /// <summary>
        /// Tells the other machine what a throw looked like.
        ///
        /// Cosmetic only, and one-way: the flight is a replay of a decision the
        /// host has already made and applied, so nothing here can change the
        /// outcome. It is sent rather than recomputed because a client has no
        /// idea a throw happened at all — the stun would simply appear, with no
        /// rock and no swing to explain it.
        /// </summary>
        [Rpc(SendTo.NotServer)]
        private void PlayThrowRpc(
            int kind,
            Vector3 origin,
            Vector3 landing)
        {
            if (throwPresenter != null)
            {
                throwPresenter.Play(
                    (PawsAndLoot.Gameplay.Items.ThrowableKind)kind,
                    origin,
                    landing);
            }
        }

        private void HandleHostThrew(
            PawsAndLoot.Gameplay.Items.ThrowableKind kind,
            PawsAndLoot.Gameplay.Items.ThrowResolver.Result result)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            PlayThrowRpc((int)kind, result.Origin, result.Landing);
        }

        /// <summary>
        /// Companion commands take the same route, so a client's dog obeys the
        /// host's single simulation rather than a local copy that would drift.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitCompanionCommandRpc(int numberKey)
        {
            if (companionInput != null)
            {
                companionInput.TryIssue(numberKey, Time.time);
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
                PublishGameplayState();
                return;
            }

            ApplyReplicatedTransform(Time.deltaTime);
            ApplyReplicatedGameplayState();
        }

        private void PublishGameplayState()
        {
            if (stun != null)
            {
                _stunSeconds.Value = stun.RemainingSeconds;
            }

            if (toolCarrier != null)
            {
                _heldTool.Value = toolCarrier.HasTool
                    ? (int)toolCarrier.HeldKind
                    : -1;
            }

            if (policeWallet != null)
            {
                _policeAmount.Value = policeWallet.Amount;
            }

            if (wallet != null)
            {
                _soldAmount.Value = wallet.SoldAmount;
            }

            if (arrestProgress == null)
            {
                return;
            }

            _arrestSeconds.Value = arrestProgress.ProgressSeconds;
            _arrestCompleted.Value = arrestProgress.IsCompleted;
            _arrestInterruptions.Value =
                arrestProgress.InterruptionCount;
            _arrestInterruptReason.Value =
                (int)arrestProgress.LastInterruptionReason;
        }

        private void ApplyReplicatedGameplayState()
        {
            // A stun the host applied has to hold this player still here too,
            // otherwise the client walks away from a hit that landed.
            if (stun != null
                && _stunCount.Value > _appliedStunCount)
            {
                _appliedStunCount = _stunCount.Value;
                stun.TryApply(_stunSeconds.Value);
            }

            if (policeWallet != null)
            {
                policeWallet.ApplyReplicated(_policeAmount.Value);
            }

            // What is in hand, so the HUD on this screen matches the hand the
            // host is actually simulating.
            if (toolCarrier != null)
            {
                int held = _heldTool.Value;
                toolCarrier.ApplyReplicated(
                    held >= 0,
                    held >= 0
                        ? (PawsAndLoot.Gameplay.Items.ThrowableKind)held
                        : PawsAndLoot.Gameplay.Items.ThrowableKind.Rock);
            }

            if (wallet != null)
            {
                wallet.ApplyRemoteSale(_soldAmount.Value);
            }

            if (arrestProgress == null)
            {
                return;
            }

            // Interruption first, so a same-frame reset then re-progress does
            // not read as progress being wiped.
            arrestProgress.ApplyRemoteInterruption(
                _arrestInterruptions.Value,
                (ArrestInterruptionReason)
                    _arrestInterruptReason.Value);
            arrestProgress.ApplyRemoteProgress(
                _arrestSeconds.Value,
                _arrestCompleted.Value);
        }

        private void ApplyReplicatedTransform(float deltaTime)
        {
            Vector3 target = _position.Value;
            float distance = Vector3.Distance(transform.position, target);
            if (distance > snapDistance)
            {
                // A long stall would otherwise show the character sliding
                // across the map.
                transform.position = target;
                _followVelocity = Vector3.zero;
            }
            else
            {
                // Smoothed rather than raced.
                //
                // MoveTowards at 14 m/s covers the gap to a target that only
                // updates a few times a second, so the character sprinted, sat
                // still, sprinted, sat still — which is the juddering reported on
                // whichever machine was the guest. Both characters are
                // remote-driven on a client, so both shook; the one the camera
                // follows was simply the one anybody noticed.
                //
                // An exponential approach never arrives and never stalls, so the
                // motion is continuous and averages out to the true speed with a
                // fraction of a second of lag. Cheaper than a full interpolation
                // buffer and enough for two players on a LAN.
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    target,
                    ref _followVelocity,
                    followSmoothSeconds,
                    catchUpSpeed,
                    deltaTime);
            }
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.Euler(0f, _yaw.Value, 0f),
                Mathf.Clamp01(deltaTime * 12f));

            // Hand the walk the host's speed instead of letting it measure this
            // stuttering transform. The correction above arrives at its target
            // and then waits for the next packet, so a measured speed reads zero
            // on most frames and the character slides with still legs.
            if (legAnimator != null && motor != null)
            {
                legAnimator.SetExternalSpeed(
                    _normalizedSpeed.Value * motor.EffectiveMoveSpeed);
            }
        }
    }
}
