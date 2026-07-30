using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Sandbox
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

            // Through the state machine, not around it. Writing the field directly
            // would skip the transition rules the rest of the game relies on, and a
            // sandbox that behaves differently from the game is not a test of the
            // game.
            if (matchRuntime.CurrentState != MatchState.Playing
                && !matchRuntime.TryTransitionTo(MatchState.Playing))
            {
                Debug.LogError(
                    "[SANDBOX] The match refused to start from "
                    + $"{matchRuntime.CurrentState}.");
            }
        }
    }
}
