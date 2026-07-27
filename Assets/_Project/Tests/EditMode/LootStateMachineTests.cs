using System.Collections.Generic;
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

        /// <summary>
        /// NET-005. A non-authority machine can miss the intermediate packet and
        /// see only the destination, so it needs a way to land there.
        /// </summary>
        [Test]
        public void ResetToForcesAnIllegalDestinationAndNotifiesOnce()
        {
            var machine = new LootStateMachine();
            var changes = new List<LootStateChanged>();
            machine.StateChanged += changes.Add;

            // AVAILABLE -> CARRIED is refused by the rules on purpose.
            Assert.That(
                machine.CanTransitionTo(LootState.Carried),
                Is.False);

            machine.ResetTo(LootState.Carried);

            Assert.That(machine.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(
                changes[0].PreviousState,
                Is.EqualTo(LootState.Available));
            Assert.That(
                changes[0].CurrentState,
                Is.EqualTo(LootState.Carried));

            // Already there: nothing to announce.
            machine.ResetTo(LootState.Carried);
            Assert.That(changes.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResetToLeavesTheRulesInPlaceForLocalPlay()
        {
            var machine = new LootStateMachine();
            machine.ResetTo(LootState.Sold);

            Assert.That(machine.IsTerminal, Is.True);
            Assert.That(
                machine.TryTransitionTo(LootState.Reserved),
                Is.False,
                "Forcing a state must not turn the transition table off.");
        }
    }
}
