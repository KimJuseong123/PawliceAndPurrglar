namespace PawsAndLoot.Audio
{
    /// <summary>
    /// AUDIO-001. The complete sound vocabulary.
    ///
    /// Rules raise these by name and never touch an AudioSource, so a missing
    /// clip, a muted mixer or a removed audio system cannot change what
    /// happens in a match.
    /// </summary>
    public enum GameSoundId
    {
        None = 0,
        CommandSucceeded = 1,
        CommandFailed = 2,
        LootAcquired = 3,
        LootSold = 4,
        ArrestStarted = 5,
        ArrestCompleted = 6,
        DogBark = 7,
        CatMeow = 8,
        Victory = 9,
        Defeat = 10
    }
}
