using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace PawliceAndPurrglar.Integration.Voice
{
    public sealed class LocalAiVoiceClient
    {
        public IEnumerator Submit(
            string baseUrl,
            byte[] audio,
            float timeoutSeconds,
            string petId,
            string contextJson,
            Action<LocalAiVoiceResponse> completed,
            Action<string> failed,
            Action processingStarted)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                failed?.Invoke("GATEWAY_URL_MISSING");
                yield break;
            }

            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection(
                    "audio",
                    audio ?? Array.Empty<byte>(),
                    "command.wav",
                    "audio/wav"),
                new MultipartFormDataSection("language", "ko"),
                new MultipartFormDataSection(
                    "actorRole",
                    petId == "dog" ? "POLICE" : "THIEF"),
                new MultipartFormDataSection(
                    "animalType",
                    petId == "dog" ? "DOG" : "CAT"),
                new MultipartFormDataSection("context", contextJson ?? "{}")
            };

            using UnityWebRequest request = UnityWebRequest.Post(
                baseUrl.TrimEnd('/') + "/v1/voice-command",
                sections);
            request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            processingStarted?.Invoke();
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                failed?.Invoke("VOICE_COMMAND_REQUEST_FAILED:" + request.error);
                yield break;
            }

            LocalAiVoiceResponse response;
            try
            {
                response = JsonUtility.FromJson<LocalAiVoiceResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                failed?.Invoke("VOICE_COMMAND_RESPONSE_INVALID:" + exception.Message);
                yield break;
            }

            if (response == null)
            {
                failed?.Invoke("VOICE_COMMAND_RESPONSE_EMPTY");
                yield break;
            }

            completed?.Invoke(response);
        }
    }
}
