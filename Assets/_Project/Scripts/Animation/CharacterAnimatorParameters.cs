namespace PawliceAndPurrglar.Animation
{
    /// <summary>
    /// ART-003. The whole vocabulary the animation layer exposes.
    ///
    /// Game logic writes these and nothing else. It never names a state, never
    /// queries a clip and never waits on an animation event, so the Animator can
    /// be rebuilt or removed without touching a rule. Match results come from
    /// <c>MatchResultArbiter</c>; this layer only reflects them.
    /// </summary>
    public static class CharacterAnimatorParameters
    {
        /// <summary>Planar speed as a 0..1 fraction of the run speed.</summary>
        public const string Speed = "Speed";

        /// <summary>Fired when the owner issues a companion command.</summary>
        public const string Command = "Command";

        /// <summary>Set once when the match is won.</summary>
        public const string Win = "Win";

        /// <summary>Set once when the match is lost.</summary>
        public const string Lose = "Lose";

        public const string IdleState = "Idle";
        public const string WalkState = "Walk";
        public const string RunState = "Run";
        public const string CommandState = "Command";
        public const string WinState = "Win";
        public const string LoseState = "Lose";

        /// <summary>Below this the character is standing still.</summary>
        public const float WalkThreshold = 0.12f;

        /// <summary>Above this the walk blends into a run.</summary>
        public const float RunThreshold = 0.62f;
    }
}
