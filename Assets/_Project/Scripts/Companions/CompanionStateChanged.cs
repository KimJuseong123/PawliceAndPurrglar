namespace PawsAndLoot.Companions
{
    public readonly struct CompanionStateChanged
    {
        public CompanionStateChanged(
            CompanionState previousState,
            CompanionState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public CompanionState PreviousState { get; }
        public CompanionState CurrentState { get; }
    }
}
