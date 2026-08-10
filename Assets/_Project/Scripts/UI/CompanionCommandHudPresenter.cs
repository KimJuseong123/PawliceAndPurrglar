using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
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
                // UX-002. Icon plus name plus key, so the command reads without
                // relying on the role colour.
                CompanionCommandId primary = ResolvePrimaryCommand();
                bool isPolice = roleSelector != null
                    && roleSelector.ActiveRole == PlayerRole.Police;
                commandLabel.text =
                    $"{(isPolice ? "🐕" : "🐈")}  "
                    + $"{CompanionCommandCatalog.GetDisplayName(primary)}"
                    + $"  [{(isPolice ? "1" : "2")}]";
            }

            // UX-003. Cooldown as a filled bar plus a number, and the animal's
            // current behaviour, so the player can see why a key does nothing.
            if (cooldownLabel != null)
            {
                float remaining = _boundAgent != null
                    ? _boundAgent.CooldownRemainingSeconds
                    : 0f;
                cooldownLabel.text = remaining > 0.05f
                    ? $"{BuildBar(remaining)}  {remaining:0.0}s"
                    : $"✔ READY   {DescribeAgentState()}";
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

        /// <summary>
        /// UX-003. Cooldown drawn with characters so it reads at a glance even
        /// on a small HUD, without needing a second Image component.
        /// </summary>
        private string BuildBar(float remainingSeconds)
        {
            const int segments = 8;
            float total = _boundAgent != null
                    && _boundAgent.CooldownRemainingSeconds > 0f
                ? Mathf.Max(remainingSeconds, 0.01f)
                : 1f;
            int filled = Mathf.Clamp(
                Mathf.CeilToInt(remainingSeconds / total * segments),
                0,
                segments);
            return "[" + new string('|', filled)
                + new string('.', segments - filled) + "]";
        }

        /// <summary>
        /// UX-003. What the animal is doing right now, so a refused command is
        /// explainable rather than mysterious.
        /// </summary>
        private string DescribeAgentState()
        {
            if (_boundAgent == null)
            {
                return "동료 없음";
            }

            return _boundAgent.CurrentState switch
            {
                CompanionState.Idle => "대기 중",
                CompanionState.Follow => "따라오는 중",
                CompanionState.MoveToTarget => "이동 중",
                CompanionState.ExecuteCommand => "수행 중",
                CompanionState.ReturnToOwner => "복귀 중",
                CompanionState.Cooldown => "쉬는 중",
                CompanionState.Disabled => "정지",
                _ => string.Empty
            };
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
