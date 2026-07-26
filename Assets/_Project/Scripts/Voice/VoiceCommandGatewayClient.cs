using System;
using System.Collections;
using System.Text;
using PawsAndLoot.Companions;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace PawsAndLoot.Voice
{
    [Serializable]
    internal sealed class VoiceGatewayRequest
    {
        public string requestId;
        public string role;
        public string locale;
        public string audioWavBase64;
    }

    [Serializable]
    internal sealed class VoiceGatewayResponse
    {
        public string requestId;
        public string transcript;
        public string commandId;
        public string reasonCode;
        public string error;
    }

    public readonly struct VoiceGatewayResult
    {
        public VoiceGatewayResult(
            string requestId,
            string transcript,
            CompanionCommandId commandId,
            string reasonCode,
            string error)
        {
            RequestId = requestId;
            Transcript = transcript;
            CommandId = commandId;
            ReasonCode = reasonCode;
            Error = error;
        }

        public string RequestId { get; }
        public string Transcript { get; }
        public CompanionCommandId CommandId { get; }
        public string ReasonCode { get; }
        public string Error { get; }
        public bool Succeeded => string.IsNullOrWhiteSpace(Error);
    }

    public interface IVoiceCommandGateway
    {
        void RequestCommand(
            byte[] wavBytes,
            PlayerRole role,
            string requestId,
            Action<VoiceGatewayResult> completed);
    }

    public sealed class VoiceCommandGatewayClient :
        MonoBehaviour,
        IVoiceCommandGateway
    {
        [SerializeField]
        private VoiceConfig config;

        public void Configure(VoiceConfig configuredConfig)
        {
            config = configuredConfig;
        }

        public void RequestCommand(
            byte[] wavBytes,
            PlayerRole role,
            string requestId,
            Action<VoiceGatewayResult> completed)
        {
            if (completed == null)
            {
                throw new ArgumentNullException(nameof(completed));
            }

            StartCoroutine(Send(
                wavBytes,
                role,
                requestId,
                completed));
        }

        private void Awake()
        {
            if (config == null && GameConfigService.IsInitialized)
            {
                config = GameConfigService.Current.Voice;
            }
        }

        private IEnumerator Send(
            byte[] wavBytes,
            PlayerRole role,
            string requestId,
            Action<VoiceGatewayResult> completed)
        {
            if (config == null)
            {
                completed(CreateError(
                    requestId,
                    "VoiceConfig is unavailable."));
                yield break;
            }

            var payload = new VoiceGatewayRequest
            {
                requestId = requestId,
                role = role.ToString(),
                locale = "ko-KR",
                audioWavBase64 = Convert.ToBase64String(
                    wavBytes ?? Array.Empty<byte>())
            };
            byte[] json = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(payload));
            using var request = new UnityWebRequest(
                config.GatewayEndpoint,
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(json),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Mathf.CeilToInt(
                    config.RequestTimeoutSeconds)
            };
            request.SetRequestHeader(
                "Content-Type",
                "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error =
                    $"음성 중계 서버 오류 ({request.responseCode}).";
                GameLogger.Warning(
                    GameLogCategory.Voice,
                    $"{error} {request.error}",
                    this);
                completed(CreateError(requestId, error));
                yield break;
            }

            VoiceGatewayResponse response;
            try
            {
                response = JsonUtility.FromJson<VoiceGatewayResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                GameLogger.Exception(
                    GameLogCategory.Voice,
                    exception,
                    "Voice gateway returned invalid JSON.",
                    this);
                completed(CreateError(
                    requestId,
                    "음성 응답을 읽지 못했습니다."));
                yield break;
            }

            if (response == null
                || !string.Equals(
                    response.requestId,
                    requestId,
                    StringComparison.Ordinal))
            {
                completed(CreateError(
                    requestId,
                    "음성 응답 ID가 일치하지 않습니다."));
                yield break;
            }

            CompanionCommandId commandId =
                ParseCommandId(response.commandId);
            string responseError =
                string.IsNullOrWhiteSpace(response.error)
                    ? null
                    : response.error;
            completed(new VoiceGatewayResult(
                response.requestId,
                response.transcript ?? string.Empty,
                commandId,
                response.reasonCode ?? string.Empty,
                responseError));
        }

        private static CompanionCommandId ParseCommandId(
            string value)
        {
            return value switch
            {
                "TRACK" => CompanionCommandId.Track,
                "DISTRACT" => CompanionCommandId.Distract,
                _ => CompanionCommandId.None
            };
        }

        private static VoiceGatewayResult CreateError(
            string requestId,
            string error)
        {
            return new VoiceGatewayResult(
                requestId,
                string.Empty,
                CompanionCommandId.None,
                "ERROR",
                error);
        }
    }
}
