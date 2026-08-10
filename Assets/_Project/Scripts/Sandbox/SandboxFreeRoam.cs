using PawliceAndPurrglar.Match;
using UnityEngine;

namespace PawliceAndPurrglar.Sandbox
{
    /// <summary>
    /// Puts the map-test scene into a state you can walk around in.
    ///
    /// The movement motor refuses to move unless the match says gameplay is active —
    /// which is right for the game and wrong for a scene whose entire purpose is
    /// walking around looking at buildings. There is no lobby here, no roles handed
    /// out and no opponent, so something has to say "playing" once at startup.
    ///
    /// Deliberately the only runtime code the sandbox adds. Everything else it uses is
    /// the real thing: the same motor, the same controller, the same camera, so what
    /// is being tested is the map and not a special walking mode built to flatter it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxFreeRoam : MonoBehaviour
    {
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        public void Configure(MatchRuntimeState runtime)
        {
            matchRuntime = runtime;
        }

        private float _waited;
        private bool _reported;

        /// <summary>
        /// Says so if the match never starts, once. A sandbox that silently refuses to
        /// let you walk is the failure this exists to make visible.
        /// </summary>
        private void Update()
        {
            if (_reported || matchRuntime == null)
            {
                return;
            }

            if (matchRuntime.CurrentState == MatchState.Playing)
            {
                _reported = true;
                Debug.Log("[SANDBOX] Match is playing; you can walk.");
                return;
            }

            _waited += Time.unscaledDeltaTime;
            if (_waited > 6f)
            {
                _reported = true;
                Debug.LogError(
                    "[SANDBOX] The match never started; it is still "
                    + $"{matchRuntime.CurrentState}, so nothing will move.");
            }
        }

        private void Start()
        {
            if (matchRuntime == null)
            {
                matchRuntime =
                    FindFirstObjectByType<MatchRuntimeState>();
            }

            if (matchRuntime == null)
            {
                Debug.LogError(
                    "[SANDBOX] No match state in the scene, so the player "
                    + "cannot move.");
                return;
            }

            // Nothing is forced here.
            //
            // Asking for Playing from Lobby was refused, and rightly: the state
            // machine goes Lobby then Countdown then Playing, and jumping the queue
            // is exactly the kind of shortcut that makes a sandbox behave unlike the
            // game. The scene is built with the automatic countdown switched on, so
            // the match starts itself; this only reports if it has not.
            _waited = 0f;
        }
    }
}
