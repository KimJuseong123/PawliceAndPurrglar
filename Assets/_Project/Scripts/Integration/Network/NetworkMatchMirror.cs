using PawsAndLoot.Match;
using Unity.Netcode;
using UnityEngine;

namespace PawsAndLoot.Integration.Network
{
    /// <summary>
    /// NET-004. The host owns the match state and the clock; clients mirror it.
    ///
    /// Only the server writes these variables, so a client cannot start, end or
    /// re-time a match. The client's own <see cref="MatchRuntimeState"/> stops
    /// ticking and is driven from the replicated values instead, which is what
    /// makes both screens show the same remaining time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkMatchMirror : NetworkBehaviour
    {
        private readonly NetworkVariable<int> _state =
            new(
                (int)MatchState.Lobby,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _remainingSeconds =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _countdownSeconds =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        /// <summary>
        /// The decided result, sent rather than recomputed.
        ///
        /// Both machines used to work this out for themselves and happened to
        /// agree, because one arrest ended the match and the arrest itself
        /// replicates. With three arrests they stopped agreeing: the client
        /// counts catches from replicated arrest progress but the release that
        /// re-arms the next one is a host-side timer, so its count stalls and
        /// the match never ends on its screen. The thief was told NO MATCH
        /// RESULT while the host showed a winner.
        ///
        /// Two machines independently reaching the same verdict is not a rule
        /// this project makes anywhere else, and this is why.
        /// </summary>
        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private MatchResultEvaluator evaluator;

        private bool _appliedRemoteResult;

        public MatchState ReplicatedState => (MatchState)_state.Value;
        public float ReplicatedRemainingSeconds => _remainingSeconds.Value;
        public float ReplicatedCountdownSeconds => _countdownSeconds.Value;

        public void Configure(
            MatchRuntimeState runtime,
            MatchResultEvaluator configuredEvaluator = null)
        {
            matchRuntime = runtime;
            evaluator = configuredEvaluator;
        }

        private void Awake()
        {
            if (matchRuntime == null)
            {
                matchRuntime = FindFirstObjectByType<MatchRuntimeState>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (matchRuntime == null)
            {
                matchRuntime = FindFirstObjectByType<MatchRuntimeState>();
            }

            if (matchRuntime == null)
            {
                return;
            }

            // A client must not simulate the match at all, otherwise two clocks
            // drift apart and the two screens disagree.
            matchRuntime.SetRemoteControlled(!IsServer);

            if (evaluator == null)
            {
                evaluator = FindFirstObjectByType<MatchResultEvaluator>();
            }

            evaluator?.SetAuthority(IsServer);

            // Published the instant it is decided, not on the next Update.
            //
            // This component lives in the match scene, and deciding a winner
            // starts that scene unloading. Waiting for the next frame is a race
            // against the mirror's own destruction: the host would reach the
            // result screen while the client sat in Playing forever, which is
            // exactly the "NO MATCH RESULT" the replication was added to fix.

        }

        private void Update()
        {
            if (matchRuntime == null || !IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                _state.Value = (int)matchRuntime.CurrentState;
                _remainingSeconds.Value =
                    matchRuntime.RemainingMatchSeconds;
                _countdownSeconds.Value =
                    matchRuntime.ReadyCountdownRemainingSeconds;
                return;
            }

            matchRuntime.ApplyRemoteState(
                (MatchState)_state.Value,
                _remainingSeconds.Value,
                _countdownSeconds.Value);
        }
    }
}
