using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PawsAndLoot.Integration.Voice
{
    [Serializable]
    public sealed class VoiceCommandResponse
    {
        public string commandId;
        public string status;
    }

    [Serializable]
    public sealed class VoiceCapturePayload
    {
        public string mimeType;
        public string base64Audio;
        public string error;
    }

    public sealed class VoiceCommandBackendClient
    {
        public IEnumerator Submit(
            string baseUrl,
            string sessionId,
            string petId,
            string clientCommandId,
            string capabilityToken,
            byte[] audio,
            string mimeType,
            float timeoutSeconds,
            Action<VoiceCommandResponse> completed,
            Action<string> failed)
        {
            string url = baseUrl.TrimEnd('/') + "/api/game/voice-commands";
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            var form = new WWWForm();
            form.AddField("gameSessionId", sessionId ?? string.Empty);
            form.AddField("petId", petId ?? string.Empty);
            form.AddField("clientCommandId", clientCommandId ?? string.Empty);
            form.AddBinaryData(
                "audio",
                audio,
                "voice.webm",
                string.IsNullOrWhiteSpace(mimeType) ? "audio/webm" : mimeType);
            request.uploadHandler = new UploadHandlerRaw(form.data);
            request.uploadHandler.contentType = form.headers["Content-Type"];
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + capabilityToken);
            request.timeout = Mathf.CeilToInt(Mathf.Max(1f, timeoutSeconds));

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                failed?.Invoke(request.error ?? "VOICE_UPLOAD_FAILED");
                yield break;
            }

            VoiceCommandResponse response;
            try
            {
                response = JsonUtility.FromJson<VoiceCommandResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                failed?.Invoke("INVALID_VOICE_RESPONSE:" + exception.Message);
                yield break;
            }

            if (response == null || string.IsNullOrWhiteSpace(response.commandId))
            {
                failed?.Invoke("INVALID_VOICE_RESPONSE");
                yield break;
            }

            completed?.Invoke(response);
        }
    }
}
