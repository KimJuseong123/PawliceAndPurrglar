using NUnit.Framework;
using PawliceAndPurrglar.Companions;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class CompanionStateMachineTests
    {
        [Test]
        public void StartsIdleAndActive()
        {
            var machine = new CompanionStateMachine();
            Assert.That(
                machine.CurrentState,
                Is.EqualTo(CompanionState.Idle));
            Assert.That(machine.IsActive, Is.True);
            Assert.That(machine.IsBusyWithCommand, Is.False);
        }

        [Test]
        public void RejectsTransitionToTheSameState()
        {
            var machine = new CompanionStateMachine();
            Assert.That(
                machine.TryTransitionTo(CompanionState.Idle),
                Is.False);
        }

        [TestCase(CompanionState.Idle, CompanionState.Follow, true)]
        [TestCase(CompanionState.Idle, CompanionState.MoveToTarget, true)]
        [TestCase(CompanionState.Idle, CompanionState.Cooldown, false)]
        [TestCase(CompanionState.Idle, CompanionState.ReturnToOwner, false)]
        [TestCase(CompanionState.Follow, CompanionState.MoveToTarget, true)]
        [TestCase(
            CompanionState.MoveToTarget,
            CompanionState.ExecuteCommand,
            true)]
        [TestCase(
            CompanionState.MoveToTarget,
            CompanionState.ReturnToOwner,
            true)]
        [TestCase(
            CompanionState.ExecuteCommand,
            CompanionState.Cooldown,
            true)]
        [TestCase(
            CompanionState.ExecuteCommand,
            CompanionState.MoveToTarget,
            false)]
        [TestCase(CompanionState.Cooldown, CompanionState.Follow, true)]
        [TestCase(
            CompanionState.Cooldown,
            CompanionState.ExecuteCommand,
            false)]
        public void OnlyDeclaredTransitionsAreAllowed(
            CompanionState from,
            CompanionState to,
            bool expected)
        {
            var machine = new CompanionStateMachine(from);
            Assert.That(machine.CanTransitionTo(to), Is.EqualTo(expected));
            Assert.That(machine.TryTransitionTo(to), Is.EqualTo(expected));
        }

        [TestCase(CompanionState.Idle)]
        [TestCase(CompanionState.Follow)]
        [TestCase(CompanionState.MoveToTarget)]
        [TestCase(CompanionState.ExecuteCommand)]
        [TestCase(CompanionState.ReturnToOwner)]
        [TestCase(CompanionState.Cooldown)]
        public void DisabledIsReachableFromEveryActiveState(
            CompanionState from)
        {
            var machine = new CompanionStateMachine(from);
            Assert.That(
                machine.TryTransitionTo(CompanionState.Disabled),
                Is.True);
            Assert.That(machine.IsActive, Is.False);
        }

        [Test]
        public void DisabledIgnoresEveryQueuedTransition()
        {
            var machine = new CompanionStateMachine(
                CompanionState.Disabled);
            Assert.That(
                machine.TryTransitionTo(CompanionState.Follow),
                Is.False);
            Assert.That(
                machine.TryTransitionTo(CompanionState.MoveToTarget),
                Is.False);
            Assert.That(
                machine.CurrentState,
                Is.EqualTo(CompanionState.Disabled));
        }

        [Test]
        public void ResetRecoversFromDisabledForARematch()
        {
            var machine = new CompanionStateMachine(
                CompanionState.Disabled);
            machine.ResetTo(CompanionState.Idle);
            Assert.That(
                machine.CurrentState,
                Is.EqualTo(CompanionState.Idle));
            Assert.That(machine.IsActive, Is.True);
        }

        [Test]
        public void BusyWithCommandCoversMovementAndExecution()
        {
            var moving = new CompanionStateMachine(
                CompanionState.MoveToTarget);
            var executing = new CompanionStateMachine(
                CompanionState.ExecuteCommand);
            var cooling = new CompanionStateMachine(
                CompanionState.Cooldown);
            Assert.That(moving.IsBusyWithCommand, Is.True);
            Assert.That(executing.IsBusyWithCommand, Is.True);
            Assert.That(cooling.IsBusyWithCommand, Is.False);
        }

        [Test]
        public void StateChangeReportsBothStates()
        {
            var machine = new CompanionStateMachine();
            CompanionStateChanged captured = default;
            int count = 0;
            machine.StateChanged += change =>
            {
                captured = change;
                count++;
            };

            Assert.That(
                machine.TryTransitionTo(CompanionState.Follow),
                Is.True);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(
                captured.PreviousState,
                Is.EqualTo(CompanionState.Idle));
            Assert.That(
                captured.CurrentState,
                Is.EqualTo(CompanionState.Follow));
        }
    }
}
