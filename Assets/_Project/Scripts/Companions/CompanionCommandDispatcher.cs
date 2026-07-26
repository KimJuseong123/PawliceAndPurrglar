using System;
using System.Collections.Generic;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// COMP-004. The single gate between input and the animal AI.
    ///
    ///     input -> CompanionCommandRequest -> validator -> dispatcher -> agent
    ///
    /// Nothing else may call <see cref="CompanionAgent.TryAcceptCommand"/>, so
    /// a UI button or a future voice adapter can never mutate AI state
    /// directly. Rejections are reported for feedback and never consume the
    /// cooldown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionCommandDispatcher : MonoBehaviour
    {
        [SerializeField]
        private MonoBehaviour matchStateSource;

        [SerializeField]
        private List<CompanionAgent> agents = new();

        private IMatchStateReader _matchState;

        public event Action<CompanionCommandRequest> CommandAccepted;
        public event Action<CompanionCommandRequest, CompanionCommandRejection>
            CommandRejected;

        public IReadOnlyList<CompanionAgent> Agents => agents;
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public CompanionCommandRejection LastRejection { get; private set; }

        public void Configure(
            IMatchStateReader matchStateReader,
            IEnumerable<CompanionAgent> companionAgents)
        {
            _matchState = matchStateReader;
            matchStateSource = matchStateReader as MonoBehaviour;
            agents = new List<CompanionAgent>(companionAgents);
            AcceptedCount = 0;
            RejectedCount = 0;
            LastRejection = CompanionCommandRejection.None;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (ResolveMatchState() == null)
            {
                throw new InvalidOperationException(
                    $"CompanionCommandDispatcher '{name}' requires a match "
                    + "state source.");
            }
        }

        public CompanionAgent FindAgent(CompanionKind kind)
        {
            foreach (CompanionAgent agent in agents)
            {
                if (agent != null && agent.CompanionKind == kind)
                {
                    return agent;
                }
            }

            return null;
        }

        /// <summary>
        /// Validates then dispatches. Returns false with a reason so the caller
        /// can show feedback without inspecting AI internals.
        /// </summary>
        public bool TryDispatch(
            in CompanionCommandRequest request,
            out CompanionCommandRejection rejection)
        {
            CompanionAgent agent = FindAgent(request.CompanionKind);
            CompanionCommandValidator.Context context = agent != null
                ? agent.BuildValidationContext()
                : new CompanionCommandValidator.Context(
                    ResolveMatchState()?.IsGameplayActive == true,
                    false,
                    false,
                    0f);

            if (!CompanionCommandValidator.TryValidate(
                    request,
                    context,
                    out rejection))
            {
                Reject(request, rejection);
                return false;
            }

            if (!agent.TryAcceptCommand(request))
            {
                rejection = CompanionCommandRejection.CompanionDisabled;
                Reject(request, rejection);
                return false;
            }

            AcceptedCount++;
            LastRejection = CompanionCommandRejection.None;
            CommandAccepted?.Invoke(request);
            return true;
        }

        /// <summary>
        /// Convenience entry point for the prototype number keys. Builds the
        /// request so every input adapter shares one construction path.
        /// </summary>
        public bool TryDispatchNumberKey(
            PlayerRole issuerRole,
            int numberKey,
            CompanionCommandInputSource inputSource,
            float issuedAtSeconds,
            Transform targetEntity,
            Vector3? targetPosition,
            out CompanionCommandRejection rejection)
        {
            CompanionCommandId commandId =
                CompanionCommandCatalog.FromNumberKey(issuerRole, numberKey);
            var request = new CompanionCommandRequest(
                commandId,
                issuerRole,
                CompanionCommandCatalog.GetCompanionKind(issuerRole),
                inputSource,
                issuedAtSeconds,
                targetEntity,
                targetPosition);
            return TryDispatch(request, out rejection);
        }

        public void HandleMatchEnded()
        {
            foreach (CompanionAgent agent in agents)
            {
                if (agent != null)
                {
                    agent.HandleMatchEnded();
                }
            }
        }

        private void Reject(
            in CompanionCommandRequest request,
            CompanionCommandRejection rejection)
        {
            RejectedCount++;
            LastRejection = rejection;
            GameLogger.DebugOnce(
                GameLogCategory.Companion,
                $"companion-reject-{request.CommandId}-{rejection}",
                $"Rejected "
                + $"{CompanionCommandCatalog.GetDisplayName(request.CommandId)}"
                + $" from {request.InputSource}: {rejection}.",
                this);
            CommandRejected?.Invoke(request, rejection);
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
        }
    }
}
