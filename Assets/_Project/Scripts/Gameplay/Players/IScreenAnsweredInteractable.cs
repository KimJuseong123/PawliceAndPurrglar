namespace PawsAndLoot.Gameplay.Players
{
    /// <summary>
    /// Something the interact key answers with a screen rather than an action.
    ///
    /// The distinction exists because the key press is forwarded to the host and
    /// run there. Anything whose <c>TryInteract</c> only opens a panel therefore
    /// opens it **on whichever machine is hosting**, not on the machine whose
    /// player pressed the key. On a host-and-client pair that is the wrong screen
    /// half the time, and the failure is loud in the wrong place: the thief talks
    /// to their cat and the bag appears on the officer's monitor.
    ///
    /// Marked here, and the two local input paths skip forwarding the key for it.
    /// The HUD opens the panel off the same local key event instead. Nothing is
    /// lost, because these interactions do no work on the host — the work is the
    /// clicking that follows, which goes through its own host-authoritative
    /// route.
    ///
    /// An interface rather than a type check so the scanner does not have to
    /// reference the loot and companion namespaces to ask a question about
    /// interaction, and so the next screen-shaped interaction says so itself.
    /// </summary>
    public interface IScreenAnsweredInteractable
    {
    }
}
