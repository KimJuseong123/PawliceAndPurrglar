namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// Why a player is standing still.
    ///
    /// The rules do not read this and must not start: every cause takes movement
    /// away for however long <see cref="StunState"/> was told, and a banana that
    /// held for less than a rock would be a balance change hiding inside a
    /// presentation change. This exists so the *screen* can tell the three apart,
    /// because they read as three different accidents and were all being drawn as
    /// four spinning stars.
    ///
    /// Replicated with the stun itself. A cause that only existed on the host
    /// would spin the character on one screen and star them on the other.
    /// </summary>
    public enum StunCause
    {
        /// <summary>Something hit you. A rock, and the default for anything new.</summary>
        Impact = 0,

        /// <summary>You stood on a banana and went over. Drawn as a spin.</summary>
        Slip = 1,

        /// <summary>You are stuck to the floor. The glue trap, drawn on the screen edge.</summary>
        Stuck = 2
    }
}
