namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// What a transfer did, so the caller can say something useful about it.
    ///
    /// <c>Blocked</c> and a <c>Moved</c> of zero are different outcomes and the
    /// player can tell: a full bag needs "가방이 가득 찼습니다", an empty cupboard
    /// needs "아무것도 없다", and one message for both is a message that is wrong
    /// half the time.
    /// </summary>
    public readonly struct TransferResult
    {
        public TransferResult(int moved, bool blocked)
        {
            Moved = moved;
            Blocked = blocked;
        }

        public int Moved { get; }

        /// <summary>
        /// Whether anything was left behind because it would not fit, as opposed to
        /// because there was nothing there.
        /// </summary>
        public bool Blocked { get; }

        public bool MovedAnything => Moved > 0;
    }

    /// <summary>
    /// Moves items between two slot containers.
    ///
    /// One routine rather than a copy per screen and per key. The order matters and
    /// is easy to get subtly wrong: take it out, put it in, and put it back if the
    /// put-in fails. Written the other way round — copy in, then clear — a failure
    /// half way leaves the item in both places, and the spec calls that out as the
    /// bug to avoid. Bulk transfer written as its own loop is precisely where that
    /// happens, so bulk transfer is this routine in a loop.
    ///
    /// Deliberately not a MonoBehaviour and deliberately unaware of the HUD, so it
    /// can be tested without building a canvas.
    /// </summary>
    public static class ContainerTransfer
    {
        /// <summary>
        /// Moves exactly one item out of <paramref name="index"/>.
        /// </summary>
        public static TransferResult MoveOne(
            ISlotContainer from,
            ISlotContainer to,
            int index)
        {
            if (from == null
                || to == null
                || ReferenceEquals(from, to)
                || !from.TryGetSlot(index, out ThrowableKind kind))
            {
                return new TransferResult(0, false);
            }

            if (!to.CanStore(kind, 1))
            {
                return new TransferResult(0, true);
            }

            if (!from.TryTakeOne(index, out kind))
            {
                return new TransferResult(0, false);
            }

            if (!to.TryStore(kind, 1))
            {
                // Put back exactly what came out. The alternative is an item that
                // exists in neither container, which is worse than one that never
                // moved.
                from.TryStore(kind, 1);
                return new TransferResult(0, true);
            }

            return new TransferResult(1, false);
        }

        /// <summary>
        /// Moves as much as the destination will take.
        ///
        /// As much as it will take, rather than all or nothing. A bag with one free
        /// slot against a full cupboard should take the one; refusing the lot because
        /// the third item does not fit reads as a broken key.
        ///
        /// Sweeps the slots repeatedly. Taking one out can leave a stack behind in
        /// the same slot, so a single pass would miss what is still there, and
        /// stopping at the first slot that will not move would skip the ones after
        /// it. A pass that moves nothing ends it, which is also what stops this
        /// spinning when the destination is full.
        /// </summary>
        public static TransferResult MoveEverything(
            ISlotContainer from,
            ISlotContainer to)
        {
            if (from == null || to == null || ReferenceEquals(from, to))
            {
                return new TransferResult(0, false);
            }

            int moved = 0;
            bool blocked = false;
            bool movedThisPass = true;
            while (movedThisPass)
            {
                movedThisPass = false;
                for (int index = 0; index < from.SlotCount; index++)
                {
                    TransferResult step = MoveOne(from, to, index);
                    moved += step.Moved;
                    blocked |= step.Blocked;
                    movedThisPass |= step.MovedAnything;
                }
            }

            return new TransferResult(moved, blocked);
        }
    }
}
