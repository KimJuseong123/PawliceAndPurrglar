using PawsAndLoot.Animation;
using UnityEngine;

namespace PawsAndLoot.Companions
{
    /// <summary>
    /// Turns what the animal did into what it shows.
    ///
    /// The mapping is the whole point of this class, and it is a narrowing:
    /// twenty outcomes and a state machine collapse into four faces. The
    /// question a player is asking mid-chase is "did that work", not "which of
    /// twenty branches did the resolver take", and an icon that tries to answer
    /// the second answers neither.
    ///
    /// Reads only. Nothing here can change what the animal does, so a missing
    /// icon or a camera that has gone away costs a picture and not a match.
    /// </summary>
    [RequireComponent(typeof(CompanionExpressionView))]
    public sealed class CompanionExpressionPresenter : MonoBehaviour
    {
        [SerializeField]
        private CompanionAgent agent;

        [SerializeField]
        private CompanionExpressionView view;

        private bool _subscribed;

        public void Configure(
            CompanionAgent configuredAgent,
            CompanionExpressionView configuredView)
        {
            Unsubscribe();
            agent = configuredAgent;
            view = configuredView;
            Subscribe();
        }

        /// <summary>
        /// Which face an outcome deserves.
        ///
        /// Everything that found something is <see cref="CompanionExpression.Alert"/>,
        /// everything that started work is Happy, and every way of not being
        /// able to is Confused. The three "nothing there" outcomes are the ones
        /// worth getting right: a dog that trots off after no trail at all is
        /// the single most confusing thing the animals do, and it is confusing
        /// precisely because it looks identical to a dog that found one.
        /// </summary>
        public static CompanionExpression FaceFor(
            CompanionCommandOutcome outcome)
        {
            switch (outcome)
            {
                case CompanionCommandOutcome.TrailFound:
                case CompanionCommandOutcome.BarkRevealedThief:
                case CompanionCommandOutcome.ScoutReported:
                    return CompanionExpression.Alert;

                case CompanionCommandOutcome.Completed:
                case CompanionCommandOutcome.DistractionStarted:
                case CompanionCommandOutcome.SearchStarted:
                case CompanionCommandOutcome.GuardStarted:
                case CompanionCommandOutcome.StealStarted:
                case CompanionCommandOutcome.StealCarrying:
                case CompanionCommandOutcome.StealDelivered:
                case CompanionCommandOutcome.HideStored:
                    return CompanionExpression.Happy;

                case CompanionCommandOutcome.TrailMissing:
                case CompanionCommandOutcome.DistractionAlreadyActive:
                case CompanionCommandOutcome.DistractionTargetTooFar:
                case CompanionCommandOutcome.Abandoned:
                case CompanionCommandOutcome.BarkFoundNobody:
                case CompanionCommandOutcome.ScoutFoundNothing:
                case CompanionCommandOutcome.StealNoLoot:
                case CompanionCommandOutcome.StealOwnerBusy:
                case CompanionCommandOutcome.HideUnavailable:
                    return CompanionExpression.Confused;

                default:
                    return CompanionExpression.None;
            }
        }

        private void HandleOutcome(CompanionCommandOutcome outcome)
        {
            CompanionExpression face = FaceFor(outcome);
            if (face != CompanionExpression.None)
            {
                view?.Show(face);
            }
        }

        /// <summary>
        /// The moment the order is understood, which is earlier than the moment
        /// it produces an outcome.
        ///
        /// Some commands take seconds to report anything, and without this the
        /// animal stands there looking like it did not hear. Cooldown gets the
        /// confused face for the same reason a refusal does: from the player's
        /// side "too soon" and "no" are the same event.
        /// </summary>
        private void HandleStateChanged(CompanionStateChanged change)
        {
            if (change.CurrentState == CompanionState.MoveToTarget
                || change.CurrentState == CompanionState.ExecuteCommand)
            {
                view?.Show(CompanionExpression.Thinking);
            }
        }

        private void HandleRejected(
            CompanionCommandId command,
            CompanionCommandRejection rejection)
        {
            view?.Show(CompanionExpression.Confused);
        }

        private void Subscribe()
        {
            if (_subscribed || agent == null)
            {
                return;
            }

            agent.OutcomeReported += HandleOutcome;
            agent.StateChanged += HandleStateChanged;
            agent.CommandRecovered += HandleRejected;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (agent != null)
            {
                agent.OutcomeReported -= HandleOutcome;
                agent.StateChanged -= HandleStateChanged;
                agent.CommandRecovered -= HandleRejected;
            }

            _subscribed = false;
        }

        /// <summary>
        /// Resolves its own references rather than relying on what the scene
        /// builder set. Serialized references survive a save, but the ones this
        /// needs sit on the same object, and finding them here means a
        /// hand-placed animal works too.
        /// </summary>
        private void Awake()
        {
            if (view == null)
            {
                view = GetComponent<CompanionExpressionView>();
            }

            if (agent == null)
            {
                agent = GetComponent<CompanionAgent>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
