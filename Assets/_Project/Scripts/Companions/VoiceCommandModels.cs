using System;

namespace PawliceAndPurrglar.Companions
{
    [Serializable]
    public sealed class VoiceIntentCandidate
    {
        public string intent;
        public string targetType;
        public string targetId;
        public float confidence;
    }

    [Serializable]
    public sealed class VoiceIntentClassificationResult
    {
        public string normalizedText;
        public VoiceIntentCandidate[] candidates = Array.Empty<VoiceIntentCandidate>();
        public string ambiguity = "HIGH";
        public string[] keywords = Array.Empty<string>();
        public bool exactMatched;
        public bool fallbackUsed;
    }

    [Serializable]
    public sealed class PetCognitionContext
    {
        public CompanionKind petKind;
        public CompanionState currentState;
        public float attention = 0.85f;
        public float distraction = 0.05f;
        public float commandFamiliarity = 1f;
        public VoiceIntentClassificationResult classification;
        public int commandSequence;
    }

    public enum PetCommandResultType
    {
        Correct = 0,
        Confused = 1,
        Misunderstood = 2,
        Ignored = 3,
        Distracted = 4,
        Failed = 5
    }

    public enum PetReactionType
    {
        None = 0,
        Listen = 1,
        TiltHead = 2,
        LookAtWrongTarget = 3,
        Groom = 4,
        DistractedByNoise = 5,
        Overexcited = 6
    }

    [Serializable]
    public sealed class PetDecision
    {
        public PetCommandResultType resultType;
        public string selectedIntent;
        public string selectedTargetId;
        public CompanionCommandId selectedCommandId;
        public PetReactionType reaction;
        public string reasonCode;
        public int commandSequence;
        public int randomSeed;
        public int reactionDurationMs;
    }
}
