using System;
using System.Runtime.InteropServices;
using PawsAndLoot.Companions;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    [Serializable]
    public sealed class VoiceBackendEvent
    {
        public int eventVersion;
        public string type;
        public string gameSessionId;
        public string commandId;
        public string petId;
        public long serverTimestamp;
        public VoiceBackendEventPayload payload;
    }

    [Serializable]
    public sealed class VoiceBackendEventPayload
    {
        public string transcript;
        public string model;
        public int commandSequence;
        public VoiceIntentClassificationResult classification;
    }

    [DisallowMultipleComponent]
    public sealed class VoiceBackendSocketClient : MonoBehaviour
    {
        [SerializeField] private string backendBaseUrl;
        [SerializeField] private string sessionId;
        [SerializeField] private string hostCapabilityToken;

        public event Action<VoiceBackendEvent> EventReceived;

        public void Configure(
            string configuredBackendBaseUrl,
            string configuredSessionId,
            string configuredHostCapabilityToken)
        {
            backendBaseUrl = configuredBackendBaseUrl;
            sessionId = configuredSessionId;
            hostCapabilityToken = configuredHostCapabilityToken;
        }

        public void Connect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string websocketUrl = backendBaseUrl
                .Replace("https://", "wss://")
                .Replace("http://", "ws://")
                .TrimEnd('/')
                + "/api/game/voice-events";
            PawsAndLoot_VoiceBackendSocket_Connect(
                gameObject.name,
                websocketUrl,
                sessionId,
                hostCapabilityToken);
#endif
        }

        public void OnBackendEvent(string json)
        {
            VoiceBackendEvent backendEvent =
                JsonUtility.FromJson<VoiceBackendEvent>(json);
            if (backendEvent != null)
            {
                EventReceived?.Invoke(backendEvent);
            }
        }

        public void RegisterVoiceContext(string commandId, string contextJson)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceBackendSocket_SendContext(
                gameObject.name,
                commandId,
                contextJson);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceBackendSocket_Connect(
            string gameObjectName,
            string websocketUrl,
            string sessionId,
            string token);

        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceBackendSocket_SendContext(
            string gameObjectName,
            string commandId,
            string contextJson);
#endif
    }
}
