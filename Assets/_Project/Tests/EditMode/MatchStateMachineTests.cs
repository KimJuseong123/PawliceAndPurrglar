using System.Collections.Generic;
using NUnit.Framework;
using PawsAndLoot.Match;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class MatchStateMachineTests
    {
        [Test]
        public void NewMatchStartsInLobby()
        {
            IMatchStateReader reader = new MatchStateMachine();

            Assert.That(reader.CurrentState, Is.EqualTo(MatchState.Lobby));
            Assert.That(reader.IsGameplayActive, Is.False);
        }

        [Test]
        public void AllowedLifecycleReachesResultInOrder()
        {
            var machine = new MatchStateMachine();
            MatchState[] sequence =
            {
                MatchState.Ready,
                MatchState.Playing,
                MatchState.Ending,
                MatchState.Result
            };

            foreach (MatchState nextState in sequence)
            {
                Assert.That(
                    machine.TryTransitionTo(nextState),
                    Is.True,
                    $"Expected transition to {nextState}.");
                Assert.That(machine.CurrentState, Is.EqualTo(nextState));
            }
        }

        [Test]
        public void DuplicateTransitionIsRejectedWithoutNotification()
        {
            var machine = new MatchStateMachine();
            int notificationCount = 0;
            machine.StateChanged += _ => notificationCount++;

            Assert.That(machine.TryTransitionTo(MatchState.Ready), Is.True);
            Assert.That(machine.TryTransitionTo(MatchState.Ready), Is.False);
            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(machine.CurrentState, Is.EqualTo(MatchState.Ready));
        }

        [TestCase(MatchState.Playing)]
        [TestCase(MatchState.Ending)]
        [TestCase(MatchState.Result)]
        [TestCase((MatchState)999)]
        public void LobbyRejectsSkippedOrUnknownState(MatchState invalidState)
        {
            var machine = new MatchStateMachine();

            Assert.That(machine.CanTransitionTo(invalidState), Is.False);
            Assert.That(machine.TryTransitionTo(invalidState), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo(MatchState.Lobby));
        }

        [Test]
        public void LifecycleRejectsBackwardTransition()
        {
            var machine = new MatchStateMachine();
            Assert.That(machine.TryTransitionTo(MatchState.Ready), Is.True);
            Assert.That(machine.TryTransitionTo(MatchState.Playing), Is.True);

            Assert.That(machine.TryTransitionTo(MatchState.Ready), Is.False);
            Assert.That(machine.CurrentState, Is.EqualTo(MatchState.Playing));
        }

        [Test]
        public void NotificationsContainPreviousAndCurrentStates()
        {
            var machine = new MatchStateMachine();
            var notifications = new List<MatchStateChanged>();
            machine.StateChanged += notifications.Add;

            machine.TryTransitionTo(MatchState.Ready);
            machine.TryTransitionTo(MatchState.Playing);

            Assert.That(notifications, Has.Count.EqualTo(2));
            Assert.That(
                notifications[0].PreviousState,
                Is.EqualTo(MatchState.Lobby));
            Assert.That(
                notifications[0].CurrentState,
                Is.EqualTo(MatchState.Ready));
            Assert.That(
                notifications[1].PreviousState,
                Is.EqualTo(MatchState.Ready));
            Assert.That(
                notifications[1].CurrentState,
                Is.EqualTo(MatchState.Playing));
            Assert.That(machine.IsGameplayActive, Is.True);
        }

        [Test]
        public void GameplayIsActiveOnlyWhilePlaying()
        {
            var machine = new MatchStateMachine();

            Assert.That(machine.IsGameplayActive, Is.False);
            machine.TryTransitionTo(MatchState.Ready);
            Assert.That(machine.IsGameplayActive, Is.False);
            machine.TryTransitionTo(MatchState.Playing);
            Assert.That(machine.IsGameplayActive, Is.True);
            machine.TryTransitionTo(MatchState.Ending);
            Assert.That(machine.IsGameplayActive, Is.False);
            machine.TryTransitionTo(MatchState.Result);
            Assert.That(machine.IsGameplayActive, Is.False);
        }
    }
}
