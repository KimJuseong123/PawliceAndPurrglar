using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
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

        public event Action<CompanionStateChanged> StateChanged;
        public event Action<CompanionCommandId> CommandCompleted;
        public event Action<CompanionCommandId, CompanionCommandRejection>
            CommandRecovered;

        public CompanionKind CompanionKind => companionKind;
        public CompanionState CurrentState => _stateMachine.CurrentState;
        public bool IsActive => _stateMachine.IsActive;
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
            IMatchStateReader matchStateReader)
        {
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
            _hasCommandDestination =
                request.TryGetDestination(out _commandDestination);

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

        public void HandleMatchEnded()
        {
            _hasCommandDestination = false;
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
                transform.position = rescue;
                _lastPosition = rescue;
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

        private void TickExecuteCommand(float deltaTime)
        {
            _commandElapsedSeconds += deltaTime;
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
                CommandCompleted?.Invoke(finished);
            }
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
                CommandRecovered?.Invoke(failed, reason);
            }
        }

        private float StepTowards(Vector3 destination, float deltaTime)
        {
            Vector3 current = transform.position;
            Vector3 flatDestination = new(
                destination.x,
                current.y,
                destination.z);
            Vector3 next = Vector3.MoveTowards(
                current,
                flatDestination,
                companionConfig.MoveSpeed * deltaTime);
            transform.position = next;

            Vector3 facing = flatDestination - current;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
            {
                transform.forward = facing.normalized;
            }

            return Vector3.Distance(current, next);
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
