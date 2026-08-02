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
        /// Which file plays for which event, by name without an extension.
        ///
        /// The extension is looked up rather than written down. Every one of these
        /// arrived from freesound named <c>.mp3</c> and only two of them were: the
        /// rest were WAV and one was FLAC. Unity picks its importer by extension, so
        /// a mislabelled file is at best confusing and at worst silent, and hard-coding
        /// the extension here would make the mapping wrong again the next time
        /// somebody saves in a different format.
        /// </summary>
        private static readonly (GameSoundId Id, string Stem)[] Mapping =
        {
            (GameSoundId.CommandSucceeded, "sfx_command_ok"),
            (GameSoundId.CommandFailed, "sfx_command_fail"),
            (GameSoundId.LootAcquired, "sfx_loot_pickup"),
            (GameSoundId.LootSold, "sfx_loot_sold"),
            (GameSoundId.ArrestStarted, "sfx_arrest_start"),
            (GameSoundId.ArrestCompleted, "sfx_arrest_done"),
            (GameSoundId.DogBark, "sfx_dog_bark"),
            (GameSoundId.CatMeow, "sfx_cat_meow"),
            (GameSoundId.Victory, "sfx_victory"),
            (GameSoundId.Defeat, "sfx_defeat")
        };

        /// <summary>
        /// The audio formats Unity imports, in the order they are tried.
        /// </summary>
        private static readonly string[] Extensions =
        {
            ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".aif"
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
            foreach ((GameSoundId id, string stem) in Mapping)
            {
                AudioClip clip = null;
                foreach (string extension in Extensions)
                {
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                        $"{ClipDirectory}/{stem}{extension}");
                    if (clip != null)
                    {
                        break;
                    }
                }

                if (clip == null)
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] No file named '{stem}' in "
                        + $"{ClipDirectory}, so {id} stays silent.");
                    continue;
                }

                if (!bank.TryAssignClip(id, clip))
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] The bank has no entry for {id}.");
                    continue;
                }

                assigned++;

                // Length is reported because it is the thing most likely to be
                // wrong and the thing nobody checks. A three-second blip on a
                // command that fires every few seconds overlaps itself.
                if (clip.length > 2.5f)
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] {id} is {clip.length:0.0}s long. Anything "
                        + "over about two seconds outstays its welcome unless it "
                        + "is the end of a match.");
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
