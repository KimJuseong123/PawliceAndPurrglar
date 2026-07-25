using System;
using UnityEngine;

namespace PawsAndLoot.Match
{
    public sealed class MatchRuntimeState : MonoBehaviour, IMatchStateReader
    {
        [SerializeField]
        private bool startPlayingImmediately = true;

        private readonly MatchStateMachine _stateMachine = new();

        public event Action<MatchStateChanged> StateChanged
        {
            add => _stateMachine.StateChanged += value;
            remove => _stateMachine.StateChanged -= value;
        }

        public MatchState CurrentState => _stateMachine.CurrentState;
        public bool IsGameplayActive => _stateMachine.IsGameplayActive;

        public void Configure(bool shouldStartPlayingImmediately)
        {
            startPlayingImmediately = shouldStartPlayingImmediately;
        }

        public bool TryTransitionTo(MatchState nextState)
        {
            return _stateMachine.TryTransitionTo(nextState);
        }

        private void Awake()
        {
            if (!startPlayingImmediately)
            {
                return;
            }

            _stateMachine.TryTransitionTo(MatchState.Ready);
            _stateMachine.TryTransitionTo(MatchState.Playing);
        }
    }
}
