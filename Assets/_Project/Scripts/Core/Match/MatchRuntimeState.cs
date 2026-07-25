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
        private bool _timerExpiredRaised;

        public event Action<MatchStateChanged> StateChanged
        {
            add => _stateMachine.StateChanged += value;
            remove => _stateMachine.StateChanged -= value;
        }

        public event Action TimerExpired;

        public MatchState CurrentState => _stateMachine.CurrentState;
        public bool IsGameplayActive => _stateMachine.IsGameplayActive;
        public bool IsCountdownActive => _countdownActive;
        public float ReadyCountdownRemainingSeconds { get; private set; }
        public float RemainingMatchSeconds { get; private set; }

        public void Configure(
            MatchConfig configuredMatchConfig,
            bool shouldStartCountdownAutomatically)
        {
            matchConfig = configuredMatchConfig;
            startCountdownAutomatically =
                shouldStartCountdownAutomatically;
            if (matchConfig != null)
            {
                RemainingMatchSeconds =
                    matchConfig.MatchDurationSeconds;
            }

            _timerExpiredRaised = false;
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
            ResetMatchTimer();
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
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (_countdownActive && CurrentState == MatchState.Ready)
            {
                ReadyCountdownRemainingSeconds = Mathf.Max(
                    0f,
                    ReadyCountdownRemainingSeconds - safeDeltaTime);
                if (ReadyCountdownRemainingSeconds <= 0f)
                {
                    _countdownActive = false;
                    _stateMachine.TryTransitionTo(MatchState.Playing);
                }

                return;
            }

            if (CurrentState != MatchState.Playing)
            {
                return;
            }

            RemainingMatchSeconds = Mathf.Max(
                0f,
                RemainingMatchSeconds - safeDeltaTime);
            if (RemainingMatchSeconds <= 0f
                && !_timerExpiredRaised)
            {
                _timerExpiredRaised = true;
                TimerExpired?.Invoke();
            }
        }

        public bool ResetMatchTimer()
        {
            if (matchConfig == null
                || CurrentState == MatchState.Playing)
            {
                return false;
            }

            matchConfig.ValidateOrThrow();
            RemainingMatchSeconds =
                matchConfig.MatchDurationSeconds;
            _timerExpiredRaised = false;
            return true;
        }

        private void Awake()
        {
            if (matchConfig == null)
            {
                throw new InvalidOperationException(
                    "MatchRuntimeState requires a MatchConfig.");
            }

            ResetMatchTimer();
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
