using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Integration.Voice
{
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
        private AudioClip clip;
        private string deviceName;
        private bool recording;

        public bool IsAvailable => Microphone.devices != null;

        public IEnumerator Begin(
            float maximumSeconds,
            Action permissionRequested,
            Action recordingStarted,
            Action<string> failed)
        {
            permissionRequested?.Invoke();

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
                failed?.Invoke("MIC_START_FAILED:" + exception.Message);
                yield break;
            }

            if (clip == null)
            {
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
            if (Microphone.IsRecording(deviceName))
            {
                Microphone.End(deviceName);
            }

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
            if (recording && !string.IsNullOrEmpty(deviceName)
                && Microphone.IsRecording(deviceName))
            {
                Microphone.End(deviceName);
            }

            recording = false;
            clip = null;
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
