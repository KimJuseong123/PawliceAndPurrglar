using System;
using PawsAndLoot.Config;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.AI;

namespace PawsAndLoot.Companions
{
    public enum CompanionKind
    {
        Dog = 0,
        Cat = 1
    }

    public enum CompanionState
    {
        Idle = 0,
        FollowOwner,
        MoveToTarget,
        ExecuteCommand,
        ReturnToOwner,
        Disabled
    }

    public sealed class CompanionAgent : MonoBehaviour
    {
        [SerializeField]
        private CompanionKind kind;

        [SerializeField]
        private Transform owner;

        [SerializeField]
        private NavMeshAgent navigationAgent;

        [SerializeField]
        private CompanionConfig config;

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private GameObject distractionVisual;

        private CompanionCommandRequest _activeRequest;
        private float _commandElapsed;
        private float _executeRemaining;
        private float _cooldownRemaining;
        private bool _hasActiveRequest;

        public event Action<CompanionState> StateChanged;
        public event Action<CompanionCommandRequest> CommandCompleted;
        public event Action<CompanionCommandRequest, CompanionCommandFailure>
            CommandFailed;

        public CompanionKind Kind => kind;
        public Transform Owner => owner;
        public CompanionState State { get; private set; } =
            CompanionState.Idle;
        public bool IsAvailable =>
            isActiveAndEnabled
            && owner != null
            && navigationAgent != null
            && navigationAgent.enabled;
        public bool IsCommandAvailable =>
            IsAvailable
            && _cooldownRemaining <= 0f
            && !_hasActiveRequest
            && (State == CompanionState.Idle
                || State == CompanionState.FollowOwner);
        public float CooldownRemainingSeconds => _cooldownRemaining;

        public void Configure(
            CompanionKind configuredKind,
            Transform configuredOwner,
            NavMeshAgent configuredAgent,
            CompanionConfig configuredConfig,
            MatchRuntimeState configuredMatchRuntime,
            GameObject configuredDistractionVisual)
        {
            kind = configuredKind;
            owner = configuredOwner;
            navigationAgent = configuredAgent;
            config = configuredConfig;
            matchRuntime = configuredMatchRuntime;
            distractionVisual = configuredDistractionVisual;
            if (navigationAgent != null && config != null)
            {
                navigationAgent.speed = config.MoveSpeed;
                navigationAgent.stoppingDistance =
                    config.ArrivalTolerance;
            }

            SetDistractionVisualActive(false);
        }

        public bool TryBeginCommand(CompanionCommandRequest request)
        {
            if (!IsCommandAvailable
                || !Supports(request.CommandId)
                || !CanNavigateTo(request.TargetPosition))
            {
                return false;
            }

            _activeRequest = request;
            _hasActiveRequest = true;
            _commandElapsed = 0f;
            _executeRemaining = 0f;
            SetDistractionVisualActive(false);
            ChangeState(CompanionState.MoveToTarget);
            if (navigationAgent.SetDestination(
                    request.TargetPosition))
            {
                return true;
            }

            _hasActiveRequest = false;
            ChangeState(CompanionState.ReturnToOwner);
            return false;
        }

        public void ResetAgent()
        {
            _hasActiveRequest = false;
            _commandElapsed = 0f;
            _executeRemaining = 0f;
            _cooldownRemaining = 0f;
            SetDistractionVisualActive(false);
            if (navigationAgent != null
                && navigationAgent.enabled
                && navigationAgent.isOnNavMesh)
            {
                navigationAgent.ResetPath();
            }

            ChangeState(CompanionState.Idle);
        }

        private void Awake()
        {
            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<NavMeshAgent>();
            }

            if (config == null && GameConfigService.IsInitialized)
            {
                config = GameConfigService.Current.Companion;
            }

            if (navigationAgent != null && config != null)
            {
                navigationAgent.speed = config.MoveSpeed;
                navigationAgent.stoppingDistance =
                    config.ArrivalTolerance;
            }

            SetDistractionVisualActive(false);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _cooldownRemaining = Mathf.Max(
                0f,
                _cooldownRemaining - deltaTime);

            if (!IsAvailable || config == null)
            {
                ChangeState(CompanionState.Disabled);
                return;
            }

            if (matchRuntime != null
                && !matchRuntime.IsGameplayActive)
            {
                if (_hasActiveRequest)
                {
                    FailActiveCommand(
                        CompanionCommandFailure.MatchInactive);
                }
                else
                {
                    FollowOwner();
                }

                return;
            }

            switch (State)
            {
                case CompanionState.MoveToTarget:
                    TickMoveToTarget(deltaTime);
                    break;
                case CompanionState.ExecuteCommand:
                    TickExecute(deltaTime);
                    break;
                case CompanionState.ReturnToOwner:
                    TickReturnToOwner();
                    break;
                default:
                    FollowOwner();
                    break;
            }
        }

        private void TickMoveToTarget(float deltaTime)
        {
            _commandElapsed += deltaTime;
            if (_commandElapsed >= config.CommandTimeoutSeconds)
            {
                FailActiveCommand(
                    CompanionCommandFailure.TargetUnreachable);
                return;
            }

            if (navigationAgent.pathPending)
            {
                return;
            }

            if (navigationAgent.pathStatus
                == NavMeshPathStatus.PathInvalid)
            {
                FailActiveCommand(
                    CompanionCommandFailure.TargetUnreachable);
                return;
            }

            if (navigationAgent.remainingDistance
                <= config.ArrivalTolerance)
            {
                BeginExecution();
            }
        }

        private void BeginExecution()
        {
            navigationAgent.ResetPath();
            _executeRemaining =
                _activeRequest.CommandId == CompanionCommandId.Distract
                    ? config.DistractionDurationSeconds
                    : config.CommandExecuteSeconds;
            if (_activeRequest.CommandId
                == CompanionCommandId.Distract)
            {
                SetDistractionVisualActive(true);
            }

            ChangeState(CompanionState.ExecuteCommand);
        }

        private void TickExecute(float deltaTime)
        {
            _executeRemaining = Mathf.Max(
                0f,
                _executeRemaining - deltaTime);
            if (_executeRemaining > 0f)
            {
                return;
            }

            SetDistractionVisualActive(false);
            CompanionCommandRequest completed = _activeRequest;
            _hasActiveRequest = false;
            _cooldownRemaining = config.CommandCooldownSeconds;
            CommandCompleted?.Invoke(completed);
            ChangeState(CompanionState.ReturnToOwner);
        }

        private void TickReturnToOwner()
        {
            if (owner == null)
            {
                ChangeState(CompanionState.Disabled);
                return;
            }

            float distance = Vector3.Distance(
                transform.position,
                owner.position);
            if (distance <= config.FollowDistance)
            {
                navigationAgent.ResetPath();
                ChangeState(CompanionState.FollowOwner);
                return;
            }

            SetOwnerDestination();
        }

        private void FollowOwner()
        {
            if (owner == null || !navigationAgent.isOnNavMesh)
            {
                return;
            }

            float distance = Vector3.Distance(
                transform.position,
                owner.position);
            if (distance > config.FollowDistance)
            {
                SetOwnerDestination();
                ChangeState(CompanionState.FollowOwner);
            }
            else if (State != CompanionState.FollowOwner)
            {
                navigationAgent.ResetPath();
                ChangeState(CompanionState.FollowOwner);
            }
        }

        private void SetOwnerDestination()
        {
            if (navigationAgent.isOnNavMesh
                && NavMesh.SamplePosition(
                    owner.position,
                    out NavMeshHit hit,
                    config.TargetSampleRadius,
                    NavMesh.AllAreas))
            {
                navigationAgent.SetDestination(hit.position);
            }
        }

        private bool CanNavigateTo(Vector3 position)
        {
            if (navigationAgent == null
                || !navigationAgent.enabled
                || !navigationAgent.isOnNavMesh
                || config == null
                || !NavMesh.SamplePosition(
                    position,
                    out NavMeshHit hit,
                    config.TargetSampleRadius,
                    NavMesh.AllAreas))
            {
                return false;
            }

            var path = new NavMeshPath();
            return navigationAgent.CalculatePath(hit.position, path)
                && path.status == NavMeshPathStatus.PathComplete;
        }

        private bool Supports(CompanionCommandId commandId)
        {
            return kind switch
            {
                CompanionKind.Dog
                    => commandId == CompanionCommandId.Track,
                CompanionKind.Cat
                    => commandId == CompanionCommandId.Distract,
                _ => false
            };
        }

        private void FailActiveCommand(
            CompanionCommandFailure failure)
        {
            CompanionCommandRequest failed = _activeRequest;
            _hasActiveRequest = false;
            _commandElapsed = 0f;
            SetDistractionVisualActive(false);
            if (navigationAgent.isOnNavMesh)
            {
                navigationAgent.ResetPath();
            }

            GameLogger.Warning(
                GameLogCategory.Companion,
                $"{kind} command failed: {failure}.",
                this);
            CommandFailed?.Invoke(failed, failure);
            ChangeState(CompanionState.ReturnToOwner);
        }

        private void SetDistractionVisualActive(bool isActive)
        {
            if (distractionVisual != null)
            {
                distractionVisual.SetActive(isActive);
            }
        }

        private void ChangeState(CompanionState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
