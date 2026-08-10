namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// The stable command vocabulary. Number keys today and voice later must
    /// produce these same values, so the animal state machines never compare
    /// natural language strings.
    /// </summary>
    public enum CompanionCommandId
    {
        None = 0,

        // Police dog.
        Track = 1,
        Search = 2,
        Guard = 3,
        Bark = 4,

        // Thief cat.
        Scout = 11,
        Distract = 12,
        Steal = 13,
        Hide = 14,

        // Safety commands are shared by both animals and must remain usable
        // when the external voice service is unavailable.
        Stop = 20,
        FollowOwner = 21,
        Stay = 22,
        ReturnOwner = 23,
        Cancel = 24
    }
}
