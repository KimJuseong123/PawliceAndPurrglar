using System.Collections.Generic;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Voice
{
    [DisallowMultipleComponent]
    public sealed class LocalAiCommandExecutor : MonoBehaviour
    {
        [SerializeField] private CompanionCommandDispatcher dispatcher;
        [SerializeField] private CompanionTargetRegistry targetRegistry;
        [SerializeField] private DogBehaviorProfile dogBehaviorProfile;
        [SerializeField, Min(1f)] private float targetDistance = 18f;

        private DogBehaviorFilter dogFilter;

        public bool TryExecute(
            VoiceCommandInput input,
            VoiceCommandResult result,
            LocalAiVoiceResponse response)
        {
            if (input == null || result == null || response == null)
            {
                return false;
            }

            ResolveReferences();
            CompanionKind kind = input.PetId == "dog"
                ? CompanionKind.Dog
                : CompanionKind.Cat;
            if (!VoiceCommandMapper.TryMap(
                    result.interpretedCommand,
                    kind,
                    out CompanionCommandId understoodCommand))
            {
                return false;
            }

            ResolveTarget(
                result,
                response,
                input,
                out Transform targetEntity,
                out Vector3? targetPosition);
            VoiceValidationResult validation = VoiceCommandValidator.Validate(
                result,
                response,
                input.IssuerRole,
                kind,
                dispatcher,
                input.transform,
                targetEntity,
                targetPosition);
            if (!validation.Accepted)
            {
                result.failureCode = validation.Rejection.ToString();
                result.failureMessage = validation.Error;
                return false;
            }

            DogBehaviorDecision decision = new(
                understoodCommand,
                CompanionCommandCatalog.GetDisplayName(understoodCommand),
                string.Empty,
                string.Empty,
                result.targetId);
            if (kind == CompanionKind.Dog)
            {
                dogFilter ??= new DogBehaviorFilter(
                    dogBehaviorProfile != null
                        ? dogBehaviorProfile
                        : CreateDefaultProfile());
                decision = dogFilter.Decide(
                    understoodCommand,
                    result.targetId,
                    result.confidence,
                    BuildBehaviorContext(input, result.transcript));
                result.animalFeedback = decision.ActualAction;
                result.deliberatelyMisunderstood = decision.WasChanged;
            }

            CompanionCommandId actualCommand = decision.ActualCommand;
            if (actualCommand == CompanionCommandId.None)
            {
                result.failureCode = "DOG_BEHAVIOR_NONE";
                return false;
            }

            var request = new CompanionCommandRequest(
                actualCommand,
                input.IssuerRole,
                kind,
                CompanionCommandInputSource.Voice,
                Time.time,
                targetEntity,
                targetPosition,
                result.requestId,
                result.targetId);
            CompanionCommandRejection rejection = CompanionCommandRejection.None;
            bool accepted = dispatcher != null && dispatcher.TryDispatch(
                request,
                out rejection);
            SetRejection(result, rejection, accepted);
            return accepted;
        }

        private void ResolveReferences()
        {
            dispatcher ??= FindFirstObjectByType<CompanionCommandDispatcher>();
            targetRegistry ??= FindFirstObjectByType<CompanionTargetRegistry>();
        }

        private void ResolveTarget(
            VoiceCommandResult result,
            LocalAiVoiceResponse response,
            VoiceCommandInput input,
            out Transform targetEntity,
            out Vector3? targetPosition)
        {
            targetEntity = null;
            targetPosition = null;
            string targetType = (result.targetType ?? "NONE").ToUpperInvariant();
            if (targetType is "VISIBLE_TARGET" or "NAMED_TARGET" or "LAST_KNOWN_TARGET")
            {
                targetRegistry?.RebuildFromScene();
                if (!targetRegistry || !targetRegistry.TryResolve(result.targetId, out targetEntity))
                {
                    return;
                }

                if (Vector3.Distance(input.transform.position, targetEntity.position) > targetDistance)
                {
                    targetEntity = null;
                }

                return;
            }

            if (targetType is "SELF_POSITION")
            {
                targetPosition = input.transform.position;
                return;
            }

            if (targetType is "LOOK_POSITION")
            {
                targetPosition = response.lookWorldPosition != null
                    ? response.lookWorldPosition.ToVector3()
                    : input.transform.position + input.transform.forward * 8f;
            }
        }

        private DogBehaviorContext BuildBehaviorContext(
            VoiceCommandInput input,
            string transcript)
        {
            var distractions = new List<string>();
            DogBehaviorProfile profile = dogBehaviorProfile;
            float radius = profile != null ? profile.DistractionRadius : 4f;
            foreach (LootItem loot in FindObjectsByType<LootItem>(FindObjectsSortMode.None))
            {
                if (loot != null
                    && loot.name.ToLowerInvariant().Contains("bone")
                    && Vector3.Distance(input.transform.position, loot.transform.position) <= radius)
                {
                    distractions.Add("BONE");
                }
            }

            foreach (CompanionAgent agent in FindObjectsByType<CompanionAgent>(FindObjectsSortMode.None))
            {
                if (agent != null
                    && agent.CompanionKind == CompanionKind.Cat
                    && Vector3.Distance(input.transform.position, agent.transform.position) <= radius)
                {
                    distractions.Add("CAT");
                }
            }

            bool urgent = (transcript ?? string.Empty).Contains("빨리")
                || (transcript ?? string.Empty).Contains("지금")
                || (transcript ?? string.Empty).ToLowerInvariant().Contains("urgent");
            bool longCommand = (transcript ?? string.Empty).Length >= 24;
            return new DogBehaviorContext(
                distractions.ToArray(),
                urgent,
                longCommand,
                seed: transcript == null ? 0 : transcript.GetHashCode());
        }

        private static DogBehaviorProfile CreateDefaultProfile()
        {
            return ScriptableObject.CreateInstance<DogBehaviorProfile>();
        }

        private static bool SetRejection(
            VoiceCommandResult result,
            CompanionCommandRejection rejection,
            bool accepted)
        {
            if (!accepted)
            {
                result.failureCode = rejection.ToString();
                result.failureMessage = "COMMAND_REJECTED";
            }

            return accepted;
        }
    }
}
