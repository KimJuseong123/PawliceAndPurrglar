namespace PawliceAndPurrglar.Gameplay.Players
{
    public enum PlayerInteractionType
    {
        Generic = 0,
        Loot = 1,
        Sale = 2,
        Arrest = 3,
        Traversal = 4,

        /// <summary>
        /// Operated by walking into it, never by a press.
        ///
        /// No role can interact with this, which is what keeps it out of the
        /// scanner: it takes no prompt and, more to the point, cannot win the
        /// nearest-target contest against the wardrobe standing beside it.
        /// </summary>
        Automatic = 5
    }
}
