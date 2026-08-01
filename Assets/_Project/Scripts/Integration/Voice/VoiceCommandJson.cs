using PawsAndLoot.Companions;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    [System.Serializable]
    public sealed class VoiceCommandResult
    {
        public string transcript;
        public string interpretedCommand;
        public string targetId;
        public string targetType;
        public float confidence;
        public bool deliberatelyMisunderstood;
        public string animalFeedback;
        public string failureCode;
        public string failureMessage;

        public string ToJson() => JsonUtility.ToJson(this);
    }

    [System.Serializable]
    public sealed class VoiceCommandJson
    {
        public string actor;
        public string intent;
        public string targetType;
        public string targetId;
        public float confidence;
        public string understanding;
        public string recognizedText;
        public string feedbackText;
        public bool deliberatelyMisunderstood;
        public string failureCode;
        public string failureMessage;

        public static VoiceCommandJson FromMatch(
            string transcript,
            CoreCommandMatch match)
        {
            return new VoiceCommandJson
            {
                actor = match.CommandId.ToString(),
                intent = match.CommandId.ToString().ToUpperInvariant(),
                targetType = "POSITION",
                targetId = string.Empty,
                confidence = match.Confidence,
                understanding = match.Understanding.ToString().ToUpperInvariant(),
                recognizedText = transcript ?? string.Empty,
                feedbackText = match.Accepted
                    ? CompanionCommandCatalog.GetDisplayName(match.CommandId)
                    : "명령을 이해하지 못했습니다."
            };
        }

        public string ToJson() => JsonUtility.ToJson(this);
    }
}
