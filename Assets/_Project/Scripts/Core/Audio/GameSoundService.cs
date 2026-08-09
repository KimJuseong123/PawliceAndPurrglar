using System.Collections.Generic;
using UnityEngine;

namespace PawsAndLoot.Audio
{
    /// <summary>
    /// AUDIO-002. One-way sink for sound requests.
    ///
    /// Deliberately fire and forget: <see cref="Play"/> returns nothing and
    /// never throws, so no rule can branch on whether audio worked. Requests are
    /// counted so tests can assert that a rule asked for a sound without needing
    /// a clip or an audio device.
    ///
    /// A request with no clip assigned is still recorded, which keeps gameplay
    /// identical whether or not audio has been authored yet.
    /// </summary>
    public sealed class GameSoundService : MonoBehaviour
    {
        private static GameSoundService _instance;

        [SerializeField]
        private GameSoundBank bank;

        [SerializeField]
        private AudioSource oneShotSource;

        [SerializeField]
        private AudioSource musicSource;

        /// <summary>
        /// Minimum gap between two identical sounds. Without it, a per-frame
        /// rule such as arrest progress would machine-gun the same clip.
        /// </summary>
        [SerializeField, Min(0f)]
        private float repeatSuppressionSeconds = 0.08f;

        private readonly Dictionary<GameSoundId, float> _lastPlayedAt = new();
        private readonly Dictionary<GameSoundId, int> _requestCounts = new();

        public static GameSoundService Instance => _instance;

        public int TotalRequests { get; private set; }
        public GameSoundId LastRequested { get; private set; }

        public void Configure(
            GameSoundBank configuredBank,
            AudioSource configuredOneShot,
            AudioSource configuredMusic)
        {
            bank = configuredBank;
            oneShotSource = configuredOneShot;
            musicSource = configuredMusic;
            _lastPlayedAt.Clear();
            _requestCounts.Clear();
            TotalRequests = 0;
            LastRequested = GameSoundId.None;
        }

        public int GetRequestCount(GameSoundId soundId)
        {
            return _requestCounts.TryGetValue(soundId, out int count)
                ? count
                : 0;
        }

        /// <summary>
        /// Where the fallback bank is looked up, relative to a Resources folder.
        ///
        /// The real asset lives at
        /// <c>Assets/_Project/Resources/Audio/GameSoundBank.asset</c>. It is the
        /// same asset the Game scene holds a reference to, not a copy, so the two
        /// paths cannot disagree about which clip a sound is.
        /// </summary>
        private const string FallbackBankResourcePath = "Audio/GameSoundBank";

        /// <summary>
        /// The bank found by name, for the scenes that have no service.
        ///
        /// Loaded once and kept. Null after a failed load is indistinguishable
        /// from not having looked, so the attempt is recorded separately —
        /// otherwise a missing asset means a Resources lookup on every button
        /// press.
        /// </summary>
        private static GameSoundBank _fallbackBank;
        private static bool _fallbackBankLoaded;

        /// <summary>
        /// How close something has to be to sound at full volume, and how far
        /// away before it cannot be heard at all.
        ///
        /// The town is about 60m across, so 55m means a theft at the far corner
        /// is inaudible and one on the next street is faint but there. The near
        /// radius is a whole city block: standing anywhere near the house being
        /// robbed should sound the same as standing in its doorway, because the
        /// officer's question is "which building", not "which room".
        /// </summary>
        public const float AudibleNearMeters = 12f;
        public const float AudibleFarMeters = 55f;

        /// <summary>
        /// Where the local player's ears are.
        ///
        /// The listener rather than the player: it is already attached to the
        /// camera that follows whoever is playing on this machine, so it is
        /// correct without asking which role this is, and it stays correct in
        /// the scenes and tests that have no player at all.
        /// </summary>
        private static AudioListener _listener;

        /// <summary>
        /// Raises a sound that happened somewhere, quieter the further away it
        /// was.
        ///
        /// For the thief's break-ins. The officer is meant to hear that a
        /// robbery is happening without being told where — a flat sound tells
        /// them the same thing wherever they stand, which makes the information
        /// free and the map irrelevant. Volume is the only cue that costs the
        /// thief something: rob the far side of town and the officer barely
        /// hears it.
        ///
        /// The position must already be a town coordinate. Interiors are rooms
        /// parked off the edge of the map, so a raw indoor position is tens of
        /// metres from everything and every theft indoors would be silent —
        /// call <c>InteriorAddress.TownPositionOf</c> first.
        /// </summary>
        public static void RequestAt(GameSoundId soundId, Vector3 townPosition)
        {
            float scale = DistanceScaleAt(townPosition);
            if (scale <= 0f)
            {
                return;
            }

            if (_instance != null)
            {
                _instance.Play(soundId, scale);
                return;
            }

            PlayWithoutAService(soundId);
        }

        /// <summary>
        /// 1 at the near radius, 0 at the far one, linear between. Returns 1
        /// when there is nobody listening yet, so a sound raised before the
        /// scene has a listener is heard rather than silently dropped.
        /// </summary>
        public static float DistanceScaleAt(Vector3 townPosition)
        {
            if (_listener == null)
            {
                _listener = Object.FindFirstObjectByType<AudioListener>();
            }

            if (_listener == null)
            {
                return 1f;
            }

            float distance = Vector3.Distance(
                _listener.transform.position,
                townPosition);
            if (distance <= AudibleNearMeters)
            {
                return 1f;
            }

            if (distance >= AudibleFarMeters)
            {
                return 0f;
            }

            return 1f - (distance - AudibleNearMeters)
                / (AudibleFarMeters - AudibleNearMeters);
        }

        /// <summary>
        /// Static entry point so rules can raise a sound without holding a
        /// reference.
        /// </summary>
        public static void Request(GameSoundId soundId)
        {
            if (_instance != null)
            {
                _instance.Play(soundId);
                return;
            }

            PlayWithoutAService(soundId);
        }

        /// <summary>
        /// Plays in a scene that has no <see cref="GameSoundService"/> in it.
        ///
        /// This service is built into the Game scene by <c>GreyboxMapSetup</c>, so
        /// until now every sound raised in Bootstrap or Result was dropped on the
        /// floor — the lobby handing out a role, the other player connecting, the
        /// voice model coming up. All of those happen where the service is not,
        /// and nothing said so, because a sound with no clip and a sound with no
        /// service both do exactly nothing.
        ///
        /// Goes through <see cref="PersistentOneShotAudio"/>, which already exists
        /// for the clips that outlive the scene that raised them, and which holds
        /// no scene references of its own.
        ///
        /// Public so a test can exercise it directly, the same way
        /// <c>GameSoundObserver.ResolveMatchEndSound</c> is. Play mode tests share
        /// one editor session and something earlier will have loaded the Game
        /// scene, so a test that waited for <see cref="Instance"/> to be null
        /// would be testing whatever ran before it.
        /// </summary>
        public static void PlayWithoutAService(GameSoundId soundId)
        {
            if (soundId == GameSoundId.None)
            {
                return;
            }

            if (!_fallbackBankLoaded)
            {
                _fallbackBankLoaded = true;
                _fallbackBank =
                    Resources.Load<GameSoundBank>(FallbackBankResourcePath);
                if (_fallbackBank == null)
                {
                    // Said once. A bank that moved out of Resources takes every
                    // sound outside the Game scene with it and leaves no trace.
                    Debug.LogWarning(
                        "[AUDIO-001] No sound bank at Resources/"
                        + $"{FallbackBankResourcePath}. Scenes without a "
                        + "GameSoundService stay silent.");
                }
            }

            if (_fallbackBank == null)
            {
                return;
            }

            PersistentOneShotAudio.Play(
                _fallbackBank.Resolve(soundId),
                _fallbackBank.GetVolume(soundId));
        }

        public void Play(GameSoundId soundId)
        {
            Play(soundId, 1f);
        }

        /// <summary>
        /// <paramref name="volumeScale"/> multiplies the bank's volume for this
        /// one play. The bank still decides how loud the sound is relative to
        /// every other sound; this only says how far away it happened.
        /// </summary>
        public void Play(GameSoundId soundId, float volumeScale)
        {
            if (soundId == GameSoundId.None)
            {
                return;
            }

            TotalRequests++;
            LastRequested = soundId;
            _requestCounts[soundId] =
                GetRequestCount(soundId) + 1;

            float now = Time.unscaledTime;
            if (_lastPlayedAt.TryGetValue(soundId, out float last)
                && now - last < repeatSuppressionSeconds)
            {
                return;
            }

            _lastPlayedAt[soundId] = now;

            if (bank == null || oneShotSource == null)
            {
                return;
            }

            AudioClip clip = bank.Resolve(soundId);
            if (clip == null)
            {
                return;
            }

            // The match-end stingers cannot play from this object: it lives in the
            // Game scene, and the Result scene load that follows the win destroys it
            // a few frames into the clip (`ISSUE-070`).
            if (GameSoundBank.OutlivesTheScene(soundId))
            {
                PersistentOneShotAudio.Play(
                    clip,
                    bank.GetVolume(soundId) * volumeScale);
                return;
            }

            oneShotSource.PlayOneShot(
                clip,
                bank.GetVolume(soundId) * volumeScale);
        }

        /// <summary>
        /// AUDIO-003. Ducks the music so a spoken command is not competing with
        /// the background track. Voice input is not implemented yet, but the
        /// hook exists so the mix does not have to be redesigned later.
        /// </summary>
        public void SetVoiceCaptureActive(bool active)
        {
            if (musicSource == null || bank == null)
            {
                return;
            }

            musicSource.volume = active
                ? bank.MusicVolume * bank.VoiceDuckingMultiplier
                : bank.MusicVolume;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (musicSource != null && bank != null)
            {
                musicSource.volume = bank.MusicVolume;
                musicSource.loop = true;
                if (bank.MusicTrack != null)
                {
                    musicSource.clip = bank.MusicTrack;
                    musicSource.Play();
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetInstance()
        {
            _instance = null;

            // The cached bank goes with it. With domain reloading switched off,
            // a reference kept from the previous play session survives into one
            // where Resources has already unloaded what it pointed at.
            _fallbackBank = null;
            _fallbackBankLoaded = false;
        }
    }
}
