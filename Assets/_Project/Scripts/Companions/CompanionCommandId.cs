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

        /// <summary>
        /// CAT-010. The cat runs at the officer and holds them still for a
        /// moment. The thief's only way to touch the police directly.
        ///
        /// Deliberately the cat's alone. The dog biting would overlap the arrest
        /// it already performs, and `CoreCommandMatcher` refuses "물어" from the
        /// police on purpose.
        /// </summary>
        Bite = 15,

        // Safety commands are shared by both animals and must remain usable
        // when the external voice service is unavailable.
        Stop = 20,
        FollowOwner = 21,
        Stay = 22,
        ReturnOwner = 23,
        Cancel = 24
    }
}
