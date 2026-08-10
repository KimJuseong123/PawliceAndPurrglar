using System;
using PawliceAndPurrglar.Companions;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Voice
{
    [Serializable]
    public sealed class LocalAiInterpretationDto
    {
        public string intent;
        public string targetType;
        public string targetId;
        public float confidence;
        public bool needsClarification;
    }

    [Serializable]
    public sealed class LocalAiTimingDto
    {
        public int stt;
        public int llm;
        public int total;
    }

    [Serializable]
    public sealed class LocalAiVoiceResponse
    {
        public string requestId;
        public string transcript;
        public string normalizedText;
        public string actorRole;
        public string animalType;
        public bool fallbackUsed;
        public string failureCode;
        public string failureMessage;
        public LocalAiInterpretationDto interpretation;
        public LocalAiTimingDto timingMs;
        public Vector3Dto lookWorldPosition;

        public VoiceCommandResult ToVoiceCommandResult()
        {
            LocalAiInterpretationDto result = interpretation
                ?? new LocalAiInterpretationDto
                {
                    intent = "NONE",
                    targetType = "NONE",
                    confidence = 0f,
                    needsClarification = true
                };

            return new VoiceCommandResult
            {
                transcript = transcript ?? string.Empty,
                interpretedCommand = result.intent ?? "NONE",
                targetId = result.targetId ?? string.Empty,
                targetType = result.targetType ?? "NONE",
                confidence = result.confidence,
                deliberatelyMisunderstood = false,
                animalFeedback = string.IsNullOrWhiteSpace(failureMessage)
                    ? result.intent ?? "NONE"
                    : failureMessage,
                failureCode = failureCode,
                failureMessage = failureMessage,
                requestId = requestId,
                fallbackUsed = fallbackUsed
            };
        }
    }

    [Serializable]
    public sealed class Vector3Dto
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new(x, y, z);

        public static Vector3Dto FromVector3(Vector3 value) => new()
        {
            x = value.x,
            y = value.y,
            z = value.z
        };
    }

    [Serializable]
    public sealed class LocalAiHealthResponse
    {
        public string service;
        public string status;
        public LocalAiServiceHealth stt;
        public LocalAiServiceHealth llm;

        public bool IsReady => string.Equals(
                service,
                "paws-local-ai",
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase);

        public bool CanHandleVoice => string.Equals(
                service,
                "paws-local-ai",
                StringComparison.OrdinalIgnoreCase)
            && stt != null
            && stt.ready
            && (string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "degraded", StringComparison.OrdinalIgnoreCase));
    }

    [Serializable]
    public sealed class LocalAiServiceHealth
    {
        public bool ready;
        public string model;
        public string device;
        public string error;
    }

    [Serializable]
    public sealed class LocalAiContextDto
    {
        public string actorRole;
        public string animalType;
        public string dogState;
        public string lookTargetId;
        public Vector3Dto lookWorldPosition;
        public string[] visibleTargetIds = Array.Empty<string>();
        public string[] availableCommands = Array.Empty<string>();
    }
}
