namespace PawsAndLoot.Companions
{
    /// <summary>
    /// What the animal shows above its head.
    ///
    /// Four, because four is what a player can learn without being told. The
    /// game's pitch is that the animal understands you, and until now the only
    /// evidence of that was the animal eventually doing something — which is
    /// slow, easy to miss, and impossible to read in a recording. These say the
    /// same thing in the frame it happens.
    ///
    /// Deliberately not one icon per <see cref="CompanionCommandOutcome"/>.
    /// There are twenty of those and they answer "what exactly happened"; these
    /// four answer "did it work", which is the question being asked while you
    /// are running away from someone.
    /// </summary>
    public enum CompanionExpression
    {
        None = 0,

        /// <summary>Found something: a trail, the thief, loot worth taking.</summary>
        Alert = 1,

        /// <summary>Understood the order and started on it.</summary>
        Thinking = 2,

        /// <summary>Did what was asked.</summary>
        Happy = 3,

        /// <summary>Could not: refused, on cooldown, or nothing to act on.</summary>
        Confused = 4
    }
}
