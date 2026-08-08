using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    /// <summary>
    /// Browser recording, behind the same seam as the native one.
    ///
    /// Unity's `Microphone` does not capture on WebGL, so the browser records and
    /// hands back encoded bytes. That difference is real and cannot be configured
    /// away — but it is the *only* difference, and it belongs here rather than
    /// smeared through the state machine as <c>#if UNITY_WEBGL</c>. With both
    /// platforms behind <see cref="IVoiceCaptureProvider"/>, everything after
    /// "here are the bytes" is one code path, which is what makes a Windows build
    /// a real rehearsal for the WebGL submission.
    ///
    /// The awkward part is the callback: the JS side can only reach Unity through
    /// <c>SendMessage(objectName, method, json)</c>, so a MonoBehaviour has to own
    /// the method and forward it here via
    /// <see cref="SubmitCallbackPayload"/>. That is why this takes a host name
    /// instead of being self-contained.
    /// </summary>
    public sealed class BrowserVoiceCaptureProvider : IVoiceCaptureProvider
    {
        private readonly string hostObjectName;
        private readonly string callbackMethodName;

        /// <summary>
        /// How long to wait for bytes after asking the recorder to stop. Encoding a
        /// few seconds of Opus is fast; this exists so a recorder that never fires
        /// `onstop` ends as a reported failure instead of a state machine parked in
        /// `Recording` with the key dead.
        /// </summary>
        private readonly float resultTimeoutSeconds;

        private bool awaitingResult;
        private bool resultArrived;
        private bool stopRequested;
        private VoiceCaptureData completedData;
        private string failureCode;

        public BrowserVoiceCaptureProvider(
            string configuredHostObjectName,
            string configuredCallbackMethodName,
            float configuredResultTimeoutSeconds = 4f)
        {
            hostObjectName = configuredHostObjectName;
            callbackMethodName = configuredCallbackMethodName;
            resultTimeoutSeconds = Mathf.Max(0.5f, configuredResultTimeoutSeconds);
        }

        /// <summary>
        /// Always true, and deliberately not a guess.
        ///
        /// Whether a microphone exists is something only the browser knows, and it
        /// will not say until asked — `getUserMedia` is what produces the answer.
        /// Reporting false here would refuse before asking; the real verdict
        /// arrives as `MIC_REQUIRES_HTTPS`, `MICROPHONE_UNSUPPORTED`, or a denied
        /// permission, each of which names its own fix.
        /// </summary>
        public bool IsAvailable => true;

        public IEnumerator Begin(
            float maximumSeconds,
            Action permissionRequested,
            Action recordingStarted,
            Action<string> failed)
        {
            awaitingResult = true;
            resultArrived = false;
            stopRequested = false;
            completedData = null;
            failureCode = null;

            permissionRequested?.Invoke();

            if (!TryStartRecorder(maximumSeconds))
            {
                awaitingResult = false;
                failed?.Invoke("BROWSER_CAPTURE_UNAVAILABLE");
                yield break;
            }

            // The permission prompt is asynchronous and the browser does not tell
            // us when capture truly begins, so the recording state starts here.
            // A refusal comes back through the same callback as a failure.
            recordingStarted?.Invoke();
        }

        /// <summary>
        /// False off WebGL, where the plugin does not exist. Reported rather than
        /// compiled out so choosing this provider on the wrong platform is a
        /// message instead of a silent no-op.
        /// </summary>
        private bool TryStartRecorder(float maximumSeconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceMediaRecorder_Start(
                hostObjectName,
                callbackMethodName,
                maximumSeconds);
            return true;
#else
            return false;
#endif
        }

        public IEnumerator End(
            Action<VoiceCaptureData> completed,
            Action<string> failed)
        {
            if (!awaitingResult && !resultArrived)
            {
                failed?.Invoke("MIC_NOT_RECORDING");
                yield break;
            }

            RequestStop();

            float deadline = Time.realtimeSinceStartup + resultTimeoutSeconds;
            while (!resultArrived && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            awaitingResult = false;

            if (!resultArrived)
            {
                failed?.Invoke("VOICE_CAPTURE_NO_RESPONSE");
                yield break;
            }

            if (!string.IsNullOrEmpty(failureCode))
            {
                failed?.Invoke(failureCode);
                yield break;
            }

            if (completedData == null
                || completedData.AudioBytes == null
                || completedData.AudioBytes.Length == 0)
            {
                failed?.Invoke("VOICE_AUDIO_EMPTY");
                yield break;
            }

            completed?.Invoke(completedData);
        }

        public void Cancel()
        {
            if (awaitingResult)
            {
                RequestStop();
            }

            awaitingResult = false;
            resultArrived = false;
            completedData = null;
            failureCode = null;
        }

        /// <summary>
        /// Forwarded by the host MonoBehaviour's `SendMessage` target.
        ///
        /// The browser's own stop timer can fire before <see cref="End"/> asks it
        /// to, so a payload may arrive while nothing is waiting — it is stored
        /// rather than dropped, and `End` returns it immediately.
        /// </summary>
        public void SubmitCallbackPayload(string json)
        {
            resultArrived = true;

            BrowserCapturePayload payload;
            try
            {
                payload = JsonUtility.FromJson<BrowserCapturePayload>(json);
            }
            catch (Exception exception)
            {
                failureCode = "VOICE_CAPTURE_RESPONSE_INVALID:" + exception.Message;
                return;
            }

            if (payload == null || !string.IsNullOrWhiteSpace(payload.error))
            {
                failureCode = payload?.error ?? "VOICE_CAPTURE_FAILED";
                return;
            }

            byte[] audio;
            try
            {
                audio = Convert.FromBase64String(payload.base64Audio ?? string.Empty);
            }
            catch (FormatException)
            {
                failureCode = "VOICE_AUDIO_ENCODING_INVALID";
                return;
            }

            completedData = new VoiceCaptureData
            {
                MimeType = string.IsNullOrWhiteSpace(payload.mimeType)
                    ? "audio/webm"
                    : payload.mimeType,
                AudioBytes = audio
            };
        }

        /// <summary>
        /// Asks the recorder to stop, at most once — it throws if stopped while
        /// already inactive, and the key release, the elapsed cap and Escape can
        /// all arrive for one recording.
        /// </summary>
        private void RequestStop()
        {
            if (stopRequested)
            {
                return;
            }

            stopRequested = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            PawsAndLoot_VoiceMediaRecorder_Stop();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceMediaRecorder_Start(
            string gameObjectName,
            string callbackMethod,
            float maximumSeconds);

        [DllImport("__Internal")]
        private static extern void PawsAndLoot_VoiceMediaRecorder_Stop();
#endif

        [Serializable]
        private sealed class BrowserCapturePayload
        {
            public string base64Audio;
            public string mimeType;
            public string error;
        }
    }
}
