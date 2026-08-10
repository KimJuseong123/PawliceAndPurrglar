namespace PawliceAndPurrglar.Gameplay.Players
{
    /// <summary>
    /// An interactable that is offered to one role and not the other, without
    /// its <see cref="PlayerInteractionType"/> saying so.
    ///
    /// The permission table answers per type, and some types genuinely mean two
    /// different things depending on who is standing there: a bin is somewhere
    /// the thief hides and somewhere the officer turfs them out of, so it has to
    /// be <c>Generic</c>. The role check for those lived only in
    /// <c>TryInteract</c> — which runs on the press, long after the prompt has
    /// already been drawn.
    ///
    /// The result was a prompt that did nothing. An officer walking past an
    /// empty box was told they could interact with it, pressed E, and nothing
    /// happened. "The key does nothing" is the worst reading a key can have, and
    /// this project has now produced it three separate ways.
    ///
    /// Implementing this lets the thing itself say "not for you, not now", early
    /// enough that the prompt is never drawn.
    /// </summary>
    public interface IRoleAwareInteractable
    {
        /// <summary>
        /// Whether this is worth offering to <paramref name="role"/> right now.
        ///
        /// Answered per press rather than cached, because the honest answer
        /// changes during a match — an empty bin is nothing to an officer and an
        /// occupied one is the most interesting thing on the street.
        /// </summary>
        bool IsAvailableFor(PlayerRole role);
    }
}
