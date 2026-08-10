using System;

namespace PawliceAndPurrglar.Companions
{
    /// <summary>
    /// COMP-001. Pure state machine shared by every companion.
    ///
    /// Deliberately has no Unity dependency so the lifecycle can be tested
    /// without a scene. Only transitions listed here are legal; anything else
    /// is rejected rather than silently applied, matching the match and loot
    /// state machines.
    /// </summary>
    public sealed class CompanionStateMachine
    {
        public CompanionStateMachine(
            CompanionState initialState = CompanionState.Idle)
        {
            if (!Enum.IsDefined(typeof(CompanionState), initialState))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialState),
                    initialState,
                    "Unknown companion state.");
            }

            CurrentState = initialState;
        }

        public event Action<CompanionStateChanged> StateChanged;

        public CompanionState CurrentState { get; private set; }

        /// <summary>
        /// True while the companion may act on commands or movement.
        /// </summary>
        public bool IsActive =>
            CurrentState != CompanionState.Disabled;

        /// <summary>
        /// True while a command is being carried out, so a second request has
        /// to be treated as a replacement rather than a parallel order.
        /// </summary>
        public bool IsBusyWithCommand =>
            CurrentState == CompanionState.MoveToTarget
            || CurrentState == CompanionState.ExecuteCommand;

        public bool CanTransitionTo(CompanionState nextState)
        {
            if (!Enum.IsDefined(typeof(CompanionState), nextState))
            {
                return false;
            }

            if (nextState == CurrentState)
            {
                return false;
            }

            // Disabled is reachable from anywhere: the match can end or the
            // companion can be removed at any moment.
            if (nextState == CompanionState.Disabled)
            {
                return true;
            }

            return CurrentState switch
            {
                CompanionState.Idle =>
                    nextState is CompanionState.Follow
                        or CompanionState.MoveToTarget
                        or CompanionState.ExecuteCommand,
                CompanionState.Follow =>
                    nextState is CompanionState.Idle
                        or CompanionState.MoveToTarget
                        or CompanionState.ExecuteCommand,
                CompanionState.MoveToTarget =>
                    nextState is CompanionState.ExecuteCommand
                        or CompanionState.ReturnToOwner
                        or CompanionState.Cooldown
                        or CompanionState.Follow,
                CompanionState.ExecuteCommand =>
                    nextState is CompanionState.Cooldown
                        or CompanionState.ReturnToOwner
                        or CompanionState.Follow,
                CompanionState.ReturnToOwner =>
                    nextState is CompanionState.Follow
                        or CompanionState.Idle
                        or CompanionState.Cooldown,
                CompanionState.Cooldown =>
                    nextState is CompanionState.Follow
                        or CompanionState.Idle
                        or CompanionState.ReturnToOwner,
                // Only an explicit reset leaves Disabled, so the match end
                // cannot be undone by a queued command.
                CompanionState.Disabled => false,
                _ => false
            };
        }

        public bool TryTransitionTo(CompanionState nextState)
        {
            if (!CanTransitionTo(nextState))
            {
                return false;
            }

            CompanionState previousState = CurrentState;
            CurrentState = nextState;
            StateChanged?.Invoke(
                new CompanionStateChanged(previousState, nextState));
            return true;
        }

        /// <summary>
        /// Returns the companion to a usable lifecycle, used when a new match
        /// starts. This is the only way out of <see cref="CompanionState.Disabled"/>.
        /// </summary>
        public void ResetTo(CompanionState state)
        {
            if (!Enum.IsDefined(typeof(CompanionState), state))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    "Unknown companion state.");
            }

            if (state == CurrentState)
            {
                return;
            }

            CompanionState previousState = CurrentState;
            CurrentState = state;
            StateChanged?.Invoke(
                new CompanionStateChanged(previousState, state));
        }
    }
}
