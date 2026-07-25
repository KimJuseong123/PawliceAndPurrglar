using NUnit.Framework;
using PawsAndLoot.Gameplay.Loot;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class LootStateMachineTests
    {
        [TestCase(LootState.Available, LootState.Reserved)]
        [TestCase(LootState.Dropped, LootState.Reserved)]
        [TestCase(LootState.Hidden, LootState.Reserved)]
        [TestCase(LootState.Reserved, LootState.Carried)]
        [TestCase(LootState.Carried, LootState.Dropped)]
        [TestCase(LootState.Carried, LootState.Hidden)]
        [TestCase(LootState.Carried, LootState.Sold)]
        public void DocumentedTransitionsAreAllowed(
            LootState initialState,
            LootState nextState)
        {
            var machine = new LootStateMachine(initialState);

            Assert.That(machine.TryTransitionTo(nextState), Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo(nextState));
        }

        [TestCase(LootState.Available, LootState.Carried)]
        [TestCase(LootState.Available, LootState.Sold)]
        [TestCase(LootState.Reserved, LootState.Sold)]
        [TestCase(LootState.Dropped, LootState.Sold)]
        [TestCase(LootState.Hidden, LootState.Sold)]
        public void SkippedTransitionsAreRejected(
            LootState initialState,
            LootState nextState)
        {
            var machine = new LootStateMachine(initialState);

            Assert.That(machine.TryTransitionTo(nextState), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo(initialState));
        }

        [Test]
        public void SoldLootCanNeverBeReused()
        {
            var machine = new LootStateMachine(LootState.Carried);
            Assert.That(machine.TryTransitionTo(LootState.Sold), Is.True);

            foreach (LootState state in System.Enum.GetValues(
                         typeof(LootState)))
            {
                Assert.That(
                    machine.TryTransitionTo(state),
                    Is.False,
                    $"SOLD unexpectedly transitioned to {state}.");
            }

            Assert.That(machine.IsTerminal, Is.True);
            Assert.That(machine.CurrentState, Is.EqualTo(LootState.Sold));
        }

        [Test]
        public void DuplicateTransitionDoesNotNotify()
        {
            var machine = new LootStateMachine();
            int notifications = 0;
            machine.StateChanged += _ => notifications++;

            Assert.That(
                machine.TryTransitionTo(LootState.Reserved),
                Is.True);
            Assert.That(
                machine.TryTransitionTo(LootState.Reserved),
                Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }
    }
}
