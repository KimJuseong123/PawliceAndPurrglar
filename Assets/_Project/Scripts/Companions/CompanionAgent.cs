using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// COMP-002 follow, COMP-006 recovery, and the runtime half of COMP-001.
    ///
    /// Owns one animal. Moves by direct transform steering rather than a
    /// NavMesh agent for now, so the greybox map needs no bake; the seam is
    /// <see cref="TryStep"/>, which is the only place displacement happens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionAgent : MonoBehaviour
    {
        [SerializeField]
        private CompanionKind companionKind;

        [SerializeField]
        private Transform owner;

        [SerializeField]
        private CompanionConfig companionConfig;

        [SerializeField]
        private MonoBehaviour matchStateSource;

        [Header("Follow tuning")]
        [SerializeField, Min(0.1f)]
        private float stopDistance = 2.2f;

        [SerializeField, Min(0.2f)]
        private float resumeDistance = 3.2f;

        [SerializeField, Min(1f)]
        private float leashDistance = 18f;

        [SerializeField, Min(0.05f)]
        private float arriveDistance = 0.6f;

        [Header("Recovery")]
        [SerializeField, Min(0.5f)]
        private float commandTimeoutSeconds = 6f;

        [SerializeField, Min(0.1f)]
        private float stuckTimeoutSeconds = 1.5f;

        [SerializeField, Min(0.001f)]
        private float stuckDistanceThreshold = 0.05f;

        [SerializeField]
        private CharacterController characterController;

        private readonly CompanionStateMachine _stateMachine = new();
        private IMatchStateReader _matchState;
        private CompanionCommandRequest _activeRequest;
        private Vector3 _commandDestination;
        private bool _hasCommandDestination;
        private float _cooldownRemainingSeconds;
        private float _commandElapsedSeconds;
        private float _stuckElapsedSeconds;
        private Vector3 _lastPosition;
        private bool _recoveryLogged;

        [SerializeField]
        private CompanionCommandResolver commandResolver;

        [SerializeField]
        private CompanionLootCourier lootCourier;

        public event Action<CompanionStateChanged> StateChanged;
        public event Action<CompanionCommandId> CommandCompleted;
        public event Action<CompanionCommandId, CompanionCommandRejection>
            CommandRecovered;

        /// <summary>
        /// UI-006. Raised whenever a command produces a reportable result,
        /// including refusals that happen at resolve time.
        /// </summary>
        public event Action<CompanionCommandOutcome> OutcomeReported;

        public CompanionCommandOutcome LastOutcome { get; private set; }

        public CompanionKind CompanionKind => companionKind;
        public CompanionState CurrentState => _stateMachine.CurrentState;
        public CompanionStatusId CurrentStatus => CurrentState switch
        {
            CompanionState.MoveToTarget => CompanionStatusId.Tracking,
            CompanionState.ExecuteCommand => CompanionStatusId.CommandReceived,
            CompanionState.ReturnToOwner => CompanionStatusId.Chasing,
            CompanionState.Cooldown => CompanionStatusId.CommandReceived,
            _ => CompanionStatusId.Idle
        };

        public void ReceiveAttraction(
            ThrowableKind kind,
            Vector3 position,
            float strength,
            float duration)
        {
            GameLogger.DebugOnce(
                GameLogCategory.Companion,
                "companion-attraction",
                $"{CompanionKind} noticed {kind} nearby.",
                this);
        }

        public void ReceiveConfusion(float duration)
        {
            GameLogger.DebugOnce(
                GameLogCategory.Companion,
                "companion-confusion",
                $"{CompanionKind} was confused for {duration:0.0}s.",
                this);
        }

        public void ReceiveNoise(Vector3 position, float strength, float duration)
        {
            GameLogger.DebugOnce(
                GameLogCategory.Companion,
                "companion-noise",
                $"{CompanionKind} heard a nearby noise.",
                this);
        }
        public bool IsActive => _stateMachine.IsActive;
        public bool IsBusyWithCommand => _stateMachine.IsBusyWithCommand;
        public float CooldownRemainingSeconds => _cooldownRemainingSeconds;
        public CompanionCommandId ActiveCommand =>
            _stateMachine.IsBusyWithCommand
                ? _activeRequest.CommandId
                : CompanionCommandId.None;
        public Transform Owner => owner;

        public void Configure(
            CompanionKind kind,
            Transform configuredOwner,
            CompanionConfig config,
            IMatchStateReader matchStateReader,
            CharacterController configuredController = null,
            CompanionCommandResolver configuredResolver = null,
            CompanionLootCourier configuredCourier = null)
        {
            characterController = configuredController != null
                ? configuredController
                : GetComponent<CharacterController>();
            commandResolver = configuredResolver;
            lootCourier = configuredCourier;
            LastOutcome = CompanionCommandOutcome.None;
            companionKind = kind;
            owner = configuredOwner;
            companionConfig = config;
            _matchState = matchStateReader;
            matchStateSource = matchStateReader as MonoBehaviour;
            _cooldownRemainingSeconds = 0f;
            _commandElapsedSeconds = 0f;
            _stuckElapsedSeconds = 0f;
            _hasCommandDestination = false;
            _activeRequest = default;
            _stateMachine.ResetTo(CompanionState.Idle);
            ValidateOrThrow();
            _lastPosition = transform.position;
        }

        public void ValidateOrThrow()
        {
            if (owner == null)
            {
                throw new InvalidOperationException(
                    $"CompanionAgent '{name}' requires an owner transform.");
            }

            if (companionConfig == null)
            {
                throw new InvalidOperationException(
                    $"CompanionAgent '{name}' requires a CompanionConfig.");
            }

            if (ResolveMatchState() == null)
            {
                throw new InvalidOperationException(
                    $"CompanionAgent '{name}' requires a match state source.");
            }

            if (resumeDistance <= stopDistance)
            {
                throw new InvalidOperationException(
                    $"CompanionAgent '{name}' needs resumeDistance greater "
                    + "than stopDistance to avoid follow chatter.");
            }

            companionConfig.ValidateOrThrow();
        }

        /// <summary>
        /// Builds the validation context from live state. The dispatcher uses
        /// this so the agent stays the single source of truth for cooldown and
        /// availability.
        /// </summary>
        public CompanionCommandValidator.Context BuildValidationContext()
        {
            return new CompanionCommandValidator.Context(
                ResolveMatchState()?.IsGameplayActive == true,
                isActiveAndEnabled,
                _stateMachine.IsActive,
                _cooldownRemainingSeconds);
        }

        /// <summary>
        /// Accepts an already validated command. A second command replaces the
        /// first rather than queueing, so a player can always correct an order.
        /// </summary>
        public bool TryAcceptCommand(in CompanionCommandRequest request)
        {
            if (!_stateMachine.IsActive
                || request.CompanionKind != companionKind)
            {
                return false;
            }

            _activeRequest = request;
            _commandElapsedSeconds = 0f;
            _stuckElapsedSeconds = 0f;
            _recoveryLogged = false;

            if (TryHandleSafetyCommand(request))
            {
                return true;
            }

            // The resolver decides what the command actually means. A refusal
            // here is a gameplay result, so it still consumes the cooldown and
            // gets reported instead of silently doing nothing.
            if (commandResolver != null)
            {
                CompanionCommandResolver.Resolution resolution =
                    commandResolver.Resolve(
                        request,
                        transform.position,
                        request.IssuedAtSeconds);
                ReportOutcome(resolution.Outcome);
                if (!resolution.Accepted)
                {
                    _cooldownRemainingSeconds =
                        companionConfig.CommandCooldownSeconds;
                    _stateMachine.TryTransitionTo(
                        CompanionState.ReturnToOwner);
                    return true;
                }

                _hasCommandDestination = resolution.Destination.HasValue;
                if (_hasCommandDestination)
                {
                    _commandDestination = resolution.Destination.Value;
                }
            }
            else
            {
                _hasCommandDestination =
                    request.TryGetDestination(out _commandDestination);
            }

            CompanionState next = _hasCommandDestination
                ? CompanionState.MoveToTarget
                : CompanionState.ExecuteCommand;
            if (!_stateMachine.TryTransitionTo(next))
            {
                // Already in the target state, so restart the same order.
                return _stateMachine.CurrentState == next;
            }

            GameLogger.Debug(
                GameLogCategory.Companion,
                $"{companionKind} accepted "
                + $"{CompanionCommandCatalog.GetDisplayName(request.CommandId)}"
                + $" from {request.InputSource}.",
                this);
            return true;
        }

        private bool TryHandleSafetyCommand(
            in CompanionCommandRequest request)
        {
            CompanionState nextState;
            switch (request.CommandId)
            {
                case CompanionCommandId.Stop:
                case CompanionCommandId.Stay:
                case CompanionCommandId.Cancel:
                    _hasCommandDestination = false;
                    nextState = CompanionState.Idle;
                    break;
                case CompanionCommandId.FollowOwner:
                case CompanionCommandId.ReturnOwner:
                    _hasCommandDestination = false;
                    nextState = CompanionState.ReturnToOwner;
                    break;
                default:
                    return false;
            }

            _cooldownRemainingSeconds = companionConfig.CommandCooldownSeconds;
            if (_stateMachine.CurrentState != nextState)
            {
                _stateMachine.TryTransitionTo(nextState);
            }

            CommandCompleted?.Invoke(request.CommandId);
            return true;
        }

        public void HandleMatchEnded()
        {
            _hasCommandDestination = false;
            if (lootCourier != null)
            {
                lootCourier.AbandonEscort();
            }

            _stateMachine.TryTransitionTo(CompanionState.Disabled);
        }

        public void Tick(float deltaTime)
        {
            float step = Mathf.Max(0f, deltaTime);
            _cooldownRemainingSeconds = Mathf.Max(
                0f,
                _cooldownRemainingSeconds - step);

            if (ResolveMatchState()?.IsGameplayActive != true)
            {
                if (_stateMachine.CurrentState != CompanionState.Disabled
                    && _stateMachine.CurrentState != CompanionState.Idle)
                {
                    _stateMachine.TryTransitionTo(CompanionState.Idle);
                }

                return;
            }

            if (!_stateMachine.IsActive)
            {
                return;
            }

            if (owner == null)
            {
                RecoverToOwner(CompanionCommandRejection.CompanionMissing);
                return;
            }

            ResolveLure();

            // A lure overrides whatever the animal was doing, including a
            // command it is halfway through. That is the whole value of the
            // prop: an order the owner gave can be spoiled, which is the only
            // counterplay either player has against the other's animal.
            //
            // Checked before the state switch rather than added as a state, so
            // the animal returns to exactly what it was doing when the smell
            // wears off instead of being dropped back to Idle.
            if (_lure != null && _lure.IsActive)
            {
                StepTowards(_lure.Point, step);
                _lastPosition = transform.position;
                return;
            }

            switch (_stateMachine.CurrentState)
            {
                case CompanionState.Idle:
                case CompanionState.Follow:
                case CompanionState.Cooldown:
                case CompanionState.ReturnToOwner:
                    TickFollow(step);
                    break;
                case CompanionState.MoveToTarget:
                    TickMoveToTarget(step);
                    break;
                case CompanionState.ExecuteCommand:
                    TickExecuteCommand(step);
                    break;
            }

            _lastPosition = transform.position;
        }

        /// <summary>
        /// COMP-002. Sits still inside the stop band, walks when the owner gets
        /// far, and teleports back if the leash breaks so a lost animal can
        /// never stall the match.
        /// </summary>
        private void TickFollow(float deltaTime)
        {
            float distance = PlanarDistance(
                transform.position,
                owner.position);

            if (distance > leashDistance)
            {
                Vector3 rescue = owner.position
                    - owner.forward * stopDistance;
                rescue.y = transform.position.y;
                WarpTo(rescue);
                _lastPosition = transform.position;
                GameLogger.DebugOnce(
                    GameLogCategory.Companion,
                    $"companion-leash-{companionKind}",
                    $"{companionKind} exceeded its leash and was returned "
                    + "to its owner.",
                    this);
                _stateMachine.TryTransitionTo(CompanionState.Follow);
                return;
            }

            if (distance > resumeDistance)
            {
                _stateMachine.TryTransitionTo(CompanionState.Follow);
                StepTowards(owner.position, deltaTime);
                return;
            }

            if (distance < stopDistance)
            {
                _stateMachine.TryTransitionTo(CompanionState.Idle);
            }
        }

        private CompanionLure _lure;
        private bool _lookedForLure;

        /// <summary>
        /// Resolved lazily and cached, because an animal built without one
        /// simply can never be lured rather than being searched for every
        /// frame.
        /// </summary>
        private CompanionLure ResolveLure()
        {
            if (!_lookedForLure)
            {
                _lure = GetComponent<CompanionLure>();
                _lookedForLure = true;
            }

            return _lure;
        }

        private void TickMoveToTarget(float deltaTime)
        {
            _commandElapsedSeconds += deltaTime;

            // A live entity keeps updating the destination; a lost one drops
            // the command instead of walking to a remembered ghost.
            if (_activeRequest.TargetEntity != null)
            {
                _commandDestination = _activeRequest.TargetEntity.position;
            }
            else if (!_hasCommandDestination)
            {
                RecoverToOwner(CompanionCommandRejection.TargetMissing);
                return;
            }

            if (_commandElapsedSeconds >= commandTimeoutSeconds)
            {
                RecoverToOwner(CompanionCommandRejection.TargetUnreachable);
                return;
            }

            if (PlanarDistance(transform.position, _commandDestination)
                <= arriveDistance)
            {
                // CAT-005. Arriving at loot means picking it up, then walking
                // it home; the command is not finished until it is handed over.
                if (TryBeginLootEscort())
                {
                    return;
                }

                _stateMachine.TryTransitionTo(
                    CompanionState.ExecuteCommand);
                _commandElapsedSeconds = 0f;
                return;
            }

            float travelled = StepTowards(_commandDestination, deltaTime);
            _stuckElapsedSeconds = travelled < stuckDistanceThreshold
                ? _stuckElapsedSeconds + deltaTime
                : 0f;
            if (_stuckElapsedSeconds >= stuckTimeoutSeconds)
            {
                RecoverToOwner(CompanionCommandRejection.TargetUnreachable);
            }
        }

        /// <summary>
        /// CAT-005. Picks up the loot the STEAL order pointed at and retargets
        /// the companion at its owner so the trip home is the rest of the
        /// command. Returns false for every other command.
        /// </summary>
        private bool TryBeginLootEscort()
        {
            if (lootCourier == null
                || _activeRequest.CommandId != CompanionCommandId.Steal
                || lootCourier.HasLoot)
            {
                return false;
            }

            LootItem nearest = FindLootWithinPickupRange();
            if (nearest == null || !lootCourier.TryPickUp(nearest))
            {
                return false;
            }

            ReportOutcome(CompanionCommandOutcome.StealCarrying);
            _commandDestination = owner.position;
            _hasCommandDestination = true;
            _commandElapsedSeconds = 0f;
            _stuckElapsedSeconds = 0f;
            return true;
        }

        private LootItem FindLootWithinPickupRange()
        {
            LootItem best = null;
            float bestDistance = float.PositiveInfinity;
            foreach (LootItem loot in
                UnityEngine.Object.FindObjectsByType<LootItem>(
                    FindObjectsSortMode.None))
            {
                if (!loot.IsAvailable)
                {
                    continue;
                }

                float distance = PlanarDistance(
                    transform.position,
                    loot.transform.position);
                if (distance < bestDistance && distance <= arriveDistance * 2f)
                {
                    bestDistance = distance;
                    best = loot;
                }
            }

            return best;
        }

        private void TickExecuteCommand(float deltaTime)
        {
            _commandElapsedSeconds += deltaTime;

            // A courier finishes by handing the item over, not by waiting.
            if (lootCourier != null && lootCourier.HasLoot)
            {
                if (!lootCourier.TryDeliver())
                {
                    _commandDestination = owner.position;
                    _hasCommandDestination = true;
                    _stateMachine.TryTransitionTo(
                        CompanionState.MoveToTarget);
                    return;
                }

                ReportOutcome(CompanionCommandOutcome.StealDelivered);
            }

            if (_commandElapsedSeconds < companionConfig.CommandCooldownSeconds)
            {
                return;
            }

            CompanionCommandId finished = _activeRequest.CommandId;
            _hasCommandDestination = false;
            _commandElapsedSeconds = 0f;
            _cooldownRemainingSeconds =
                companionConfig.CommandCooldownSeconds;
            if (_stateMachine.TryTransitionTo(CompanionState.Cooldown))
            {
                ReportOutcome(CompanionCommandOutcome.Completed);
                CommandCompleted?.Invoke(finished);
            }
        }

        private void ReportOutcome(CompanionCommandOutcome outcome)
        {
            if (outcome == CompanionCommandOutcome.None)
            {
                return;
            }

            LastOutcome = outcome;
            OutcomeReported?.Invoke(outcome);
        }

        /// <summary>
        /// COMP-006. One recovery path for every failure, with a suppressed log
        /// so a repeatedly unreachable target cannot flood the console.
        /// </summary>
        private void RecoverToOwner(CompanionCommandRejection reason)
        {
            CompanionCommandId failed = _activeRequest.CommandId;
            _hasCommandDestination = false;
            _commandElapsedSeconds = 0f;
            _stuckElapsedSeconds = 0f;
            _activeRequest = default;

            if (!_recoveryLogged)
            {
                _recoveryLogged = true;
                GameLogger.DebugOnce(
                    GameLogCategory.Companion,
                    $"companion-recovery-{companionKind}-{reason}",
                    $"{companionKind} abandoned "
                    + $"{CompanionCommandCatalog.GetDisplayName(failed)} "
                    + $"because of {reason} and returned to its owner.",
                    this);
            }

            if (_stateMachine.TryTransitionTo(CompanionState.ReturnToOwner))
            {
                ReportOutcome(CompanionCommandOutcome.Abandoned);
                CommandRecovered?.Invoke(failed, reason);
            }
        }

        /// <summary>
        /// Moves one frame toward a destination and returns the distance
        /// actually covered.
        ///
        /// A CharacterController does the displacement so walls and buildings
        /// block the companion the same way they block players. The returned
        /// travelled distance is the real one, not the requested one, which is
        /// what lets the stuck detector notice a wall.
        /// </summary>
        private float StepTowards(Vector3 destination, float deltaTime)
        {
            Vector3 current = transform.position;
            Vector3 flatDestination = new(
                destination.x,
                current.y,
                destination.z);
            Vector3 toDestination = flatDestination - current;
            float distance = toDestination.magnitude;
            if (distance <= 0.0001f)
            {
                return 0f;
            }

            float stepLength = Mathf.Min(
                companionConfig.MoveSpeed * deltaTime,
                distance);
            Vector3 direction = toDestination / distance;

            if (characterController != null
                && characterController.enabled)
            {
                // A little gravity keeps the capsule grounded on the greybox
                // slabs instead of hovering after a step down.
                Vector3 motion = direction * stepLength
                    + Vector3.up * (Physics.gravity.y * 0.05f * deltaTime);
                characterController.Move(motion);
            }
            else
            {
                transform.position = current + direction * stepLength;
            }

            Vector3 facing = direction;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                transform.forward = facing.normalized;
            }

            return PlanarDistance(current, transform.position);
        }

        /// <summary>
        /// Teleporting has to go through the controller, otherwise the
        /// character controller keeps its old internal position and the next
        /// Move snaps the companion back.
        /// </summary>
        private void WarpTo(Vector3 position)
        {
            if (characterController != null)
            {
                bool wasEnabled = characterController.enabled;
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = wasEnabled;
                return;
            }

            transform.position = position;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private IMatchStateReader ResolveMatchState()
        {
            if (_matchState == null && matchStateSource != null)
            {
                _matchState = matchStateSource as IMatchStateReader;
            }

            return _matchState;
        }

        private void Awake()
        {
            ValidateOrThrow();
            _stateMachine.StateChanged += HandleStateChanged;
            _lastPosition = transform.position;
        }

        private void OnDestroy()
        {
            _stateMachine.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(CompanionStateChanged change)
        {
            StateChanged?.Invoke(change);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
