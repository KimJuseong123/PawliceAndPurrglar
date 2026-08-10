using UnityEngine;

namespace PawliceAndPurrglar.Audio
{
    /// <summary>
    /// One AudioSource that survives a scene load, for the sounds that are raised
    /// *because* the scene is about to change.
    ///
    /// The match-end stingers are the whole reason this exists. `GameSoundService`
    /// and its AudioSource are built into the Game scene by `GreyboxMapSetup`, and
    /// the Result scene loads within a few frames of the victory clip starting — so
    /// the object playing it was destroyed part-way through and a two-second fanfare
    /// came out as a click. The clip was never the problem; nothing about it was
    /// short (`ISSUE-070`).
    ///
    /// Deliberately holds no scene references. That is what makes it safe to keep
    /// across a load: it cannot go stale, and a second match cannot leave an older
    /// copy wired to a dead observer — the failure mode that made a stale
    /// `NetworkManager` so hard to find (`ISSUE-052`).
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class PersistentOneShotAudio : MonoBehaviour
    {
        private static PersistentOneShotAudio _instance;

        private AudioSource _source;

        internal static void Play(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                return;
            }

            PersistentOneShotAudio player = Resolve();
            if (player == null || player._source == null)
            {
                return;
            }

            player._source.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Creates the player on first use rather than at startup, so a build that
        /// never finishes a match never makes it.
        /// </summary>
        private static PersistentOneShotAudio Resolve()
        {
            // `!= null` rather than `is not null`: a destroyed MonoBehaviour still
            // has a live C# reference, and only Unity's operator reports it gone.
            if (_instance != null)
            {
                return _instance;
            }

            var host = new GameObject("Persistent One Shot Audio");
            DontDestroyOnLoad(host);
            return host.AddComponent<PersistentOneShotAudio>();
        }

        private void Awake()
        {
            _instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            // 2D. The result is not somewhere in the world, and a spatialised
            // stinger would fade with wherever the listener happened to stop.
            _source.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
        }
    }
}
