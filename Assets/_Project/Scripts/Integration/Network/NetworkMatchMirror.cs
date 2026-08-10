using PawliceAndPurrglar.Match;
using Unity.Netcode;
using UnityEngine;

namespace PawliceAndPurrglar.Integration.Network
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

        /// <summary>
        /// Which of the black market places are open this match, one bit each.
        ///
        /// Replicated because **being switched off is not a thing that
        /// replicates.** Where something is does — the loot pieces are moved by
        /// the host and their positions arrive on the client by themselves — but
        /// whether an object is active is a local fact. Two machines each drawing
        /// two bins from five would show the thief a market the host had never
        /// opened.
        ///
        /// Carried here rather than in a message of its own because this is
        /// already the one thing in the match scene that the server writes and
        /// everybody reads. A second channel would be a second thing to keep in
        /// step.
        ///
        /// A mask rather than two indices, so "none chosen yet" is zero and needs
        /// no sentinel.
        /// </summary>
        private readonly NetworkVariable<int> _openMarkets =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public int OpenMarkets => _openMarkets.Value;

        /// <summary>
        /// The number every random draw in the match starts from, rolled once
        /// by the host.
        ///
        /// The market mask above is the choice itself; this is the choice's
        /// *input*, and it is here for the draws whose result is too large to
        /// send. A room's layout is seven positions per room across thirteen
        /// rooms, and a message carrying that is a format to keep in step
        /// forever. One integer covers every draw there is and every draw added
        /// later.
        ///
        /// Zero means the host has not rolled yet — a drawer waits rather than
        /// running on zero, which would be the same layout every match. It goes
        /// back to zero when the match ends, so a rematch is not the match both
        /// players have just learned.
        /// </summary>
        private readonly NetworkVariable<int> _matchSeed =
            new(
                0,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        public int MatchSeed => _matchSeed.Value;

        /// <summary>
        /// Told by the host's draw. Ignored anywhere else — a client that wrote
        /// here would be refused by the write permission, loudly and every
        /// frame.
        /// </summary>
        public void SetOpenMarkets(int mask)
        {
            if (IsServer)
            {
                _openMarkets.Value = mask;
            }
        }

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

            // The animals are scene objects with no link of their own, so their
            // authority is set from the one component that knows whether this
            // machine is the host.
            foreach (PawliceAndPurrglar.Companions.CompanionLure lure in
                FindObjectsByType<PawliceAndPurrglar.Companions.CompanionLure>(
                    FindObjectsSortMode.None))
            {
                lure.SetAuthority(IsServer);
            }

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
                PublishMatchSeed();
                return;
            }

            matchRuntime.ApplyRemoteState(
                (MatchState)_state.Value,
                _remainingSeconds.Value,
                _countdownSeconds.Value);
        }

        /// <summary>
        /// Rolled on the first frame of a match and cleared when it ends.
        ///
        /// Rolled here rather than by whoever needs it, because there are
        /// thirteen rooms that need it and they must all get the same one. This
        /// component is already the single thing in the match scene the server
        /// writes and everybody reads, so it is the only place that can say
        /// "once".
        ///
        /// Never zero: zero is the value a drawer treats as "not told yet", and
        /// a roll that landed on it would leave every room waiting for the whole
        /// match.
        /// </summary>
        private void PublishMatchSeed()
        {
            if (matchRuntime.CurrentState != MatchState.Playing)
            {
                _matchSeed.Value = 0;
                return;
            }

            if (_matchSeed.Value == 0)
            {
                _matchSeed.Value = Random.Range(1, int.MaxValue);
            }
        }
    }
}
