using System.Collections.Generic;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// Moving items between two containers.
    ///
    /// The failure this fixture exists for is duplication: an item that ends up in
    /// both containers, or in neither. Neither shows up as an error - the icons look
    /// right in both panels and the count is only wrong if somebody adds it up - so
    /// every test here counts the total across both sides before and after.
    /// </summary>
    public sealed class ContainerTransferTests
    {
        /// <summary>
        /// A container that is nothing but slots, so the transfer rules are tested
        /// without a scene, a match, or a MonoBehaviour to keep alive.
        ///
        /// Backed by the real <c>QuickSlotController</c> rather than a dictionary,
        /// because stacking and capacity are the parts of "will it fit" that the
        /// transfer leans on, and a hand-written stand-in would agree with itself
        /// rather than with the game.
        /// </summary>
        private sealed class Bag : ISlotContainer
        {
            private readonly QuickSlotController _slots = new();
            private readonly int _maximumStack;

            public Bag(string name, int maximumStack = 9)
            {
                DisplayName = name;
                _maximumStack = maximumStack;
            }

            public int SlotCount => QuickSlotController.SlotCount;
            public string DisplayName { get; }

            public bool TryGetSlot(int index, out ThrowableKind kind) =>
                _slots.TryGet(index, out kind);

            public int GetSlotQuantity(int index) => _slots.GetQuantity(index);

            public bool CanStore(ThrowableKind kind, int quantity) =>
                _slots.CanStore(kind, quantity, _maximumStack);

            public bool TryStore(ThrowableKind kind, int quantity) =>
                _slots.TryStore(kind, quantity, _maximumStack, out _);

            public bool TryTakeOne(int index, out ThrowableKind kind) =>
                _slots.TryTakeOne(index, out kind);

            /// <summary>Everything in it, for counting across both sides.</summary>
            public Dictionary<ThrowableKind, int> Tally()
            {
                var tally = new Dictionary<ThrowableKind, int>();
                for (int index = 0; index < SlotCount; index++)
                {
                    if (!TryGetSlot(index, out ThrowableKind kind))
                    {
                        continue;
                    }

                    tally.TryGetValue(kind, out int already);
                    tally[kind] = already + GetSlotQuantity(index);
                }

                return tally;
            }

            public int Total()
            {
                int total = 0;
                for (int index = 0; index < SlotCount; index++)
                {
                    total += GetSlotQuantity(index);
                }

                return total;
            }
        }

        [Test]
        public void MovingOneLeavesTheTotalUnchanged()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방");
            Assert.That(from.TryStore(ThrowableKind.TunaCan, 3), Is.True);

            TransferResult result = ContainerTransfer.MoveOne(from, to, 0);

            Assert.That(result.Moved, Is.EqualTo(1));
            Assert.That(from.Total(), Is.EqualTo(2));
            Assert.That(to.Total(), Is.EqualTo(1));
            Assert.That(
                from.Total() + to.Total(),
                Is.EqualTo(3),
                "The transfer changed how many items exist.");
        }

        /// <summary>
        /// The one the spec names outright: the item must not survive in the source
        /// as well. A copy-then-clear that failed half way looks exactly like a
        /// working transfer until somebody counts.
        /// </summary>
        [Test]
        public void AMovedItemIsGoneFromTheSource()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방");
            from.TryStore(ThrowableKind.DogTreat, 1);

            ContainerTransfer.MoveOne(from, to, 0);

            Assert.That(
                from.TryGetSlot(0, out _),
                Is.False,
                "The source still holds the item that was moved out of it.");
            Assert.That(to.Tally()[ThrowableKind.DogTreat], Is.EqualTo(1));
        }

        /// <summary>
        /// A full destination must leave the item where it was, not drop it.
        /// </summary>
        [Test]
        public void AFullDestinationKeepsTheItemInTheSource()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방", maximumStack: 1);

            // Four distinct kinds fill all four slots at a stack limit of one.
            to.TryStore(ThrowableKind.Rock, 1);
            to.TryStore(ThrowableKind.Banana, 1);
            to.TryStore(ThrowableKind.TunaCan, 1);
            to.TryStore(ThrowableKind.DogTreat, 1);
            from.TryStore(ThrowableKind.RubberChicken, 1);

            TransferResult result = ContainerTransfer.MoveOne(from, to, 0);

            Assert.That(result.Moved, Is.EqualTo(0));
            Assert.That(
                result.Blocked,
                Is.True,
                "A refused move has to say it was refused, or the player is told "
                + "the cupboard was empty.");
            Assert.That(
                from.Tally()[ThrowableKind.RubberChicken],
                Is.EqualTo(1),
                "The item vanished when the bag would not take it.");
            Assert.That(to.Total(), Is.EqualTo(4));
        }

        [Test]
        public void TakingEverythingEmptiesTheContainer()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방");
            from.TryStore(ThrowableKind.TunaCan, 2);
            from.TryStore(ThrowableKind.Banana, 3);

            TransferResult result = ContainerTransfer.MoveEverything(from, to);

            Assert.That(result.Moved, Is.EqualTo(5));
            Assert.That(result.Blocked, Is.False);
            Assert.That(from.Total(), Is.EqualTo(0));
            Assert.That(to.Total(), Is.EqualTo(5));
        }

        /// <summary>
        /// The partial case, which is the one that reads as a broken key if it is
        /// implemented as all-or-nothing: what fits moves, what does not stays.
        /// </summary>
        [Test]
        public void TakingEverythingMovesWhatFitsAndLeavesTheRest()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방", maximumStack: 1);

            to.TryStore(ThrowableKind.Rock, 1);
            to.TryStore(ThrowableKind.Banana, 1);
            to.TryStore(ThrowableKind.TunaCan, 1);
            from.TryStore(ThrowableKind.DogTreat, 1);
            from.TryStore(ThrowableKind.RubberChicken, 1);

            TransferResult result = ContainerTransfer.MoveEverything(from, to);

            Assert.That(
                result.Moved,
                Is.EqualTo(1),
                "One free slot should have taken exactly one item.");
            Assert.That(result.Blocked, Is.True);
            Assert.That(from.Total() + to.Total(), Is.EqualTo(5));
            Assert.That(to.Total(), Is.EqualTo(4));
        }

        /// <summary>
        /// Nothing to take is not the same as nothing fitting, and the caller shows
        /// a different message for each.
        /// </summary>
        [Test]
        public void AnEmptyContainerReportsNothingRatherThanBlocked()
        {
            var from = new Bag("찬장");
            var to = new Bag("가방");

            TransferResult result = ContainerTransfer.MoveEverything(from, to);

            Assert.That(result.Moved, Is.EqualTo(0));
            Assert.That(result.Blocked, Is.False);
        }

        /// <summary>
        /// A container handed itself would double what it holds on the way through,
        /// and a full sweep of it would not terminate.
        /// </summary>
        [Test]
        public void MovingIntoItselfDoesNothing()
        {
            var bag = new Bag("가방");
            bag.TryStore(ThrowableKind.Rock, 2);

            Assert.That(ContainerTransfer.MoveOne(bag, bag, 0).Moved, Is.EqualTo(0));
            Assert.That(
                ContainerTransfer.MoveEverything(bag, bag).Moved,
                Is.EqualTo(0));
            Assert.That(bag.Total(), Is.EqualTo(2));
        }

        [Test]
        public void MissingContainersAreRefusedRatherThanThrowing()
        {
            var bag = new Bag("가방");
            bag.TryStore(ThrowableKind.Rock, 1);

            Assert.That(ContainerTransfer.MoveOne(null, bag, 0).Moved, Is.EqualTo(0));
            Assert.That(ContainerTransfer.MoveOne(bag, null, 0).Moved, Is.EqualTo(0));
            Assert.That(
                ContainerTransfer.MoveEverything(null, null).Moved,
                Is.EqualTo(0));
            Assert.That(bag.Total(), Is.EqualTo(1));
        }
    }
}
