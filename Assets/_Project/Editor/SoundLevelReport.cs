using System;
using System.Collections.Generic;
using System.Text;
using PawliceAndPurrglar.Audio;
using UnityEditor;
using UnityEngine;

namespace PawliceAndPurrglar.Editor
{
    /// <summary>
    /// How loud each clip in the bank actually is.
    ///
    /// Written because "turn the loud ones down" is not a decision anybody can
    /// make by ear across thirty-seven files that arrived from thirty-seven
    /// different recordings. Peak says nothing on its own — almost every file
    /// here peaks at 1.0, because that is what normalising does — so the number
    /// that matters is RMS, which is roughly what a listener calls loudness.
    ///
    /// Reads the decoded samples through <see cref="AudioClip.GetData"/>, which
    /// is why this lives in Unity rather than in a script beside the files: six
    /// of the clips are MP3, FLAC and OGG, and nothing on this machine decodes
    /// those.
    ///
    /// Batch mode:
    /// <code>
    /// -executeMethod PawliceAndPurrglar.Editor.SoundLevelReport.Report
    /// </code>
    /// </summary>
    internal static class SoundLevelReport
    {
        private const string BankPath =
            "Assets/_Project/Resources/Audio/GameSoundBank.asset";

        [MenuItem("PawliceAndPurrglar/Setup/Report Sound Levels")]
        public static void Report()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
            if (bank == null)
            {
                Debug.LogError($"[AUDIO-001] No sound bank at '{BankPath}'.");
                return;
            }

            var measured = new List<(string Name, float Peak, float Rms,
                float Volume, float Effective)>();
            foreach (GameSoundId id in Enum.GetValues(typeof(GameSoundId)))
            {
                if (id == GameSoundId.None)
                {
                    continue;
                }

                AudioClip clip = bank.Resolve(id);
                if (clip == null)
                {
                    continue;
                }

                if (!TryMeasure(clip, out float peak, out float rms))
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] {id} ({clip.name}) could not be read. "
                        + "A streaming clip has no samples to fetch.");
                    continue;
                }

                float volume = bank.GetVolume(id);
                measured.Add((
                    $"{id} ({clip.name})",
                    peak,
                    rms,
                    volume,
                    rms * volume));
            }

            // By what the player hears, which is the recording scaled by the
            // volume in the bank. Sorting by the raw level would put a loud file
            // that has already been turned down at the top of the list of things
            // to turn down.
            measured.Sort((left, right) =>
                right.Effective.CompareTo(left.Effective));

            var report = new StringBuilder();
            report.AppendLine(
                "[AUDIO-001] Loudness, loudest as heard first. "
                + "RMS is the number that matters; peak is 1.0 on almost "
                + "everything because that is what normalising does.");
            foreach ((string name, float peak, float rms, float volume,
                float effective) in measured)
            {
                report.AppendLine(
                    $"  {effective:0.000} heard   rms {rms:0.000}   "
                    + $"peak {peak:0.00}   volume {volume:0.00}   {name}");
            }

            Debug.Log(report.ToString());
        }

        private static bool TryMeasure(
            AudioClip clip,
            out float peak,
            out float rms)
        {
            peak = 0f;
            rms = 0f;
            int total = clip.samples * clip.channels;
            if (total <= 0)
            {
                return false;
            }

            var samples = new float[total];
            if (!clip.GetData(samples, 0))
            {
                return false;
            }

            double sum = 0d;
            foreach (float sample in samples)
            {
                float magnitude = Mathf.Abs(sample);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }

                sum += (double)sample * sample;
            }

            rms = Mathf.Sqrt((float)(sum / total));
            return true;
        }
    }
}
