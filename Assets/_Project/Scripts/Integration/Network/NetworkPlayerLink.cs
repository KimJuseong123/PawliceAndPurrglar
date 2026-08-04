using System;
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
        /// Where this player's animal is, and how fast it is walking.
        ///
        /// The animals were never replicated at all. Both machines ran their own
        /// copy of the agent, and because commands only ever reach the host, the
        /// client's animal followed its owner and did nothing else — no orders,
        /// no lures, no scouting. It looked like a pet that had stopped
        /// listening, and only came to light when a prop made somebody watch it.
        ///
        /// Host simulates and the client is shown the result, exactly like the
        /// players. Two simulations of the same object driven by different
        /// inputs cannot agree, and this one was not even trying to.
        /// </summary>
        private readonly NetworkVariable<Vector3> _companionPosition =
            new(
                Vector3.zero,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _companionYaw =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// Metres per second, sent rather than measured.
        ///
        /// A replicated position arrives in steps and sits still between
        /// packets, so a client measuring frame-to-frame movement reads mostly
        /// zero and the legs stop. The host knows the real speed.
        /// </summary>
        private readonly NetworkVariable<float> _companionSpeed =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// What this player's animal is showing above its head.
        ///
        /// The animals are scene objects rather than spawned network objects,
        /// so nothing about them replicates on its own — their whole state
        /// lives on the host. That was invisible while they only moved, because
        /// their positions are driven from the host anyway, and it showed up the
        /// moment they had something to say: the thief could not see their own
        /// cat react.
        ///
        /// The face and a counter are packed into one int. Without the counter
        /// the same face twice in a row is not a change, so a dog that fails to
        /// find a trail twice would look like it heard the second order and
        /// ignored it — which is the exact confusion these icons exist to fix.
        /// </summary>
        private readonly NetworkVariable<int> _companionFace =
            new(
                0,
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
        private PawsAndLoot.Animation.CompanionExpressionView companionFace;

        [SerializeField]
        private PawsAndLoot.Companions.CompanionAgent companionAgent;

        /// <summary>
        /// How far the client's animal may be from where the host says it is
        /// before it is put there instead of walked there.
        ///
        /// Wide enough that ordinary packet lag still eases, which is what
        /// keeps the animal from stuttering, and narrow enough that going
        /// through a door does not become a long walk.
        /// </summary>
        private const float SnapDistance = 4f;

        private Vector3 _companionLastPosition;
        private float _companionReportAt;

        private int _lastCompanionFaceSequence;
        private int _companionFaceSequence;

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

        [SerializeField]
        private PawsAndLoot.Gameplay.Interiors.PlayerInteriorState
            interiorState;

        /// <summary>
        /// MAP-008. Which house this player is inside, written only by the host.
        ///
        /// Replicated because three separate things read it and two of them are on
        /// the other machine: the indoor camera switches on the player's own
        /// screen, and the dog reports a house rather than a position on the
        /// officer's. A client that guessed would put the camera in a room while
        /// the host still had the character in the street.
        /// </summary>
        private readonly NetworkVariable<int> _interiorId =
            new(
                PawsAndLoot.Gameplay.Interiors.PlayerInteriorState.Outside,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

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

        private readonly NetworkVariable<int> _toolSlots =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _toolQuantities =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _selectedToolSlot =
            new(
                0,
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
        /// <summary>
        /// How many times this officer has caught the thief, and how long this
        /// thief has left in the cells.
        ///
        /// Display only — neither decides anything. They are here because the
        /// screen was reading them off components that only ever run on the
        /// host: a client watched the whole match with the counter stuck on
        /// zero and no idea how long they were locked up for. The rules were
        /// working and the player could not see them, which is a different
        /// failure from the rules not working and is just as bad to play.
        /// </summary>
        private readonly NetworkVariable<int> _catchCount =
            new(0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _jailSeconds =
            new(0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _stunCount =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// Off the ground, replicated.
        ///
        /// The pose cannot work this out for itself on a client. A replicated
        /// character is moved by writing its position, not by its controller, so
        /// <c>isGrounded</c> is meaningless there — and reading the vertical
        /// motion instead would be measuring a value that arrives in lumps, which
        /// is the mistake that made both characters look legless on the client
        /// (ISSUE-026). The host says so.
        /// </summary>
        private readonly NetworkVariable<bool> _airborne =
            new(
                false,
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
        private float? _submittedYaw;
        private bool _submittedDash;
        private bool _submittedJump;
        private bool _remoteDriven;

        public event Action<ulong, string, string>
            VoiceCommandMetadataReceived;
        public event Action<string, string, string, string, int, int, int>
            VoiceCommandEventReceived;

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
                null,
            PawsAndLoot.Gameplay.Interiors.PlayerInteriorState
                configuredInteriorState = null,
            PawsAndLoot.Animation.CompanionExpressionView
                configuredCompanionFace = null)
        {
            companionFace = configuredCompanionFace;
            companionAgent = configuredCompanionFace != null
                ? configuredCompanionFace
                    .GetComponent<PawsAndLoot.Companions.CompanionAgent>()
                : null;
            interiorState = configuredInteriorState;
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
            // Only the host decides where a character is, so only the host may act
            // on one walking into a doorway. A client's capsule passes through the
            // same trigger while following replicated positions.
            interiorState?.SetAuthority(IsServer);

            // Same reason as the interior: a client running its own sentence
            // teleports the thief on its screen only, and its clock drifts from
            // the host's.
            GetComponent<PawsAndLoot.Gameplay.Arrest.ThiefJailState>()
                ?.SetAuthority(IsServer);

            // The animal's own presenter decides faces from events the host
            // raises, so on a client it would either say nothing or disagree
            // with what the host sent. Turning it off leaves exactly one writer.
            // The client stops simulating its own animal. Left running, it
            // walks after its owner on that screen while the host walks it
            // somewhere else, and the two never agree.
            if (!IsServer && companionAgent != null)
            {
                companionAgent.enabled = false;
            }

            if (!IsServer && companionFace != null)
            {
                var presenter = companionFace
                    .GetComponent<
                        PawsAndLoot.Companions.CompanionExpressionPresenter>();
                if (presenter != null)
                {
                    presenter.enabled = false;
                }
            }

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
        public void SubmitUseToolRpc(Vector3 aimDirection, float charge01)
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
                    : null,
                charge01);
        }

        [Rpc(SendTo.Server)]
        public void SubmitSelectToolSlotRpc(int slot)
        {
            toolCarrier?.SelectSlot(slot);
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
        /// Registers the lightweight part of a voice command with the Host.
        /// The audio never travels through NGO; the Host uses this callback to
        /// prepare the authoritative world context before classification.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitVoiceCommandMetadataRpc(
            string clientCommandId,
            string petId)
        {
            if (string.IsNullOrWhiteSpace(clientCommandId)
                || string.IsNullOrWhiteSpace(petId))
            {
                return;
            }

            VoiceCommandMetadataReceived?.Invoke(
                OwnerClientId,
                clientCommandId,
                petId);
        }

        /// <summary>
        /// Host-only result publication. Clients never calculate cognition or
        /// random outcomes; they only receive this presentation/event data.
        /// </summary>
        public void PublishVoiceCommandEvent(
            string commandId,
            string eventType,
            string transcript,
            string targetId,
            int resultType,
            int reaction,
            int action)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            VoiceCommandEventReceived?.Invoke(
                commandId,
                eventType,
                transcript,
                targetId,
                resultType,
                reaction,
                action);
            BroadcastVoiceCommandEventRpc(
                commandId,
                eventType,
                transcript,
                targetId,
                resultType,
                reaction,
                action);
        }

        [Rpc(SendTo.NotServer)]
        private void BroadcastVoiceCommandEventRpc(
            string commandId,
            string eventType,
            string transcript,
            string targetId,
            int resultType,
            int reaction,
            int action)
        {
            VoiceCommandEventReceived?.Invoke(
                commandId,
                eventType,
                transcript,
                targetId,
                resultType,
                reaction,
                action);
        }

        /// <summary>
        /// Sends this machine's input for its own role. Called by the input
        /// bridge, which knows which role is local.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitInputRpc(
            Vector2 move,
            bool dashPressed,
            float orientationYaw = float.NaN)
        {
            _submittedMove = Vector2.ClampMagnitude(move, 1f);

            // Which way the sender was looking when they pressed it.
            //
            // Without this the host measures a client's WASD against the host's
            // own camera. Outdoors that is the same fixed angle for both and
            // nothing shows; indoors the client orbits their view and their
            // keys arrive rotated by however far apart the two cameras are.
            _submittedYaw = float.IsNaN(orientationYaw)
                ? (float?)null
                : orientationYaw;
            if (dashPressed)
            {
                _submittedDash = true;
            }
        }

        /// <summary>
        /// A jump, as its own message rather than another flag on the movement one.
        ///
        /// Movement is a stream and a jump is an event. Riding along on the movement
        /// packet would mean the press is only heard if it lands on a frame the
        /// stream happens to be sent, which is how a jump comes to work four times
        /// out of five. This follows <c>SubmitInteractRpc</c>, which is an event for
        /// the same reason.
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitJumpRpc()
        {
            _submittedJump = true;
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

            if (_submittedJump)
            {
                _submittedJump = false;
                motor.TryJump();
            }

            motor.SetOrientationYaw(_submittedYaw);
            motor.Move(_submittedMove, deltaTime);
        }

        /// <summary>
        /// Sends the face the host's animal is already showing.
        ///
        /// Read off the view rather than driven from the events, so there is
        /// one decision about which face to show and the client cannot disagree
        /// with the host about it.
        /// </summary>
        private void PublishCompanionTransform()
        {
            if (companionAgent == null)
            {
                return;
            }

            Transform animal = companionAgent.transform;
            Vector3 position = animal.position;
            _companionPosition.Value = position;
            _companionYaw.Value = animal.eulerAngles.y;
            _companionSpeed.Value = Time.deltaTime > 0f
                ? Vector3.Distance(
                    new Vector3(position.x, 0f, position.z),
                    new Vector3(
                        _companionLastPosition.x,
                        0f,
                        _companionLastPosition.z))
                    / Time.deltaTime
                : 0f;
            _companionLastPosition = position;
        }

        /// <summary>
        /// Places the client's animal where the host says it is.
        ///
        /// Eased rather than snapped, because the packets arrive far apart
        /// compared with the frame rate and a snapped animal reads as a
        /// stutter. The legs are driven from the sent speed rather than from
        /// this movement, which is mostly zero between packets.
        /// </summary>
        /// <summary>
        /// Complains, on the client, when this player has no animal wired.
        ///
        /// The cat was on the host's screen and not on the client's, and the
        /// three things that cause that — no animal, an animal nobody moves,
        /// and an animal moved somewhere else — look identical from outside the
        /// window. It was the third, and finding that out took printing all
        /// three. What is left is the one that cannot be seen any other way: a
        /// link with no animal replicates nothing and says nothing, and the
        /// animal on each machine quietly goes its own way.
        /// </summary>
        private void ReportCompanion()
        {
            if (companionAgent != null || Time.time < _companionReportAt)
            {
                return;
            }

            _companionReportAt = Time.time + 5f;
            PawsAndLoot.Logging.GameLogger.Warning(
                PawsAndLoot.Logging.GameLogCategory.Companion,
                $"{Role} link has no companion wired, so nothing about this "
                + "animal reaches the client.",
                this);
        }

        private void ApplyCompanionTransform(float deltaTime)
        {
            if (companionAgent == null)
            {
                return;
            }

            // The local brain is switched off first, and only here.
            //
            // Position was already being replicated and the animal still stood
            // somewhere else on the client, because the client was *also*
            // running its own CompanionAgent: two writers to one transform,
            // every frame, one easing toward the host's answer and the other
            // walking off to its own. Neither is wrong on its own and the
            // result is an animal in two places.
            //
            // Whoever is being told where the animal is does not get to decide
            // where the animal is. The same rule as every other divergence this
            // project has met.
            if (companionAgent.enabled)
            {
                companionAgent.enabled = false;
            }

            Transform animal = companionAgent.transform;
            Vector3 told = _companionPosition.Value;

            // Walked toward when it is a step away, put there when it is not.
            //
            // Easing alone assumes the animal only ever drifts, and the one
            // thing in this game that moves an animal a long way in one frame
            // is the thing that matters: the owner goes through a door and the
            // host's cat is suddenly in a room 370 m off the edge of the town.
            // The client's cat then set off south at one metre a second, which
            // it can keep up for the rest of the match without arriving. The
            // cat was not hidden and not missing — it was walking to the room,
            // and it had already left the screen.
            if (Vector3.Distance(animal.position, told) > SnapDistance)
            {
                animal.position = told;
            }
            else
            {
                animal.position = Vector3.MoveTowards(
                    animal.position,
                    told,
                    Mathf.Max(0.5f, _companionSpeed.Value * 2f) * deltaTime);
            }
            animal.rotation = Quaternion.Slerp(
                animal.rotation,
                Quaternion.Euler(0f, _companionYaw.Value, 0f),
                Mathf.Clamp01(deltaTime * 10f));

            companionAgent
                .GetComponent<PawsAndLoot.Animation.CompanionLegAnimator>()
                ?.SetExternalSpeed(_companionSpeed.Value);

            // The body settle is told the same speed the legs are.
            //
            // Only the legs were told, so on this machine the two read the
            // animal differently: the legs walked at the host's speed while the
            // body chased a speed measured from packets, which is a spike
            // followed by nothing. The hop's height changed every frame and the
            // cat shook.
            companionAgent
                .GetComponent<
                    PawsAndLoot.Animation.CompanionProceduralAnimator>()
                ?.SetExternalSpeed(_companionSpeed.Value);
        }

        private void PublishCompanionFace()
        {
            if (companionFace == null)
            {
                return;
            }

            int face = companionFace.IsShowing
                ? (int)companionFace.Current
                : 0;
            int current = _companionFace.Value & 0xF;
            if (face == current)
            {
                return;
            }

            _companionFaceSequence++;
            _companionFace.Value = (_companionFaceSequence << 4) | face;
        }

        /// <summary>
        /// Shows what the host says the animal is showing.
        ///
        /// Acts on the counter rather than the face so the same face twice in a
        /// row still re-triggers, and ignores a repeat of a sequence it has
        /// already drawn.
        /// </summary>
        private void ApplyCompanionFace()
        {
            if (companionFace == null)
            {
                return;
            }

            int packed = _companionFace.Value;
            int sequence = packed >> 4;
            if (sequence == _lastCompanionFaceSequence)
            {
                return;
            }

            _lastCompanionFaceSequence = sequence;
            var face = (PawsAndLoot.Companions.CompanionExpression)(packed & 0xF);
            if (face == PawsAndLoot.Companions.CompanionExpression.None)
            {
                companionFace.Hide();
            }
            else
            {
                companionFace.Show(face);
            }
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
                PublishCompanionFace();
                PublishCompanionTransform();
                PublishGameplayState();
                return;
            }

            ApplyCompanionFace();
            ReportCompanion();
            ApplyCompanionTransform(Time.deltaTime);
            ApplyReplicatedTransform(Time.deltaTime);
            ApplyReplicatedGameplayState();
        }

        private void PublishGameplayState()
        {
            if (motor != null)
            {
                _airborne.Value = motor.IsAirborne;

                // The host's own pose, from the motor that just ran. The client
                // branch reads the replicated flag instead; this branch returns
                // before it, so both need saying.
                legAnimator?.SetAirborne(motor.IsAirborne);
            }

            if (stun != null)
            {
                _stunSeconds.Value = stun.RemainingSeconds;
            }

            // Read off whichever of the two this player happens to have. The
            // officer carries the tally and the thief carries the sentence, so
            // one of these is always null and that is fine — asking is cheaper
            // than a second link type.
            var completion =
                GetComponent<PawsAndLoot.Gameplay.Arrest.ArrestCompletionController>();
            if (completion != null)
            {
                _catchCount.Value = completion.CurrentCatchCount;
            }

            var jail =
                GetComponent<PawsAndLoot.Gameplay.Arrest.ThiefJailState>();
            if (jail != null)
            {
                _jailSeconds.Value = jail.RemainingSeconds;
            }

            if (toolCarrier != null)
            {
                _heldTool.Value = toolCarrier.HasTool
                    ? (int)toolCarrier.HeldKind
                    : -1;
                _toolSlots.Value = toolCarrier.EncodedSlots;
                _toolQuantities.Value = toolCarrier.EncodedQuantities;
                _selectedToolSlot.Value = toolCarrier.SelectedSlot;
            }

            if (policeWallet != null)
            {
                _policeAmount.Value = policeWallet.Amount;
            }

            if (interiorState != null)
            {
                _interiorId.Value = interiorState.CurrentInteriorId;
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

            // Written into the same components the screen already reads, rather
            // than handed to the HUD separately. One reader, two writers that
            // never run on the same machine — the host simulates and the client
            // is told, which is how everything else here works.
            GetComponent<PawsAndLoot.Gameplay.Arrest.ArrestCompletionController>()
                ?.ApplyReplicatedCatchCount(_catchCount.Value);
            GetComponent<PawsAndLoot.Gameplay.Arrest.ThiefJailState>()
                ?.ApplyReplicatedRemaining(_jailSeconds.Value);

            if (interiorState != null)
            {
                interiorState.ApplyReplicated(_interiorId.Value);
            }

            // What is in hand, so the HUD on this screen matches the hand the
            // host is actually simulating.
            if (toolCarrier != null)
            {
                toolCarrier.ApplyReplicatedSlots(
                    _toolSlots.Value,
                    _toolQuantities.Value,
                    _selectedToolSlot.Value);
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
            // Told, not worked out. A replicated character is moved by writing its
            // position, so its controller never reports a contact and its vertical
            // motion arrives in lumps.
            legAnimator?.SetAirborne(_airborne.Value);

            if (legAnimator != null && motor != null)
            {
                float told =
                    _normalizedSpeed.Value * motor.EffectiveMoveSpeed;
                legAnimator.SetExternalSpeed(told);

                // The same for the body. The animal is what somebody noticed,
                // because the thief's cat stays close enough to fill the
                // screen, but the replicated player is moved exactly the same
                // way and had exactly the same two opinions about its speed.
                GetComponent<
                    PawsAndLoot.Animation.CompanionProceduralAnimator>()
                    ?.SetExternalSpeed(told);
            }
        }
    }
}
