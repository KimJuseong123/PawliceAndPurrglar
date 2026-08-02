using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;

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

        [Test]
        public void ToolCarrierSelectionCanMoveToEmptySlotForHudFocus()
        {
            var owner = new GameObject("ToolCarrier");
            try
            {
                ToolCarrier carrier = owner.AddComponent<ToolCarrier>();

                Assert.That(carrier.SelectSlot(3), Is.True);
                Assert.That(carrier.SelectedSlot, Is.EqualTo(3));
                Assert.That(carrier.HasTool, Is.False);
                Assert.That(carrier.TryConsume(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void StoresQuantityInOneStackableSlot()
        {
            var slots = new QuickSlotController();

            Assert.That(
                slots.TryStore(ThrowableKind.Rock, 2, 9, out int firstSlot),
                Is.True);
            Assert.That(
                slots.TryStore(ThrowableKind.Rock, 1, 9, out int secondSlot),
                Is.True);

            Assert.That(firstSlot, Is.EqualTo(0));
            Assert.That(secondSlot, Is.EqualTo(0));
            Assert.That(slots.GetQuantity(0), Is.EqualTo(3));
            Assert.That(slots.OccupiedSlotCount, Is.EqualTo(1));
        }

        [Test]
        public void ConsumingStackedSlotReducesQuantityBeforeClearing()
        {
            var slots = new QuickSlotController();
            slots.TryStore(ThrowableKind.Rock, 2, 9, out _);

            Assert.That(slots.TryConsumeSelected(out ThrowableKind first), Is.True);
            Assert.That(first, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(slots.TryGet(0, out ThrowableKind remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(slots.GetQuantity(0), Is.EqualTo(1));

            Assert.That(slots.TryConsumeSelected(out ThrowableKind second), Is.True);
            Assert.That(second, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(slots.TryGet(0, out _), Is.False);
            Assert.That(slots.GetQuantity(0), Is.EqualTo(0));
        }

        [Test]
        public void PackedReplicationPreservesQuantitiesAndSelection()
        {
            var source = new QuickSlotController();
            source.TryStore(ThrowableKind.Rock, 3, 9, out _);
            source.TrySetSlot(2, ThrowableKind.Banana, 4);
            source.SelectSlot(2);

            var copy = new QuickSlotController();
            copy.ApplyEncodedSlots(
                source.EncodeSlots(),
                source.EncodeQuantities(),
                source.SelectedSlot);

            Assert.That(copy.SelectedSlot, Is.EqualTo(2));
            Assert.That(copy.GetQuantity(0), Is.EqualTo(3));
            Assert.That(copy.GetQuantity(2), Is.EqualTo(4));
            Assert.That(copy.TryGet(2, out ThrowableKind selected), Is.True);
            Assert.That(selected, Is.EqualTo(ThrowableKind.Banana));
        }

        [Test]
        public void PoliceStartingLoadoutSelectsThrowableFirst()
        {
            Assert.That(
                ThrowableCatalog.TryGetStartingLoadout(
                    PlayerRole.Police,
                    0,
                    out ThrowableLoadoutItem item),
                Is.True);
            Assert.That(item.Kind, Is.EqualTo(ThrowableKind.Rock));
            Assert.That(
                ThrowableCatalog.GetUse(item.Kind),
                Is.EqualTo(ThrowableUse.Thrown));
            Assert.That(item.Quantity, Is.GreaterThan(1));
        }
    }
}
