namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Why a command was refused. Surfaced to the UI so a failed order reads as
    /// a reason rather than as nothing happening.
    /// </summary>
    public enum CompanionCommandRejection
    {
        None = 0,
        MatchNotPlaying = 1,
        WrongFaction = 2,
        CompanionMissing = 3,
        CompanionDisabled = 4,
        OnCooldown = 5,
        UnknownCommand = 6,
        TargetMissing = 7,
        TargetUnreachable = 8
    }
}
