namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// The one place the torch cone's shape is written down.
    ///
    /// Three separate things read this: the spot light, the rule that hides the
    /// thief, and the outline drawn on the ground. They were drifting apart —
    /// the light opened 23° while the rule used 30°, so the thief became
    /// visible in a band that was never lit. From a fixed overhead camera that
    /// reads as the game cheating, because the player is looking at the lit
    /// floor and judging distance from it.
    ///
    /// Sharing the numbers is what makes the drawn outline trustworthy: it is
    /// the rule, not an approximation of it.
    /// </summary>
    public static class FlashlightCone
    {
        /// <summary>
        /// Half-angle of the cone. The visibility test measures this on the
        /// ground plane, so it is a planar half-angle, not a light cone angle.
        /// </summary>
        public const float HalfAngleDegrees = 30f;

        public const float RangeMeters = 17f;

        /// <summary>
        /// Always visible inside this radius whatever way the officer faces.
        /// Wider than the 1.75 m arrest range on purpose — somebody close
        /// enough to be arrested must never be invisible, or the police is
        /// grappling with thin air.
        /// </summary>
        public const float AlwaysSeenRadius = 3f;

        /// <summary>
        /// How far the beam is tilted down.
        ///
        /// The light and the rule cannot agree exactly: the rule is flat and
        /// the light is a cone pointed at the floor, so a tilted beam paints an
        /// ellipse rather than the rule's wedge. That is why the wedge is also
        /// drawn as an outline — the light sells the night, the outline states
        /// the rule.
        /// </summary>
        public const float PitchDegrees = 22f;

        /// <summary>
        /// Spot angle that opens the light as wide as the rule, so the lit
        /// floor and the wedge broadly agree instead of contradicting.
        /// </summary>
        public const float SpotAngleDegrees = HalfAngleDegrees * 2f;
    }
}
