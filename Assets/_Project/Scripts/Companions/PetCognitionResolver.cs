using System;
using PawliceAndPurrglar.Config;
using UnityEngine;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// Converts classifier candidates into a pet-specific decision. This is a
    /// pure domain service: it never moves an animal and never trusts a target
    /// until the action validator resolves it in the authoritative world.
    /// </summary>
    public sealed class PetCognitionResolver
    {
        private readonly PetCognitionConfig config;

        public PetCognitionResolver(PetCognitionConfig configuredConfig = null)
        {
            config = configuredConfig;
        }

        public PetDecision Resolve(PetCognitionContext context, int randomSeed)
        {
            VoiceIntentCandidate[] candidates = context.classification?.candidates;
            if (candidates == null || candidates.Length == 0)
            {
                return CreateFailure(context, randomSeed, "NO_CANDIDATE");
            }

            VoiceIntentCandidate first = candidates[0];
            if (string.IsNullOrWhiteSpace(first.intent))
            {
                return CreateFailure(context, randomSeed, "EMPTY_INTENT");
            }

            float obedience = ResolveObedience(context.petKind);
            float attention = Mathf.Clamp01(context.attention);
            float distraction = Mathf.Clamp01(context.distraction);
            float familiarity = Mathf.Clamp01(context.commandFamiliarity);
            float score = Mathf.Clamp01(
                first.confidence
                * familiarity
                * attention
                * obedience
                * (1f - distraction));

            System.Random random = new(randomSeed);
            if (IsAbsolute(first.intent) && context.classification.exactMatched)
            {
                return CreateDecision(
                    PetCommandResultType.Correct,
                    first,
                    context,
                    randomSeed,
                    PetReactionType.Listen,
                    "ABSOLUTE_COMMAND");
            }

            float correctThreshold = ResolveCorrectThreshold(context.petKind);
            if (score >= correctThreshold)
            {
                return CreateDecision(
                    PetCommandResultType.Correct,
                    first,
                    context,
                    randomSeed,
                    context.petKind == CompanionKind.Dog
                        ? PetReactionType.Overexcited
                        : PetReactionType.Listen,
                    "HIGH_CONFIDENCE");
            }

            if (context.petKind == CompanionKind.Cat && random.NextDouble() > score)
            {
                PetReactionType reaction = distraction > 0.25f
                    ? PetReactionType.DistractedByNoise
                    : PetReactionType.Groom;
                return CreateDecision(
                    distraction > 0.25f
                        ? PetCommandResultType.Distracted
                        : PetCommandResultType.Ignored,
                    null,
                    context,
                    randomSeed,
                    reaction,
                    distraction > 0.25f ? "DISTRACTED" : "CAT_IGNORED");
            }

            if (candidates.Length > 1
                && score >= ResolveConfusedThreshold())
            {
                VoiceIntentCandidate alternative = candidates[1];
                return CreateDecision(
                    PetCommandResultType.Confused,
                    alternative,
                    context,
                    randomSeed,
                    PetReactionType.TiltHead,
                    "SIMILAR_INTENT_SELECTED");
            }

            return CreateDecision(
                PetCommandResultType.Misunderstood,
                first,
                context,
                randomSeed,
                context.petKind == CompanionKind.Dog
                    ? PetReactionType.Overexcited
                    : PetReactionType.LookAtWrongTarget,
                "LOW_CONFIDENCE");
        }

        private PetDecision CreateFailure(
            PetCognitionContext context,
            int randomSeed,
            string reason)
        {
            return new PetDecision
            {
                resultType = PetCommandResultType.Failed,
                selectedCommandId = CompanionCommandId.None,
                reaction = PetReactionType.TiltHead,
                reasonCode = reason,
                commandSequence = context.commandSequence,
                randomSeed = randomSeed,
                reactionDurationMs = 350
            };
        }

        private PetDecision CreateDecision(
            PetCommandResultType resultType,
            VoiceIntentCandidate candidate,
            PetCognitionContext context,
            int randomSeed,
            PetReactionType reaction,
            string reason)
        {
            return new PetDecision
            {
                resultType = resultType,
                selectedIntent = candidate?.intent,
                selectedTargetId = candidate?.targetId,
                selectedCommandId = CompanionCommandCatalog.FromIntent(
                    candidate?.intent,
                    context.petKind),
                reaction = reaction,
                reasonCode = reason,
                commandSequence = context.commandSequence,
                randomSeed = randomSeed,
                reactionDurationMs = resultType == PetCommandResultType.Correct
                    ? 150
                    : 500
            };
        }

        private float ResolveObedience(CompanionKind kind)
        {
            if (config == null)
            {
                return kind == CompanionKind.Dog ? 0.9f : 0.52f;
            }

            return kind == CompanionKind.Dog
                ? config.DogObedience
                : config.CatObedience;
        }

        private float ResolveCorrectThreshold(CompanionKind kind)
        {
            if (config == null)
            {
                return kind == CompanionKind.Dog ? 0.62f : 0.78f;
            }

            return kind == CompanionKind.Dog
                ? config.DogCorrectThreshold
                : config.CatCorrectThreshold;
        }

        private float ResolveConfusedThreshold()
        {
            return config == null ? 0.34f : config.ConfusedThreshold;
        }

        private static bool IsAbsolute(string intent)
        {
            return intent == "STOP"
                || intent == "FOLLOW_OWNER"
                || intent == "STAY"
                || intent == "RETURN_OWNER"
                || intent == "CANCEL";
        }
    }
}
