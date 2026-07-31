using System.Linq;
using PawsAndLoot.Audio;
using UnityEditor;
using UnityEngine;

namespace PawsAndLoot.Editor
{
    /// <summary>
    /// Puts the clips that exist into the sound bank.
    ///
    /// The bank's ten entries are created by the scene builder and left empty for a
    /// human to fill in the inspector. That is fine for one clip and wrong for ten:
    /// filling them by hand is not reproducible, and an entry that quietly loses its
    /// clip on a merge looks exactly like a sound nobody has recorded yet.
    ///
    /// So the mapping is written down here, by file name, and applied. A name that has
    /// no file is reported rather than skipped — silence is the failure mode of every
    /// audio bug, and it needs something in a log.
    /// </summary>
    internal static class SoundBankSetup
    {
        private const string BankPath =
            "Assets/_Project/Settings/Audio/GameSoundBank.asset";

        private const string ClipDirectory = "Assets/_Project/Audio/SFX";

        /// <summary>
        /// Which file plays for which event.
        ///
        /// Only what the repository actually has. The siren stands in for the arrest
        /// because an arrest is the loudest thing that happens in a match and a
        /// four-minute round with no audible ending reads as a crash; it is a
        /// placeholder and should be replaced by a whistle or a cuff.
        /// </summary>
        private static readonly (GameSoundId Id, string File)[] Mapping =
        {
            (GameSoundId.CatMeow, "sfx_cat_meow"),
            (GameSoundId.ArrestCompleted, "sfx_alarm_siren")
        };

        [MenuItem("Paws & Loot/Setup/Assign Sound Bank Clips")]
        public static void AssignClips()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
            if (bank == null)
            {
                Debug.LogError(
                    $"[AUDIO-001] No sound bank at '{BankPath}'. Rebuild the "
                    + "Game scene first.");
                return;
            }

            int assigned = 0;
            foreach ((GameSoundId id, string file) in Mapping)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    $"{ClipDirectory}/{file}.mp3");
                if (clip == null)
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] '{file}.mp3' is missing, so "
                        + $"{id} stays silent.");
                    continue;
                }

                if (bank.TryAssignClip(id, clip))
                {
                    assigned++;
                }
                else
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] The bank has no entry for {id}.");
                }
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[AUDIO-001] {assigned} clips assigned, "
                + $"{bank.CountMissingClips()} of {bank.EntryCount} entries "
                + "still without one.");
        }

        [MenuItem("Paws & Loot/Setup/Validate Sound Bank")]
        public static void ValidateBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
            if (bank == null)
            {
                throw new System.InvalidOperationException(
                    $"No sound bank at '{BankPath}'.");
            }

            // Every id the game can raise has to have an entry, clip or not. A missing
            // entry is a different bug from a missing clip: the first can never be
            // filled without touching code, the second is just work outstanding.
            GameSoundId[] missing = System.Enum
                .GetValues(typeof(GameSoundId))
                .Cast<GameSoundId>()
                .Where(id => id != GameSoundId.None && !bank.HasEntry(id))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new System.InvalidOperationException(
                    "The sound bank has no entry for "
                    + string.Join(", ", missing));
            }

            Debug.Log(
                $"[AUDIO-001] Sound bank validated: {bank.EntryCount} entries, "
                + $"{bank.CountMissingClips()} awaiting a clip.");
        }
    }
}
