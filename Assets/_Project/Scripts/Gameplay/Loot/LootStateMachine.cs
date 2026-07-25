using System;
using PawsAndLoot.Logging;

namespace PawsAndLoot.Gameplay.Loot
{
    public sealed class LootStateMachine
    {
        private LootState _currentState;

        public LootStateMachine(
            LootState initialState = LootState.Available)
        {
            if (!Enum.IsDefined(typeof(LootState), initialState))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialState),
                    initialState,
                    "Unknown loot state.");
            }

            _currentState = initialState;
        }

        public event Action<LootStateChanged> StateChanged;

        public LootState CurrentState => _currentState;
        public bool IsTerminal => _currentState == LootState.Sold;

        public bool CanTransitionTo(LootState nextState)
        {
            if (!Enum.IsDefined(typeof(LootState), nextState)
                || nextState == _currentState
                || IsTerminal)
            {
                return false;
            }

            return (_currentState, nextState) switch
            {
                (LootState.Available, LootState.Reserved) => true,
                (LootState.Dropped, LootState.Reserved) => true,
                (LootState.Hidden, LootState.Reserved) => true,
                (LootState.Reserved, LootState.Carried) => true,
                (LootState.Carried, LootState.Dropped) => true,
                (LootState.Carried, LootState.Hidden) => true,
                (LootState.Carried, LootState.Sold) => true,
                _ => false
            };
        }

        public bool TryTransitionTo(LootState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            LootState previousState = _currentState;
            _currentState = nextState;
            GameLogger.Info(
                GameLogCategory.Loot,
                $"Loot state changed: {previousState} -> {nextState}.");
            StateChanged?.Invoke(
                new LootStateChanged(previousState, nextState));
            return true;
        }
    }
}
