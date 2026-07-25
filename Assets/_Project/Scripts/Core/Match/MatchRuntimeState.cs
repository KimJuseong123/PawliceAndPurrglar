using System;
using PawsAndLoot.Config;
using UnityEngine;

namespace PawsAndLoot.Match
{
    public sealed class MatchRuntimeState : MonoBehaviour, IMatchStateReader
    {
        [SerializeField]
        private MatchConfig matchConfig;

        [SerializeField]
        private bool startCountdownAutomatically = true;

        private readonly MatchStateMachine _stateMachine = new();
        private bool _countdownActive;

        public event Action<MatchStateChanged> StateChanged
        {
            add => _stateMachine.StateChanged += value;
            remove => _stateMachine.StateChanged -= value;
        }

        public MatchState CurrentState => _stateMachine.CurrentState;
        public bool IsGameplayActive => _stateMachine.IsGameplayActive;
        public bool IsCountdownActive => _countdownActive;
        public float ReadyCountdownRemainingSeconds { get; private set; }

        public void Configure(
            MatchConfig configuredMatchConfig,
            bool shouldStartCountdownAutomatically)
        {
            matchConfig = configuredMatchConfig;
            startCountdownAutomatically =
                shouldStartCountdownAutomatically;
        }

        public bool TryTransitionTo(MatchState nextState)
        {
            return _stateMachine.TryTransitionTo(nextState);
        }

        public bool BeginCountdown()
        {
            if (_countdownActive
                || matchConfig == null
                || CurrentState != MatchState.Lobby)
            {
                return false;
            }

            matchConfig.ValidateOrThrow();
            if (!_stateMachine.TryTransitionTo(MatchState.Ready))
            {
                return false;
            }

            ReadyCountdownRemainingSeconds =
                matchConfig.ReadyCountdownSeconds;
            _countdownActive = true;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_countdownActive || CurrentState != MatchState.Ready)
            {
                return;
            }

            ReadyCountdownRemainingSeconds = Mathf.Max(
                0f,
                ReadyCountdownRemainingSeconds
                    - Mathf.Max(0f, deltaTime));
            if (ReadyCountdownRemainingSeconds > 0f)
            {
                return;
            }

            _countdownActive = false;
            _stateMachine.TryTransitionTo(MatchState.Playing);
        }

        private void Awake()
        {
            if (matchConfig == null)
            {
                throw new InvalidOperationException(
                    "MatchRuntimeState requires a MatchConfig.");
            }

            if (startCountdownAutomatically)
            {
                BeginCountdown();
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }
    }
}
