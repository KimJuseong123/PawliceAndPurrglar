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

        /// <summary>
        /// The configured length of a match. Exposed so the result screen can
        /// report how long the match ran rather than how long was left.
        /// </summary>
        public float MatchDurationSeconds =>
            matchConfig != null ? matchConfig.MatchDurationSeconds : 0f;

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

        /// <summary>
        /// True when another machine owns this match. A remote-controlled state
        /// stops simulating so two clocks cannot drift apart; it only reflects
        /// what the authority sends.
        /// </summary>
        public bool IsRemoteControlled { get; private set; }

        public void SetRemoteControlled(bool remoteControlled)
        {
            IsRemoteControlled = remoteControlled;
        }

        /// <summary>
        /// Applies authority state. Transitions go through the state machine so
        /// the legal-transition rules still hold, and a skipped step is walked
        /// rather than jumped, because the machine rejects jumps.
        /// </summary>
        public void ApplyRemoteState(
            MatchState state,
            float remainingSeconds,
            float countdownSeconds)
        {
            RemainingMatchSeconds = Mathf.Max(0f, remainingSeconds);
            ReadyCountdownRemainingSeconds =
                Mathf.Max(0f, countdownSeconds);
            _countdownActive = state == MatchState.Ready
                && countdownSeconds > 0f;

            // Walk forward one legal step at a time until the authority state
            // is reached, so no listener misses a transition.
            for (int guard = 0;
                guard < 8 && CurrentState != state;
                guard++)
            {
                if (!_stateMachine.TryTransitionTo(state)
                    && !TryAdvanceTowards(state))
                {
                    break;
                }
            }
        }

        private bool TryAdvanceTowards(MatchState target)
        {
            MatchState next = CurrentState switch
            {
                MatchState.Lobby => MatchState.Ready,
                MatchState.Ready => MatchState.Playing,
                MatchState.Playing => MatchState.Ending,
                MatchState.Ending => MatchState.Result,
                _ => CurrentState
            };

            return next != CurrentState
                && (int)next <= (int)target
                && _stateMachine.TryTransitionTo(next);
        }

        public void Tick(float deltaTime)
        {
            if (IsRemoteControlled)
            {
                return;
            }

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
