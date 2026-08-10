namespace PawliceAndPurrglar.Match
{
    public readonly struct MatchStateChanged
    {
        public MatchStateChanged(
            MatchState previousState,
            MatchState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public MatchState PreviousState { get; }
        public MatchState CurrentState { get; }
    }
}
