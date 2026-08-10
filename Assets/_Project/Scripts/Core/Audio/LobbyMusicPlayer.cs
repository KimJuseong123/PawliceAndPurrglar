using PawliceAndPurrglar.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawliceAndPurrglar.Audio
{
    /// <summary>
    /// Looping music for the lobby, and silence once a match starts.
    ///
    /// `GameSoundBank.MusicTrack` and `GameSoundService`'s music source already
    /// existed, but that service is built into the **Game** scene by
    /// `GreyboxMapSetup` — so it cannot play anything in Bootstrap, which is where
    /// the lobby lives. That is why the lobby has been silent despite a music
    /// slot being wired since `AUDIO-003`.
    ///
    /// Loads the clip from `Resources` rather than a serialized reference so the
    /// Bootstrap scene needs no rebuild, and stops itself on leaving the lobby:
    /// carrying lobby music into a match would fight the match audio and there is
    /// no crossfade to arbitrate (`docs/16` D).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyMusicPlayer : MonoBehaviour
    {
        private const string ClipResourcePath = "Audio/bgm_lobby";

        /// <summary>
        /// Well below the effects. Music that competes with a voice command or a
        /// door is a mixing problem the game cannot solve later.
        /// </summary>
        private const float Volume = 0.35f;

        private static LobbyMusicPlayer _instance;

        private AudioSource _source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject("Lobby Music");
            DontDestroyOnLoad(host);
            host.AddComponent<LobbyMusicPlayer>();
        }

        private void Awake()
        {
            _instance = this;

            var clip = Resources.Load<AudioClip>(ClipResourcePath);
            if (clip == null)
            {
                // Said once, not silently skipped: a missing music file and a
                // muted mixer look identical from the player's chair.
                Debug.LogWarning(
                    $"[AUDIO] No lobby music at Resources/{ClipResourcePath}. "
                    + "The lobby stays silent.");
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = Volume;
            _source.spatialBlend = 0f;

            SceneManager.activeSceneChanged += HandleSceneChanged;
            ApplyForScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleSceneChanged;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HandleSceneChanged(Scene _, Scene next)
        {
            ApplyForScene(next);
        }

        /// <summary>
        /// Plays in Bootstrap and Result, stops in the match.
        ///
        /// Result is included because the lobby track is where a rematch returns
        /// to, and cutting to silence on the result screen reads as the game
        /// having ended badly rather than having ended.
        /// </summary>
        private void ApplyForScene(Scene scene)
        {
            if (_source == null)
            {
                return;
            }

            bool wanted = scene.name == GameSceneCatalog.GetName(GameSceneId.Bootstrap)
                || scene.name == GameSceneCatalog.GetName(GameSceneId.Result);

            if (wanted && !_source.isPlaying)
            {
                _source.Play();
            }
            else if (!wanted && _source.isPlaying)
            {
                _source.Stop();
            }
        }
    }
}
