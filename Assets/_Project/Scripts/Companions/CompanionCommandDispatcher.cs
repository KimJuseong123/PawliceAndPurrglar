using System;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.AI;

namespace PawsAndLoot.Companions
{
    public interface ICompanionCommandDispatcher
    {
        CompanionCommandResult TryDispatch(
            CompanionCommandId commandId,
            CompanionCommandSource source,
            string requestId = null);
    }

    public sealed class CompanionCommandDispatcher :
        MonoBehaviour,
        ICompanionCommandDispatcher
    {
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private PlayerRoleIdentity police;

        [SerializeField]
        private PlayerRoleIdentity thief;

        [SerializeField]
        private CompanionAgent dogAgent;

        [SerializeField]
        private CompanionAgent catAgent;

        [SerializeField]
        private CompanionConfig config;

        public event Action<CompanionCommandResult> CommandResolved;

        public void Configure(
            MatchRuntimeState configuredMatchRuntime,
            LocalPlayerRoleSelector configuredRoleSelector,
            PlayerRoleIdentity configuredPolice,
            PlayerRoleIdentity configuredThief,
            CompanionAgent configuredDog,
            CompanionAgent configuredCat,
            CompanionConfig configuredConfig)
        {
            matchRuntime = configuredMatchRuntime;
            roleSelector = configuredRoleSelector;
            police = configuredPolice;
            thief = configuredThief;
            dogAgent = configuredDog;
            catAgent = configuredCat;
            config = configuredConfig;
        }

        public CompanionCommandResult TryDispatch(
            CompanionCommandId commandId,
            CompanionCommandSource source,
            string requestId = null)
        {
            PlayerRole role = roleSelector == null
                ? PlayerRole.Police
                : roleSelector.ActiveRole;
            CompanionAgent companion = role == PlayerRole.Police
                ? dogAgent
                : catAgent;
            PlayerRoleIdentity target = role == PlayerRole.Police
                ? thief
                : police;
            Vector3 targetPosition = target == null
                ? Vector3.zero
                : ResolveTargetPosition(
                    commandId,
                    target.transform.position,
                    companion);
            bool targetReachable = target != null
                && TryResolveReachableTarget(
                    companion,
                    targetPosition,
                    out targetPosition);

            var request = new CompanionCommandRequest(
                string.IsNullOrWhiteSpace(requestId)
                    ? Guid.NewGuid().ToString("N")
                    : requestId,
                commandId,
                role,
                companion == null
                    ? string.Empty
                    : companion.name,
                target == null
                    ? string.Empty
                    : target.GetInstanceID().ToString(),
                targetPosition,
                Time.unscaledTimeAsDouble,
                source);

            CompanionCommandFailure failure =
                CompanionCommandValidator.Validate(
                    commandId,
                    role,
                    matchRuntime != null
                        && matchRuntime.IsGameplayActive,
                    companion != null && companion.IsAvailable,
                    companion != null
                        && companion.IsCommandAvailable,
                    target != null,
                    targetReachable);

            CompanionCommandResult result;
            if (failure != CompanionCommandFailure.None)
            {
                result = CompanionCommandResult.Reject(
                    request,
                    failure,
                    CompanionCommandValidator.GetFailureMessage(
                        failure));
            }
            else if (!companion.TryBeginCommand(request))
            {
                result = CompanionCommandResult.Reject(
                    request,
                    CompanionCommandFailure.ExecutionFailed,
                    CompanionCommandValidator.GetFailureMessage(
                        CompanionCommandFailure.ExecutionFailed));
            }
            else
            {
                result = CompanionCommandResult.Accept(request);
                GameLogger.Info(
                    GameLogCategory.Companion,
                    $"{role} accepted {commandId} from {source}.",
                    this);
            }

            CommandResolved?.Invoke(result);
            return result;
        }

        private Vector3 ResolveTargetPosition(
            CompanionCommandId commandId,
            Vector3 targetPosition,
            CompanionAgent companion)
        {
            if (commandId != CompanionCommandId.Distract
                || companion == null
                || config == null)
            {
                return targetPosition;
            }

            Vector3 towardCat =
                companion.transform.position - targetPosition;
            towardCat.y = 0f;
            if (towardCat.sqrMagnitude < 0.01f)
            {
                towardCat = Vector3.forward;
            }

            return targetPosition
                + towardCat.normalized
                * config.DistractionDistance;
        }

        private bool TryResolveReachableTarget(
            CompanionAgent companion,
            Vector3 requested,
            out Vector3 sampled)
        {
            float radius = config == null
                ? 2f
                : config.TargetSampleRadius;
            if (NavMesh.SamplePosition(
                    requested,
                    out NavMeshHit hit,
                    radius,
                    NavMesh.AllAreas))
            {
                sampled = hit.position;
                NavMeshAgent navigationAgent = companion == null
                    ? null
                    : companion.GetComponent<NavMeshAgent>();
                if (navigationAgent == null
                    || !navigationAgent.enabled
                    || !navigationAgent.isOnNavMesh)
                {
                    return false;
                }

                var path = new NavMeshPath();
                return navigationAgent.CalculatePath(
                           sampled,
                           path)
                    && path.status
                    == NavMeshPathStatus.PathComplete;
            }

            sampled = requested;
            return false;
        }
    }
}
