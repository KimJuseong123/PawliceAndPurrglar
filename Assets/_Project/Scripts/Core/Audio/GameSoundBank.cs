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
        menuName = "PawliceAndPurrglar/Game Sound Bank",
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

        /// <summary>
        /// True when the vocabulary has a slot for this sound, clip or not.
        ///
        /// A missing entry and a missing clip are different bugs. The first cannot be
        /// fixed without touching code; the second is only work outstanding, and the
        /// game is meant to run silently either way.
        /// </summary>
        public bool HasEntry(GameSoundId soundId)
        {
            foreach (Entry entry in entries)
            {
                if (entry.soundId == soundId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Puts a clip in an existing slot. Returns false when there is no slot, so a
        /// caller can say so rather than appearing to have worked.
        /// </summary>
        public bool TryAssignClip(GameSoundId soundId, AudioClip clip)
        {
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].soundId != soundId)
                {
                    continue;
                }

                entries[index].clip = clip;
                return true;
            }

            return false;
        }

        /// <summary>
        /// True for the sounds raised at the moment the scene is about to change.
        ///
        /// The match-end stingers are raised by `MatchEndController` and the Result
        /// scene loads immediately after, which destroys the AudioSource built into
        /// the Game scene mid-clip. These two play from
        /// <see cref="PersistentOneShotAudio"/> instead so the whole clip is heard
        /// (`ISSUE-070`).
        ///
        /// Kept as code rather than a serialized flag on the entry: it is a fact
        /// about when the game raises the sound, not a mixing choice, and a field
        /// would default to false on every existing bank asset — silently
        /// reintroducing the bug.
        /// </summary>
        public static bool OutlivesTheScene(GameSoundId soundId)
        {
            return soundId == GameSoundId.Victory
                || soundId == GameSoundId.Defeat;
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
