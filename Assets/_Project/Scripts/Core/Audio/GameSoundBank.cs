using System;
using UnityEngine;

namespace PawsAndLoot.Audio
{
    /// <summary>
    /// AUDIO-001 and AUDIO-003 data. Maps sound ids to clips and holds the mix
    /// levels.
    ///
    /// Every clip is optional so the game stays playable before audio has been
    /// authored. Missing clips are reported by the validator, not at runtime.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Paws & Loot/Game Sound Bank",
        fileName = "GameSoundBank")]
    public sealed class GameSoundBank : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public GameSoundId soundId;
            public AudioClip clip;

            [Range(0f, 1f)]
            public float volume;
        }

        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        [Header("Mixing (AUDIO-003)")]
        [SerializeField, Range(0f, 1f)]
        private float musicVolume = 0.35f;

        [SerializeField]
        private AudioClip musicTrack;

        /// <summary>
        /// How far the music drops while voice capture is running. Kept well
        /// below one so speech recognition is not fighting the background.
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float voiceDuckingMultiplier = 0.25f;

        public float MusicVolume => musicVolume;
        public AudioClip MusicTrack => musicTrack;
        public float VoiceDuckingMultiplier => voiceDuckingMultiplier;
        public int EntryCount => entries.Length;

        public AudioClip Resolve(GameSoundId soundId)
        {
            foreach (Entry entry in entries)
            {
                if (entry.soundId == soundId)
                {
                    return entry.clip;
                }
            }

            return null;
        }

        public float GetVolume(GameSoundId soundId)
        {
            foreach (Entry entry in entries)
            {
                if (entry.soundId == soundId)
                {
                    return entry.volume <= 0f ? 1f : entry.volume;
                }
            }

            return 1f;
        }

        /// <summary>
        /// Creates one entry per sound id so the Inspector shows the full list
        /// to fill in rather than an empty array.
        /// </summary>
        public void EnsureAllSoundIds()
        {
            var ids = (GameSoundId[])Enum.GetValues(typeof(GameSoundId));
            var rebuilt = new Entry[ids.Length - 1];
            int write = 0;
            foreach (GameSoundId id in ids)
            {
                if (id == GameSoundId.None)
                {
                    continue;
                }

                rebuilt[write++] = new Entry
                {
                    soundId = id,
                    clip = Resolve(id),
                    volume = GetVolume(id)
                };
            }

            entries = rebuilt;
        }

        public int CountMissingClips()
        {
            int missing = 0;
            foreach (Entry entry in entries)
            {
                if (entry.clip == null)
                {
                    missing++;
                }
            }

            return missing;
        }
    }
}
