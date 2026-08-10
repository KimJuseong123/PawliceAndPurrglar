using System;
using System.Collections;
using PawliceAndPurrglar.Companions;
using UnityEngine;
using UnityEngine.Networking;

namespace PawliceAndPurrglar.Integration.Voice
{
    [Serializable]
    public sealed class VoiceCommandResponse
    {
        public string commandId;
        public string status;

        /// <summary>
        /// Present only on a `?wait=1` request, where the server runs the whole
        /// pipeline and answers in this response instead of over the socket.
        ///
        /// The socket client is a jslib plugin and therefore WebGL-only, so
        /// without this a Windows build cannot use this backend at all — and then
        /// the two platforms are back on different AI stacks, which is what
        /// `VOICE-012` set out to end.
        /// </summary>
        public string transcript;

        public VoiceIntentClassificationResult classification;
        public string errorCode;
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
            // `?wait=1` on every platform. The socket remains for pushing events
            // to the *other* player; keeping it on the critical path would mean
            // Windows and WebGL receive their own answers by different routes,
            // and routes diverge.
            string url = baseUrl.TrimEnd('/')
                + "/api/game/voice-commands?wait=1";
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
