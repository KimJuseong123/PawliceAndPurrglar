namespace PawsAndLoot.Match
{
    public interface IMatchStateReader
    {
        MatchState CurrentState { get; }
        bool IsGameplayActive { get; }
    }
}
