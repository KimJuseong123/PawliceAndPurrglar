using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
    public static class VoiceCaptureSettings
    {
        private const float DefaultMicrophoneSensitivity = 1f;
        private static float microphoneSensitivity = DefaultMicrophoneSensitivity;

        public static float MicrophoneSensitivity
        {
            get => microphoneSensitivity;
            set => microphoneSensitivity = Mathf.Clamp(value, 0.25f, 5f);
        }
    }

    [Serializable]
    public sealed class VoiceCaptureData
    {
        public string MimeType;
        public byte[] AudioBytes;
    }

    /// <summary>
    /// Platform capture seam. Providers only capture audio; transcription and
    /// command interpretation remain server responsibilities.
    /// </summary>
    public interface IVoiceCaptureProvider
    {
        bool IsAvailable { get; }

        IEnumerator Begin(
            float maximumSeconds,
            Action permissionRequested,
            Action recordingStarted,
            Action<string> failed);

        IEnumerator End(
            Action<VoiceCaptureData> completed,
            Action<string> failed);

        void Cancel();
    }

    /// <summary>
    /// Native Unity microphone capture for Windows and the Editor. This class
    /// deliberately has no transcript fallback: missing hardware is a clear
    /// voice failure, not a successful fake command.
    /// </summary>
    public sealed class NativeVoiceCaptureProvider : IVoiceCaptureProvider
    {
        private const int SampleRate = 16000;
        private const int MinimumMilliseconds = 250;
        private const float SilenceRmsThreshold = 0.003f;

        /// <summary>
        /// The one provider currently allowed to hold the recording device.
        ///
        /// Windows hands a capture device to a single client, and this game asks
        /// twice: a <c>VoiceCommandInput</c> is attached to both role objects, so
        /// pressing the voice key started the police one and the thief one in the
        /// same frame. The second <c>Microphone.Start</c> left one of the two
        /// clips receiving nothing, and either one's <c>Microphone.End</c> stopped
        /// the other's recording — that is where `VOICE_AUDIO_SILENT` came from,
        /// and why it was intermittent rather than constant (`ISSUE-069`).
        ///
        /// Static because the device is one machine-wide resource, not a field of
        /// whichever component happened to ask first.
        /// </summary>
        private static NativeVoiceCaptureProvider deviceOwner;

        private AudioClip clip;
        private string deviceName;
        private bool recording;

        public bool IsAvailable => Microphone.devices != null
            && Microphone.devices.Length > 0;

        /// <summary>
        /// Peak absolute sample of the last capture. Reported so a rejected
        /// recording says whether the device produced exact silence — a blocked
        /// Windows privacy setting returns a clip of zeros *successfully* — or
        /// something too quiet to be speech. The two look identical in the HUD
        /// and are fixed in different places.
        /// </summary>
        public float LastPeakAmplitude { get; private set; }

        public IEnumerator Begin(
            float maximumSeconds,
            Action permissionRequested,
            Action recordingStarted,
            Action<string> failed)
        {
            permissionRequested?.Invoke();

            if (deviceOwner != null && deviceOwner != this)
            {
                failed?.Invoke("MIC_HELD_BY_ANOTHER_CAPTURE");
                yield break;
            }

#if UNITY_2020_1_OR_NEWER
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                AsyncOperation request =
                    Application.RequestUserAuthorization(UserAuthorization.Microphone);
                while (request != null && !request.isDone)
                {
                    yield return null;
                }

                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    failed?.Invoke("MIC_PERMISSION_DENIED");
                    yield break;
                }
            }
#endif

            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                failed?.Invoke("MIC_DEVICE_MISSING");
                yield break;
            }

            deviceName = devices[0];
            int lengthSeconds = Mathf.Clamp(
                Mathf.CeilToInt(maximumSeconds),
                1,
                60);
            deviceOwner = this;
            try
            {
                clip = Microphone.Start(
                    deviceName,
                    false,
                    lengthSeconds,
                    SampleRate);
            }
            catch (Exception exception)
            {
                ReleaseDevice();
                failed?.Invoke("MIC_START_FAILED:" + exception.Message);
                yield break;
            }

            if (clip == null)
            {
                ReleaseDevice();
                failed?.Invoke("MIC_DEVICE_BUSY");
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 1f;
            while (!Microphone.IsRecording(deviceName)
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!Microphone.IsRecording(deviceName))
            {
                clip = null;
                ReleaseDevice();
                failed?.Invoke("MIC_DEVICE_BUSY");
                yield break;
            }

            recording = true;
            recordingStarted?.Invoke();
        }

        public IEnumerator End(
            Action<VoiceCaptureData> completed,
            Action<string> failed)
        {
            if (!recording || clip == null)
            {
                failed?.Invoke("MIC_NOT_RECORDING");
                yield break;
            }

            int sampleFrames = Microphone.GetPosition(deviceName);
            ReleaseDevice();
            recording = false;
            yield return null;

            if (sampleFrames <= 0)
            {
                failed?.Invoke("MIC_AUDIO_EMPTY");
                clip = null;
                yield break;
            }

            sampleFrames = Mathf.Min(sampleFrames, clip.samples);
            float[] samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0))
            {
                failed?.Invoke("MIC_AUDIO_READ_FAILED");
                clip = null;
                yield break;
            }

            int sampleCount = Mathf.Min(
                samples.Length,
                sampleFrames * clip.channels);
            int minimumFrames = Mathf.CeilToInt(
                SampleRate * (MinimumMilliseconds / 1000f));
            if (sampleFrames < minimumFrames)
            {
                clip = null;
                failed?.Invoke("VOICE_AUDIO_TOO_SHORT");
                yield break;
            }

            float sumSquares = 0f;
            float peak = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float value = samples[index];
                sumSquares += value * value;
                float magnitude = Mathf.Abs(value);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            LastPeakAmplitude = peak;
            float sensitivity = VoiceCaptureSettings.MicrophoneSensitivity;
            float rms = Mathf.Sqrt(sumSquares / Mathf.Max(1, sampleCount));
            float silenceThreshold = SilenceRmsThreshold
                / Mathf.Max(0.25f, sensitivity);
            if (rms < silenceThreshold)
            {
                clip = null;

                // Two different failures with one old name. A clip of exact
                // zeros is a device that was opened but is not being fed —
                // Windows privacy settings return that *successfully*. Anything
                // above zero is a real signal that was too quiet, which is a
                // volume or sensitivity matter. Naming them apart is the whole
                // point: the fix is in a different place.
                failed?.Invoke(peak <= 0f
                    ? "MIC_RETURNED_ONLY_ZEROS"
                    : "VOICE_AUDIO_SILENT:peak="
                        + peak.ToString("F4", CultureInfo.InvariantCulture));
                yield break;
            }

            if (!Mathf.Approximately(sensitivity, 1f))
            {
                float[] amplified = new float[sampleCount];
                for (int index = 0; index < sampleCount; index++)
                {
                    amplified[index] = Mathf.Clamp(
                        samples[index] * sensitivity,
                        -1f,
                        1f);
                }

                samples = amplified;
            }

            byte[] wav = WavAudioEncoder.Encode(
                samples,
                sampleCount,
                clip.channels,
                clip.frequency);
            clip = null;

            if (wav.Length == 0)
            {
                failed?.Invoke("MIC_AUDIO_EMPTY");
                yield break;
            }

            completed?.Invoke(new VoiceCaptureData
            {
                MimeType = "audio/wav",
                AudioBytes = wav
            });
        }

        public void Cancel()
        {
            ReleaseDevice();
            recording = false;
            clip = null;
        }

        /// <summary>
        /// Stops the recording and gives the device back, but only if this
        /// provider is the one holding it. The guard matters on cancellation:
        /// Escape and losing window focus cancel *every* capture component, and
        /// an unguarded <c>Microphone.End</c> would let the idle one stop the
        /// recording the player is in the middle of.
        /// </summary>
        private void ReleaseDevice()
        {
            if (deviceOwner != this)
            {
                return;
            }

            if (!string.IsNullOrEmpty(deviceName)
                && Microphone.IsRecording(deviceName))
            {
                Microphone.End(deviceName);
            }

            deviceOwner = null;
        }
    }

    public static class WavAudioEncoder
    {
        public static byte[] Encode(
            float[] samples,
            int sampleCount,
            int channels,
            int frequency)
        {
            if (samples == null || sampleCount <= 0 || channels <= 0 || frequency <= 0)
            {
                return Array.Empty<byte>();
            }

            sampleCount = Mathf.Min(sampleCount, samples.Length);
            int dataLength = sampleCount * sizeof(short);
            using var stream = new MemoryStream(44 + dataLength);
            using var writer = new BinaryWriter(stream);
            writer.Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + dataLength);
            writer.Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * sizeof(short));
            writer.Write((short)(channels * sizeof(short)));
            writer.Write((short)16);
            writer.Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataLength);

            for (int index = 0; index < sampleCount; index++)
            {
                float value = Mathf.Clamp(samples[index], -1f, 1f);
                writer.Write((short)Mathf.RoundToInt(value * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
