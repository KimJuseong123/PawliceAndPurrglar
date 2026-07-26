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
        /// Static entry point so rules can raise a sound without holding a
        /// reference. Silently does nothing when no service exists, which is the
        /// case in most unit tests.
        /// </summary>
        public static void Request(GameSoundId soundId)
        {
            if (_instance != null)
            {
                _instance.Play(soundId);
            }
        }

        public void Play(GameSoundId soundId)
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

            oneShotSource.PlayOneShot(clip, bank.GetVolume(soundId));
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
        }
    }
}
