namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Where a command came from. Recorded for feedback and logging only; the
    /// validator and the animal AI must behave identically whatever the source,
    /// which is what lets voice replace keys later without touching rules.
    /// </summary>
    public enum CompanionCommandInputSource
    {
        Keyboard = 0,
        Button = 1,
        Voice = 2,
        Network = 3
    }
}
