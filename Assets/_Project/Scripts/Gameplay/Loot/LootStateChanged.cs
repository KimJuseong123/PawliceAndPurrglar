namespace PawsAndLoot.Gameplay.Loot
{
    public readonly struct LootStateChanged
    {
        public LootStateChanged(
            LootState previousState,
            LootState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public LootState PreviousState { get; }
        public LootState CurrentState { get; }
    }
}
