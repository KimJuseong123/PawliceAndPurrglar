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
        /// <summary>
        /// Whether a result exists at all, kept separate from the winner.
        ///
        /// <see cref="MatchWinner"/> has no "nobody" and Police is zero, so a
        /// freshly spawned variable reads as a police victory. A client would
        /// have adopted one the instant it connected.
        /// </summary>
        private readonly NetworkVariable<bool> _resultDecided =
            new(
                false,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _resultWinner =
            new(
                (int)MatchWinner.Police,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _resultReason =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> _resultSoldAmount =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _resultRemainingSeconds =
            new(
                0f,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        [SerializeField]
        private MatchResultEvaluator evaluator;

        private bool _appliedRemoteResult;

        public MatchState ReplicatedState => (MatchState)_state.Value;
        public float ReplicatedRemainingSeconds => _remainingSeconds.Value;
        public float ReplicatedCountdownSeconds => _countdownSeconds.Value;

        public bool HasReplicatedResult => _resultDecided.Value;

        public MatchResult ReplicatedResult => new(
            (MatchWinner)_resultWinner.Value,
            (MatchEndReason)_resultReason.Value,
            _resultSoldAmount.Value,
            _resultRemainingSeconds.Value);

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
            if (IsServer && evaluator != null)
            {
                evaluator.ResultDecided += PublishDecided;
            }
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
                PublishResult();
                return;
            }

            matchRuntime.ApplyRemoteState(
                (MatchState)_state.Value,
                _remainingSeconds.Value,
                _countdownSeconds.Value);
            ApplyRemoteResult();
        }
        public override void OnNetworkDespawn()
        {
            if (evaluator != null)
            {
                evaluator.ResultDecided -= PublishDecided;
            }
        }

        private void PublishDecided(MatchResult result)
        {
            _resultDecided.Value = true;
            _resultWinner.Value = (int)result.Winner;
            _resultReason.Value = (int)result.Reason;
            _resultSoldAmount.Value = result.SoldAmount;
            _resultRemainingSeconds.Value = result.RemainingSeconds;

            // The state goes with it. A client that has the verdict but is
            // still told the match is Playing will not run its ending.
            _state.Value = (int)matchRuntime.CurrentState;
        }

        private void PublishResult()
        {
            if (evaluator == null || !evaluator.HasResult)
            {
                return;
            }

            MatchResult result = evaluator.CurrentResult;
            _resultDecided.Value = true;
            _resultWinner.Value = (int)result.Winner;
            _resultReason.Value = (int)result.Reason;
            _resultSoldAmount.Value = result.SoldAmount;
            _resultRemainingSeconds.Value = result.RemainingSeconds;
        }

        /// <summary>
        /// Hands the host's verdict to the client's own evaluator, which then
        /// runs the same ending it would have run for a locally decided match.
        /// Reusing that path means the result screen, the rematch and the sound
        /// all keep one route rather than gaining a networked special case.
        /// </summary>
        private void ApplyRemoteResult()
        {
            if (_appliedRemoteResult
                || evaluator == null
                || !HasReplicatedResult)
            {
                return;
            }

            _appliedRemoteResult = true;
            evaluator.AdoptDecidedResult(ReplicatedResult);
        }
    }
}
