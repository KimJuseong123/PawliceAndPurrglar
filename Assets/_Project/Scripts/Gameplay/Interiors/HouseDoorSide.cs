namespace PawsAndLoot.Gameplay.Interiors
{
    /// <summary>
    /// Which of a house's two doors a doorway is.
    ///
    /// A house with only a front door is a dead end: the thief who goes in has one
    /// way out and the officer only has to stand on the porch. Two doors make a
    /// room a route rather than a trap, so the side has to be carried around
    /// explicitly — the front door and the back door lead to the same room but to
    /// opposite ends of it, and coming in the back and leaving out the front is the
    /// whole point.
    ///
    /// The model names its parts <c>Door_Front_*</c> and <c>Door_Back_*</c>, and
    /// the front is the porch side (+Z) — measured, after the first version put
    /// both the way in and the way out at the back door (ISSUE-034).
    /// </summary>
    public enum HouseDoorSide
    {
        Front = 0,
        Back = 1
    }
}
