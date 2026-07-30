using System;
using System.Collections;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Integration.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

namespace PawsAndLoot.Integration.Voice
{
    [Serializable]
    internal sealed class VoiceSessionRegistrationRequest
    {
        public string gameSessionId;
        public string hostClientId;
        public VoiceSessionParticipant[] participants;
    }

    [Serializable]
    internal sealed class VoiceSessionParticipant
    {
        public string clientId;
        public string role;
        public string petId;
    }

    [Serializable]
    internal sealed class VoiceSessionRegistrationResponse
    {
        public string gameSessionId;
        public string hostToken;
        public string policeToken;
        public string thiefToken;
    }

    [DisallowMultipleComponent]
    public sealed class VoiceSessionCapabilityClient : MonoBehaviour
    {
        [SerializeField] private VoiceConfig voiceConfig;
        private bool registrationStarted;

        public void Configure(VoiceConfig configuredConfig)
        {
            voiceConfig = configuredConfig;
        }

        /// <summary>
        /// Registers a single-player browser session as a Host capability.
        /// The game remains offline; this token only authorizes the voice API
        /// and WebSocket event channel.
        /// </summary>
        public void RegisterOffline()
        {
            if (registrationStarted || voiceConfig == null)
            {
                return;
            }

            registrationStarted = true;
            string sessionId = Guid.NewGuid().ToString("N");
            VoiceCapabilityStore.Set(sessionId, string.Empty);
            StartCoroutine(Register(
                sessionId,
                "offline-player",
                Array.Empty<VoiceSessionParticipant>(),
                null));
        }

        public void RegisterForRoles(NetworkRoleBoard roleBoard)
        {
            if (registrationStarted
                || roleBoard == null
                || NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsServer
                || voiceConfig == null)
            {
                return;
            }

            registrationStarted = true;
            string sessionId = Guid.NewGuid().ToString("N");
            VoiceCapabilityStore.Set(sessionId, string.Empty);
            StartCoroutine(RegisterNetworkSession(sessionId, roleBoard));
        }

        private IEnumerator RegisterNetworkSession(
            string sessionId,
            NetworkRoleBoard roleBoard)
        {
            NetworkManager manager = NetworkManager.Singleton;
            ulong policeClientId = roleBoard.PoliceClientId;
            var participants = new System.Collections.Generic.List<VoiceSessionParticipant>();
            foreach (ulong clientId in manager.ConnectedClientsIds)
            {
                if (clientId == manager.LocalClientId) continue;
                PlayerRole role = clientId == policeClientId
                    ? PlayerRole.Police
                    : PlayerRole.Thief;
                participants.Add(new VoiceSessionParticipant
                {
                    clientId = clientId.ToString(),
                    role = role == PlayerRole.Police ? "POLICE" : "THIEF",
                    petId = role == PlayerRole.Police ? "dog" : "cat"
                });
            }

            yield return Register(
                sessionId,
                manager.LocalClientId.ToString(),
                participants.ToArray(),
                roleBoard);
        }

        private IEnumerator Register(
            string sessionId,
            string hostClientId,
            VoiceSessionParticipant[] participants,
            NetworkRoleBoard roleBoard)
        {
            var requestData = new VoiceSessionRegistrationRequest
            {
                gameSessionId = sessionId,
                hostClientId = hostClientId,
                participants = participants ?? Array.Empty<VoiceSessionParticipant>()
            };
            string json = JsonUtility.ToJson(requestData);
            using var request = new UnityWebRequest(
                voiceConfig.BackendBaseUrl.TrimEnd('/') + "/api/game/sessions",
                UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(json));
            request.uploadHandler.contentType = "application/json";
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.CeilToInt(
                Mathf.Max(1f, voiceConfig.RequestTimeoutSeconds));
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                registrationStarted = false;
                VoiceCapabilityStore.Clear();
                yield break;
            }

            VoiceSessionRegistrationResponse response =
                JsonUtility.FromJson<VoiceSessionRegistrationResponse>(
                    request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.hostToken))
            {
                registrationStarted = false;
                VoiceCapabilityStore.Clear();
                yield break;
            }

            VoiceCapabilityStore.Set(
                string.IsNullOrWhiteSpace(response.gameSessionId)
                    ? sessionId
                    : response.gameSessionId,
                response.hostToken);
            roleBoard?.ApplyVoiceTokensRpc(
                sessionId,
                response.hostToken,
                response.policeToken,
                response.thiefToken);
        }
    }
}
