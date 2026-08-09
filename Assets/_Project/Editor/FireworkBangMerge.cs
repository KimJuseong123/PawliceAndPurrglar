using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Joins the two firework recordings into the one clip the game plays.
    ///
    /// The bang arrived as two files that have to sound in order — the crack and
    /// then the tail. Nothing in this project can promise that: the audio layer
    /// is one <c>PlayOneShot</c>, so two calls in the same frame come out on top
    /// of each other, and scheduling the second one by the first one's length
    /// means the gap is a number somebody has to keep right.
    ///
    /// Joining them makes the order a property of the file. There is one id, one
    /// clip, and no timing to maintain — which is worth doing precisely because
    /// the gap between the two is never going to need changing.
    ///
    /// Reads from <c>ArtSource/Audio</c> and writes to <c>Audio/SFX</c>. The two
    /// parts stay in the repository so this can be run again; they are not in
    /// <c>Audio/SFX</c> because everything in there is expected to be a sound the
    /// bank maps, and two thirds of a firework is not.
    ///
    /// Batch mode:
    /// <code>
    /// -executeMethod PawsAndLoot.Editor.FireworkBangMerge.Merge
    /// </code>
    /// </summary>
    internal static class FireworkBangMerge
    {
        private const string SourceDirectory = "Assets/_Project/ArtSource/Audio";
        private const string OutputPath =
            "Assets/_Project/Audio/SFX/sfx_firework_bang.wav";

        private const string FirstPath =
            SourceDirectory + "/sfx_firework_bang_1.mp3";
        private const string SecondPath =
            SourceDirectory + "/sfx_firework_bang_2.wav";

        [MenuItem("PawliceAndPurrglar/Setup/Merge Firework Bang Clips")]
        public static void Merge()
        {
            AudioClip first = Load(FirstPath);
            AudioClip second = Load(SecondPath);
            if (first == null || second == null)
            {
                return;
            }

            // Unity has already decoded both by the time they are AudioClips, so
            // the MP3 and the WAV arrive here as the same thing. That is the only
            // reason this is an editor script and not a shell command — there is
            // no MP3 decoder on this machine.
            int channels = Mathf.Max(first.channels, second.channels);
            int frequency = Mathf.Max(first.frequency, second.frequency);

            float[] head = ReadInterleaved(first, channels, frequency);
            float[] tail = ReadInterleaved(second, channels, frequency);
            if (head == null || tail == null)
            {
                return;
            }

            var joined = new float[head.Length + tail.Length];
            Array.Copy(head, joined, head.Length);
            Array.Copy(tail, 0, joined, head.Length, tail.Length);

            // A short fade across the seam. Two recordings butted together almost
            // never meet at zero, and a step in the waveform is a click — the one
            // artefact a listener notices every single time.
            CrossfadeSeam(joined, head.Length, channels, frequency);

            WriteWav(OutputPath, joined, channels, frequency);
            AssetDatabase.ImportAsset(
                OutputPath,
                ImportAssetOptions.ForceUpdate);

            float seconds =
                joined.Length / (float)(channels * frequency);
            Debug.Log(
                $"[AUDIO-001] sfx_firework_bang written: {seconds:0.00}s, "
                + $"{channels}ch @ {frequency}Hz "
                + $"({first.length:0.00}s + {second.length:0.00}s).");
        }

        private static AudioClip Load(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError(
                    $"[AUDIO-001] No clip at '{path}'. The firework bang cannot "
                    + "be built and stays silent.");
            }

            return clip;
        }

        /// <summary>
        /// The clip's samples, laid out for the target channel count and rate.
        ///
        /// Mono is duplicated rather than left short, and the rate is matched by
        /// linear interpolation. Neither part is high fidelity work; it exists so
        /// that two recordings made at different settings can be one file, which
        /// they have to be for the order to be guaranteed.
        /// </summary>
        private static float[] ReadInterleaved(
            AudioClip clip,
            int channels,
            int frequency)
        {
            var source = new float[clip.samples * clip.channels];
            if (!clip.GetData(source, 0))
            {
                Debug.LogError(
                    $"[AUDIO-001] Could not read samples from '{clip.name}'. "
                    + "Set its import Load Type to Decompress On Load.");
                return null;
            }

            int outFrames = Mathf.Max(
                1,
                Mathf.RoundToInt(clip.samples * (frequency / (float)clip.frequency)));
            var result = new float[outFrames * channels];
            for (int frame = 0; frame < outFrames; frame++)
            {
                float sourceFrame =
                    frame * clip.frequency / (float)frequency;

                // Both ends clamped, not just the upper one. The frame count is
                // rounded, so the last output frame can land a hair past the last
                // input frame — and reading `lower` unclamped is then off the end
                // of the array rather than a fraction of a sample early.
                int lower = Mathf.Clamp(
                    Mathf.FloorToInt(sourceFrame),
                    0,
                    clip.samples - 1);
                int upper = Mathf.Min(lower + 1, clip.samples - 1);
                float blend = Mathf.Clamp01(sourceFrame - lower);

                for (int channel = 0; channel < channels; channel++)
                {
                    // A mono source feeds every output channel; a stereo source
                    // read as mono takes its first.
                    int sourceChannel = channel % clip.channels;
                    float a = source[lower * clip.channels + sourceChannel];
                    float b = source[upper * clip.channels + sourceChannel];
                    result[frame * channels + channel] =
                        Mathf.Lerp(a, b, blend);
                }
            }

            return result;
        }

        private static void CrossfadeSeam(
            float[] samples,
            int seamSample,
            int channels,
            int frequency)
        {
            int fadeFrames = Mathf.Max(1, frequency / 200);
            int seamFrame = seamSample / channels;
            int totalFrames = samples.Length / channels;

            for (int offset = 0; offset < fadeFrames; offset++)
            {
                int outFrame = seamFrame - fadeFrames + offset;
                int inFrame = seamFrame + offset;
                if (outFrame < 0 || inFrame >= totalFrames)
                {
                    continue;
                }

                float t = (offset + 1) / (float)fadeFrames;
                for (int channel = 0; channel < channels; channel++)
                {
                    samples[outFrame * channels + channel] *= 1f - t;
                    samples[inFrame * channels + channel] *= t;
                }
            }
        }

        /// <summary>
        /// 16-bit PCM, because that is what every importer reads without an
        /// opinion and the source material is 16-bit anyway.
        /// </summary>
        private static void WriteWav(
            string path,
            float[] samples,
            int channels,
            int frequency)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)
                && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(frequency);
            writer.Write(frequency * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in samples)
            {
                writer.Write(
                    (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
