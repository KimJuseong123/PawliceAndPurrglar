using System;
using PawliceAndPurrglar.Logging;

namespace PawliceAndPurrglar.Match
{
    public sealed class MatchStateMachine : IMatchStateReader
    {
        private MatchState _currentState = MatchState.Lobby;

        public event Action<MatchStateChanged> StateChanged;

        public MatchState CurrentState => _currentState;

        public bool IsGameplayActive =>
            _currentState == MatchState.Playing;

        public bool CanTransitionTo(MatchState nextState)
        {
            if (!Enum.IsDefined(typeof(MatchState), nextState)
                || nextState == _currentState)
            {
                return false;
            }

            return (_currentState, nextState) switch
            {
                (MatchState.Lobby, MatchState.Ready) => true,
                (MatchState.Ready, MatchState.Playing) => true,
                (MatchState.Playing, MatchState.Ending) => true,
                (MatchState.Ending, MatchState.Result) => true,
                _ => false
            };
        }

        public bool TryTransitionTo(MatchState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            MatchState previousState = _currentState;
            _currentState = nextState;

            GameLogger.Info(
                GameLogCategory.Match,
                $"Match state changed: {previousState} -> {nextState}.");
            StateChanged?.Invoke(
                new MatchStateChanged(previousState, nextState));
            return true;
        }
    }
}
