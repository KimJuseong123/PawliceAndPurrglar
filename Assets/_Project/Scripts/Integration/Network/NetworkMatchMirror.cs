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

        [SerializeField]
        private MatchRuntimeState matchRuntime;

        public MatchState ReplicatedState => (MatchState)_state.Value;
        public float ReplicatedRemainingSeconds => _remainingSeconds.Value;
        public float ReplicatedCountdownSeconds => _countdownSeconds.Value;

        public void Configure(MatchRuntimeState runtime)
        {
            matchRuntime = runtime;
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
