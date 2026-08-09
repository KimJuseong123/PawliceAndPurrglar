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
        /// <summary>
        /// Under <c>Resources</c> since the lobby needed sound.
        ///
        /// The service that plays these is built into the Game scene, so every id
        /// raised in Bootstrap or Result — a role being handed out, the other
        /// player arriving, the voice model finishing — had nowhere to play. The
        /// fallback in <see cref="GameSoundService"/> loads the bank by name
        /// instead of by reference, and a name only resolves from here.
        ///
        /// Moved rather than copied. A second bank would drift from this one and
        /// the drift would be silent.
        /// </summary>
        private const string BankPath =
            "Assets/_Project/Resources/Audio/GameSoundBank.asset";

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
            (GameSoundId.Defeat, "sfx_defeat"),

            // C-4 through C-11, 2026-08-09.
            //
            // The sheet listed 71 rows across these sections and 11 files were
            // recorded. Everything else on those rows was struck out rather than
            // left waiting: an id with no file is a warning on every run of this
            // menu item, and 60 of them would have buried the ones that matter.
            (GameSoundId.DoorOpen, "sfx_door_open"),
            (GameSoundId.InventoryToggle, "sfx_inventory_toggle"),
            (GameSoundId.Jump, "sfx_jump"),
            (GameSoundId.PurchaseMade, "sfx_purchase_ok"),
            (GameSoundId.CountdownTick, "sfx_countdown_tick"),
            (GameSoundId.RoleAssignedPolice, "sfx_role_assigned_police"),
            (GameSoundId.RoleAssignedThief, "sfx_role_assigned_thief"),
            (GameSoundId.RaccoonChitter, "sfx_raccoon"),
            (GameSoundId.VoiceRecordStart, "sfx_voice_start"),
            (GameSoundId.VoiceRecordStop, "sfx_voice_stop"),
            (GameSoundId.VoiceModelReady, "sfx_voice_ready"),

            // Three ids that deliberately share a recording with one above.
            //
            // Asked for that way: a peer connecting is the same rising beep as
            // the microphone opening, and a failed transcription is the same
            // refusal as a rejected command. They stay separate ids because the
            // call sites are separate and the mix may want to part them later —
            // pointing two ids at one file costs nothing, and one id used from
            // two places cannot be told apart afterwards.
            (GameSoundId.PeerJoined, "sfx_voice_start"),
            (GameSoundId.PeerLeft, "sfx_voice_stop"),
            (GameSoundId.VoiceRecognizeFail, "sfx_command_fail"),

            // C-1 through C-3, 2026-08-09. Nineteen ids, sixteen recordings.
            //
            // `sfx_firework_bang` is built rather than received: it arrived as
            // two files that have to sound in order, and `FireworkBangMerge`
            // joins them so the order is a property of the file instead of a
            // timing somebody has to keep right.
            (GameSoundId.NoisePropSquawk, "sfx_chicken_squawk"),
            (GameSoundId.NoisePropFuse, "sfx_fuse_burn"),
            (GameSoundId.NoisePropBang, "sfx_firework_bang"),
            (GameSoundId.NoiseHeardFar, "sfx_bang_far"),
            (GameSoundId.ThrowCharge, "sfx_throw_charge"),
            (GameSoundId.ThrowReleased, "sfx_throw_release"),
            (GameSoundId.ThrowHitBody, "sfx_throw_hit_body"),
            (GameSoundId.Stunned, "sfx_stunned"),
            (GameSoundId.Blinded, "sfx_ink_splat"),
            (GameSoundId.TrapPlaced, "sfx_trap_place"),
            (GameSoundId.TrapSlip, "sfx_banana_slip"),
            (GameSoundId.SensorTripped, "sfx_sensor_trip"),
            (GameSoundId.LureTaken, "sfx_lure_eat"),
            (GameSoundId.LootPickupStart, "sfx_loot_search"),
            (GameSoundId.GlassBreak, "sfx_glass_break"),
            (GameSoundId.CaseKeyUnlock, "sfx_case_unlock"),

            // Three that are wired and waiting for a recording.
            //
            // Left in on purpose, unlike the sixty rows struck off the sheet: the
            // warning each one prints on every run is the outstanding list, and
            // three of those is a reminder rather than the noise sixty would be.
            // The code that raises them is finished, so the day the files land
            // they work without anybody touching this file.
            (GameSoundId.DogAlerted, "sfx_dog_alert"),
            (GameSoundId.CatAlerted, "sfx_cat_alert"),
            (GameSoundId.TrapSticky, "sfx_glue_stick")
        };

        /// <summary>
        /// The audio formats Unity imports, in the order they are tried.
        /// </summary>
        private static readonly string[] Extensions =
        {
            ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".aif"
        };

        /// <summary>
        /// Ids whose clip is meant to run past the two-and-a-half second mark.
        ///
        /// Listed so the length warning below stays worth reading. The countdown
        /// is the only one here that is not a match-end sting: the recording is a
        /// whole three-second count rather than one beep, which is why it is
        /// raised once at the start of the count instead of on every second.
        /// </summary>
        private static bool IsLongByDesign(GameSoundId id)
        {
            return GameSoundBank.OutlivesTheScene(id)
                || id == GameSoundId.CountdownTick;
        }

        /// <summary>
        /// Grows the bank to fit the enum, fills it in, and checks the result.
        ///
        /// The three steps have always had to happen in this order and were three
        /// separate things to remember: a new id with no entry is silent for good,
        /// and <c>Validate Sound Bank</c> throws on it. Adding one is the moment
        /// that is easiest to get half-right, so the whole sequence is one call.
        ///
        /// Batch mode:
        /// <code>
        /// -executeMethod PawsAndLoot.Editor.SoundBankSetup.RebuildBank
        /// </code>
        /// </summary>
        [MenuItem("PawliceAndPurrglar/Setup/Rebuild Sound Bank")]
        public static void RebuildBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<GameSoundBank>(BankPath);
            if (bank == null)
            {
                Debug.LogError(
                    $"[AUDIO-001] No sound bank at '{BankPath}'.");
                return;
            }

            bank.EnsureAllSoundIds();
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            AssignClips();
            ValidateBank();
        }

        [MenuItem("PawliceAndPurrglar/Setup/Assign Sound Bank Clips")]
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
                //
                // The match-end stingers are exempt — the sentence below always
                // said "unless it is the end of a match" and then warned about
                // them anyway, which trains you to ignore the warning.
                if (clip.length > 2.5f && !IsLongByDesign(id))
                {
                    Debug.LogWarning(
                        $"[AUDIO-001] {id} is {clip.length:0.0}s long. Anything "
                        + "over about two seconds outstays its welcome unless it "
                        + "is the end of a match.");
                }

                // Printed for every clip, not just the suspicious ones. The length
                // on disk is the first thing to check when a sound is cut short,
                // and until now nothing reported it.
                Debug.Log(
                    $"[AUDIO-001] {id} ← {stem} ({clip.length:0.00}s)");
            }

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[AUDIO-001] {assigned} clips assigned, "
                + $"{bank.CountMissingClips()} of {bank.EntryCount} entries "
                + "still without one.");
        }

        [MenuItem("PawliceAndPurrglar/Setup/Validate Sound Bank")]
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
