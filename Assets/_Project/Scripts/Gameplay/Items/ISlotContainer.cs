namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// Something with numbered slots the exchange screen can move items in and
    /// out of: the cat's bag, a cupboard, a shop counter's till.
    ///
    /// Pulled out of <c>CatInventoryInteractable</c> rather than written for the
    /// cupboards. The cat already had a working two-panel exchange - container on
    /// the left, the thief's quick slots on the right, click a slot to move one -
    /// and a second screen doing the same job for furniture would have been a
    /// second place for "the icon moved but the item did not" to happen. The HUD
    /// now talks to this and does not know which of the two it has.
    ///
    /// Deliberately smaller than <c>QuickSlotController</c>. The exchange needs to
    /// read a slot, take one out, and ask whether one fits; it has no business
    /// with selection, consumption, or encoding.
    /// </summary>
    public interface ISlotContainer
    {
        /// <summary>
        /// How many slots to draw. A cupboard is not the same size as the cat's
        /// bag, and the panel is built from this rather than from a constant.
        /// </summary>
        int SlotCount { get; }

        /// <summary>
        /// What to put at the top of the panel — "주방 찬장", "고양이 가방".
        /// The player is looking at two grids of the same icons and the heading is
        /// the only thing that says which one is theirs.
        /// </summary>
        string DisplayName { get; }

        bool TryGetSlot(int index, out ThrowableKind kind);

        int GetSlotQuantity(int index);

        bool CanStore(ThrowableKind kind, int quantity);

        bool TryStore(ThrowableKind kind, int quantity);

        /// <summary>
        /// Takes exactly one out of a slot.
        ///
        /// One rather than the whole stack, and it must come out before it goes in
        /// anywhere: the transfer is "remove, then add, and put it back if the add
        /// fails". Copying first and clearing afterwards is how a container ends
        /// up handing out the same item twice.
        /// </summary>
        bool TryTakeOne(int index, out ThrowableKind kind);
    }
}
