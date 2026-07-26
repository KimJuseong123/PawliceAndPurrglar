using System;
using System.IO;
using UnityEngine;

namespace PawsAndLoot.Voice
{
    public static class WavEncoder
    {
        public static byte[] Encode(
            AudioClip clip,
            int sampleFrames)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            int frames = Mathf.Clamp(
                sampleFrames,
                0,
                clip.samples);
            int channels = clip.channels;
            float[] samples = new float[frames * channels];
            if (frames > 0 && !clip.GetData(samples, 0))
            {
                throw new InvalidOperationException(
                    "Could not read microphone samples.");
            }

            const short bitsPerSample = 16;
            int dataLength = samples.Length * sizeof(short);
            using var stream = new MemoryStream(44 + dataLength);
            using var writer = new BinaryWriter(stream);
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataLength);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * channels
                * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataLength);

            foreach (float sample in samples)
            {
                writer.Write((short)Mathf.RoundToInt(
                    Mathf.Clamp(sample, -1f, 1f)
                    * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
