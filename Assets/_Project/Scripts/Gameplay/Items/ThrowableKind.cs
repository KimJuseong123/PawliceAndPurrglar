namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// The prop kinds a player can pick up and use.
    ///
    /// Deliberately few. Each one has to teach a different lesson: the rock
    /// punishes being seen, the banana punishes running a straight line. Adding
    /// a third that does neither would just be more inventory.
    /// </summary>
    public enum ThrowableKind
    {
        /// <summary>
        /// Thrown at a player. Stuns on contact.
        /// </summary>
        Rock = 0,

        /// <summary>
        /// Placed on the ground. Slips whoever runs over it.
        /// </summary>
        Banana = 1
    }

    /// <summary>
    /// How each kind is used. Thrown props travel and hit; placed props wait.
    /// </summary>
    public enum ThrowableUse
    {
        Thrown = 0,
        Placed = 1
    }

    public static class ThrowableCatalog
    {
        /// <summary>
        /// Durations come from <c>docs/03_GAME_RULES.md</c> section 12: a strong
        /// stun is 1.0~1.5s and a banana slip is 1.0s. They live here rather
        /// than in a config asset for now because the whole item layer is a
        /// prototype; they move to <c>Settings/Configs</c> once the set settles.
        /// </summary>
        public const float RockStunSeconds = 1.2f;
        public const float BananaSlipSeconds = 1.0f;

        /// <summary>
        /// How far a thrown prop travels before it drops. Short on purpose:
        /// a rock that crosses the map would make the chase a shooting range.
        ///
        /// Deliberately shorter than the torch's 17 m reach, so seeing somebody
        /// is not the same as being able to hit them — the officer still has to
        /// close the distance, which is the chase.
        /// </summary>
        public const float ThrowRangeMeters = 12f;

        /// <summary>
        /// Half-width of the corridor a throw sweeps. Measured against the rig
        /// rather than picked: the player capsule's radius is 0.45 m, so a
        /// corridor this wide clears a character's own width either side of the
        /// line and the throw connects on a graze.
        ///
        /// Generous on purpose. Aiming with the mouse from a fixed tilted camera
        /// is not precise, and a pinpoint line would make every throw feel
        /// stolen rather than missed.
        /// </summary>
        public const float ThrowHitRadiusMeters = 1.25f;

        public static ThrowableUse GetUse(ThrowableKind kind)
        {
            return kind == ThrowableKind.Banana
                ? ThrowableUse.Placed
                : ThrowableUse.Thrown;
        }

        public static float GetStunSeconds(ThrowableKind kind)
        {
            return kind == ThrowableKind.Banana
                ? BananaSlipSeconds
                : RockStunSeconds;
        }

        /// <summary>
        /// Model stem under <c>Assets/_Project/Art/Props</c>. The banana has no
        /// authored model yet, so it borrows the can until one arrives.
        /// </summary>
        public static string GetModelStem(ThrowableKind kind)
        {
            return kind == ThrowableKind.Banana
                ? "throwable_can"
                : "throwable_rock";
        }
    }
}
