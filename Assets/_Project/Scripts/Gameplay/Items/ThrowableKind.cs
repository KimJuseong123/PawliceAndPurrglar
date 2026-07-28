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
        /// Visual diameter of a thrown prop. The corridor is derived from it, so
        /// what the player watches fly is the same size as the thing that decides
        /// the hit.
        /// </summary>
        public const float PropDiameterMeters = 0.34f;

        /// <summary>
        /// Half-width of the corridor a throw sweeps, measured against the rig
        /// rather than picked.
        ///
        /// The brief was "the rock's edge grazing the victim's edge should
        /// count". A player capsule is 0.45 m in radius, so edges touching is
        /// 0.45 + one prop radius = 0.62 m. Two more prop widths of slack on top
        /// of that is what "generously" comes to.
        ///
        /// Worth stating plainly: this was never why throws were missing. The
        /// corridor was already wider than edge-to-edge, and the real cause was
        /// the throw stopping dead on invisible trigger volumes before it got
        /// anywhere near the target.
        /// </summary>
        public const float ThrowHitRadiusMeters =
            0.45f + PropDiameterMeters * 2.5f;

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
