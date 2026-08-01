using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class QuickSlotControllerTests
    {
        [Test]
        public void StoresFourItemsAndRejectsTheFifth()
        {
            var slots = new QuickSlotController();

            for (int index = 0; index < QuickSlotController.SlotCount; index++)
            {
                Assert.That(
                    slots.TryStore(ThrowableKind.Rock, out int stored),
                    Is.True);
                Assert.That(stored, Is.EqualTo(index));
            }

            Assert.That(
                slots.TryStore(ThrowableKind.Banana, out _),
                Is.False);
        }

        [Test]
        public void SelectionAndPackedReplicationPreserveEmptySlots()
        {
            var source = new QuickSlotController();
            source.TryStore(ThrowableKind.Rock, out _);
            source.TryStore(ThrowableKind.SensorLight, out _);
            source.SelectSlot(3);

            var copy = new QuickSlotController();
            copy.ApplyEncodedSlots(source.EncodeSlots(), source.SelectedSlot);

            Assert.That(copy.SelectedSlot, Is.EqualTo(3));
            Assert.That(copy.TryGet(0, out ThrowableKind first), Is.True);
            Assert.That(first, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(copy.TryGet(1, out ThrowableKind second), Is.True);
            Assert.That(second, Is.EqualTo(ThrowableKind.SensorLight));
            Assert.That(copy.TryGet(2, out _), Is.False);
        }

        [Test]
        public void ConsumingSelectedSlotDoesNotConsumeAnotherSlot()
        {
            var slots = new QuickSlotController();
            slots.TryStore(ThrowableKind.Rock, out _);
            slots.TryStore(ThrowableKind.Banana, out _);
            slots.SelectSlot(0);

            Assert.That(
                slots.TryConsumeSelected(out ThrowableKind consumed),
                Is.True);
            Assert.That(consumed, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(slots.TryGet(0, out _), Is.False);
            Assert.That(slots.TryGet(1, out ThrowableKind remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(ThrowableKind.Banana));
        }
    }
}
