using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;

namespace PawsAndLoot.Animation
{
    /// <summary>
    /// ART-003. Reflects match and command results in the Animator.
    ///
    /// One direction only: it reads the arbiter's decision and the dispatcher's
    /// accepted commands, then sets parameters. It never decides an outcome, so
    /// animation can never change who wins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterOutcomeAnimator : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        [SerializeField]
        private PlayerRoleIdentity identity;

        [SerializeField]
        private MatchEndController matchEndController;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        private static readonly int CommandId =
            Animator.StringToHash(CharacterAnimatorParameters.Command);
        private static readonly int WinId =
            Animator.StringToHash(CharacterAnimatorParameters.Win);
        private static readonly int LoseId =
            Animator.StringToHash(CharacterAnimatorParameters.Lose);

        private bool _subscribed;

        public bool HasReportedResult { get; private set; }
        public int CommandTriggerCount { get; private set; }

        public void Configure(
            Animator configuredAnimator,
            PlayerRoleIdentity configuredIdentity,
            MatchEndController configuredEndController,
            CompanionCommandDispatcher configuredDispatcher)
        {
            Unsubscribe();
            animator = configuredAnimator;
            identity = configuredIdentity;
            matchEndController = configuredEndController;
            dispatcher = configuredDispatcher;
            HasReportedResult = false;
            CommandTriggerCount = 0;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted += HandleMatchEnded;
            }

            if (dispatcher != null)
            {
                dispatcher.CommandAccepted += HandleCommandAccepted;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (matchEndController != null)
            {
                matchEndController.MatchEndingStarted -= HandleMatchEnded;
            }

            if (dispatcher != null)
            {
                dispatcher.CommandAccepted -= HandleCommandAccepted;
            }

            _subscribed = false;
        }

        private void HandleCommandAccepted(CompanionCommandRequest request)
        {
            if (identity == null || request.IssuerRole != identity.Role)
            {
                return;
            }

            CommandTriggerCount++;
            if (HasParameter(CharacterAnimatorParameters.Command))
            {
                animator.SetTrigger(CommandId);
            }
        }

        /// <summary>
        /// Reads the already decided result. Reported once, so a second end
        /// request cannot flip the pose.
        /// </summary>
        private void HandleMatchEnded(MatchResult result)
        {
            if (HasReportedResult || identity == null)
            {
                return;
            }

            HasReportedResult = true;
            bool won = result.Winner == MatchWinner.Police
                ? identity.Role == PlayerRole.Police
                : identity.Role == PlayerRole.Thief;

            if (won && HasParameter(CharacterAnimatorParameters.Win))
            {
                animator.SetBool(WinId, true);
            }
            else if (!won && HasParameter(CharacterAnimatorParameters.Lose))
            {
                animator.SetBool(LoseId, true);
            }
        }

        /// <summary>
        /// The controller is built from whatever clips exist, so a parameter may
        /// legitimately be absent. Setting a missing one logs an error every
        /// frame, which is worse than skipping it.
        /// </summary>
        private bool HasParameter(string parameterName)
        {
            if (animator == null
                || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in
                animator.parameters)
            {
                if (parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
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
