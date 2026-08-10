using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace PawliceAndPurrglar.Integration.Voice
{
    public sealed class LocalAiHealthClient
    {
        public IEnumerator GetGatewayHealth(
            string baseUrl,
            int timeoutSeconds,
            Action<LocalAiHealthResponse> completed,
            Action<string> failed)
        {
            using UnityWebRequest request = UnityWebRequest.Get(
                baseUrl.TrimEnd('/') + "/health");
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                failed?.Invoke("GATEWAY_HEALTH_FAILED:" + request.error);
                yield break;
            }

            LocalAiHealthResponse response;
            try
            {
                response = JsonUtility.FromJson<LocalAiHealthResponse>(
                    request.downloadHandler.text);
            }
            catch (Exception exception)
            {
                failed?.Invoke("GATEWAY_HEALTH_INVALID:" + exception.Message);
                yield break;
            }

            if (response == null)
            {
                failed?.Invoke("GATEWAY_HEALTH_EMPTY");
                yield break;
            }

            completed?.Invoke(response);
        }

        public IEnumerator IsOllamaReady(
            string baseUrl,
            string model,
            int timeoutSeconds,
            Action<bool> completed)
        {
            using UnityWebRequest request = UnityWebRequest.Get(
                baseUrl.TrimEnd('/') + "/api/tags");
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(false);
                yield break;
            }

            string text = request.downloadHandler.text ?? string.Empty;
            completed?.Invoke(text.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
