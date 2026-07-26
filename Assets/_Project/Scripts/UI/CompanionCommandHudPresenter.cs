using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    /// <summary>
    /// UI-004 command button, UI-005 cooldown readout and UI-006 result text.
    ///
    /// Read only. The button raises a command request through the dispatcher
    /// exactly like the number keys do, so pressing it can never change
    /// companion state directly.
    /// </summary>
    public sealed class CompanionCommandHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private Text commandLabel;

        [SerializeField]
        private Text cooldownLabel;

        [SerializeField]
        private Text feedbackLabel;

        [SerializeField]
        private Button commandButton;

        [SerializeField, Min(0.5f)]
        private float feedbackHoldSeconds = 3f;

        private CompanionAgent _boundAgent;
        private float _feedbackRemainingSeconds;

        public string CommandText =>
            commandLabel != null ? commandLabel.text : string.Empty;
        public string CooldownText =>
            cooldownLabel != null ? cooldownLabel.text : string.Empty;
        public string FeedbackText =>
            feedbackLabel != null ? feedbackLabel.text : string.Empty;

        public void Configure(
            CompanionCommandDispatcher configuredDispatcher,
            LocalPlayerRoleSelector configuredRoleSelector,
            Text configuredCommandLabel,
            Text configuredCooldownLabel,
            Text configuredFeedbackLabel,
            Button configuredButton)
        {
            dispatcher = configuredDispatcher;
            roleSelector = configuredRoleSelector;
            commandLabel = configuredCommandLabel;
            cooldownLabel = configuredCooldownLabel;
            feedbackLabel = configuredFeedbackLabel;
            commandButton = configuredButton;
            _feedbackRemainingSeconds = 0f;
            Bind();
            Refresh(0f);
        }

        /// <summary>
        /// The single representative command for the local role: TRACK for the
        /// police, DISTRACT for the thief. The rest stay unbuilt at this stage.
        /// </summary>
        public CompanionCommandId ResolvePrimaryCommand()
        {
            if (roleSelector == null)
            {
                return CompanionCommandId.None;
            }

            return roleSelector.ActiveRole == PlayerRole.Police
                ? CompanionCommandId.Track
                : CompanionCommandId.Distract;
        }

        public void IssuePrimaryCommand()
        {
            if (dispatcher == null || roleSelector == null)
            {
                return;
            }

            PlayerRole role = roleSelector.ActiveRole;
            int numberKey = role == PlayerRole.Police ? 1 : 2;
            Transform issuer = roleSelector.ActiveBinding?.Identity != null
                ? roleSelector.ActiveBinding.Identity.transform
                : null;
            Vector3? target = issuer != null
                ? issuer.position + issuer.forward * 8f
                : null;

            if (!dispatcher.TryDispatchNumberKey(
                    role,
                    numberKey,
                    CompanionCommandInputSource.Button,
                    Time.time,
                    null,
                    target,
                    out CompanionCommandRejection rejection))
            {
                ShowFeedback(
                    CompanionCommandOutcomeText.Describe(
                        CompanionCommandCatalog.GetCompanionKind(role),
                        rejection));
            }
        }

        public void Refresh(float deltaTime)
        {
            Bind();

            if (commandLabel != null)
            {
                commandLabel.text = CompanionCommandCatalog.GetDisplayName(
                    ResolvePrimaryCommand());
            }

            if (cooldownLabel != null)
            {
                float remaining = _boundAgent != null
                    ? _boundAgent.CooldownRemainingSeconds
                    : 0f;
                cooldownLabel.text = remaining > 0.05f
                    ? $"COOLDOWN {remaining:0.0}s"
                    : "READY";
            }

            if (commandButton != null)
            {
                commandButton.interactable = _boundAgent != null
                    && _boundAgent.IsActive
                    && _boundAgent.CooldownRemainingSeconds <= 0.05f;
            }

            if (_feedbackRemainingSeconds > 0f)
            {
                _feedbackRemainingSeconds -= Mathf.Max(0f, deltaTime);
                if (_feedbackRemainingSeconds <= 0f
                    && feedbackLabel != null)
                {
                    feedbackLabel.text = string.Empty;
                }
            }
        }

        private void Bind()
        {
            if (dispatcher == null || roleSelector == null)
            {
                return;
            }

            CompanionAgent expected = dispatcher.FindAgent(
                CompanionCommandCatalog.GetCompanionKind(
                    roleSelector.ActiveRole));
            if (expected == _boundAgent)
            {
                return;
            }

            if (_boundAgent != null)
            {
                _boundAgent.OutcomeReported -= HandleOutcome;
            }

            _boundAgent = expected;
            if (_boundAgent != null)
            {
                _boundAgent.OutcomeReported += HandleOutcome;
            }
        }

        private void HandleOutcome(CompanionCommandOutcome outcome)
        {
            if (_boundAgent == null)
            {
                return;
            }

            ShowFeedback(
                CompanionCommandOutcomeText.Describe(
                    _boundAgent.CompanionKind,
                    outcome));
        }

        private void ShowFeedback(string message)
        {
            if (feedbackLabel == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            feedbackLabel.text = message;
            _feedbackRemainingSeconds = feedbackHoldSeconds;
        }

        private void OnEnable()
        {
            if (commandButton != null)
            {
                commandButton.onClick.AddListener(IssuePrimaryCommand);
            }
        }

        private void OnDisable()
        {
            if (commandButton != null)
            {
                commandButton.onClick.RemoveListener(IssuePrimaryCommand);
            }

            if (_boundAgent != null)
            {
                _boundAgent.OutcomeReported -= HandleOutcome;
                _boundAgent = null;
            }
        }

        private void Update()
        {
            Refresh(Time.deltaTime);
        }
    }
}
