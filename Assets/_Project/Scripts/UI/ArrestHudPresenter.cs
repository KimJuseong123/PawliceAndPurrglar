using System;
using PawsAndLoot.Gameplay.Arrest;
using PawsAndLoot.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class ArrestHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private LocalPlayerRoleSelector roleSelector;

        [SerializeField]
        private ArrestProgressController progressController;

        [SerializeField]
        private ArrestCompletionController completionController;

        [SerializeField]
        private GameObject panel;

        [SerializeField]
        private Image progressFill;

        [SerializeField]
        private Text statusLabel;

        [SerializeField]
        private Text thiefWarningLabel;

        private ArrestInterruptionReason? _lastInterruption;
        private bool _subscribed;

        public GameObject Panel => panel;
        public Image ProgressFill => progressFill;
        public Text StatusLabel => statusLabel;
        public Text ThiefWarningLabel => thiefWarningLabel;

        public void Configure(
            LocalPlayerRoleSelector selector,
            ArrestProgressController configuredProgressController,
            ArrestCompletionController configuredCompletionController,
            GameObject configuredPanel,
            Image configuredProgressFill,
            Text configuredStatusLabel,
            Text configuredThiefWarningLabel)
        {
            Unsubscribe();
            roleSelector = selector;
            progressController = configuredProgressController;
            completionController = configuredCompletionController;
            panel = configuredPanel;
            progressFill = configuredProgressFill;
            statusLabel = configuredStatusLabel;
            thiefWarningLabel = configuredThiefWarningLabel;
            _lastInterruption = null;
            ValidateOrThrow();
            Subscribe();
            Refresh();
        }

        public void Refresh()
        {
            if (panel == null
                || roleSelector == null
                || progressController == null
                || completionController == null)
            {
                return;
            }

            bool isPolice =
                roleSelector.ActiveRole == PlayerRole.Police;
            bool completed = completionController.IsCompleted;
            float progress = progressController.ProgressNormalized;
            if (progress > 0f && !completed)
            {
                _lastInterruption = null;
            }

            bool interrupted =
                _lastInterruption.HasValue && progress <= 0f;
            bool visible =
                isPolice || progress > 0f || interrupted || completed;
            panel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            progressFill.fillAmount = progress;
            progressFill.color = isPolice
                ? new Color(0.16f, 0.55f, 0.95f)
                : new Color(0.95f, 0.2f, 0.12f);
            thiefWarningLabel.gameObject.SetActive(
                !isPolice && (progress > 0f || completed));
            thiefWarningLabel.text = completed
                ? "CAUGHT"
                : "RUN!";

            statusLabel.text = ResolveStatus(
                isPolice,
                progress,
                interrupted,
                completed);
        }

        public void ValidateOrThrow()
        {
            if (roleSelector == null
                || progressController == null
                || completionController == null
                || panel == null
                || progressFill == null
                || statusLabel == null
                || thiefWarningLabel == null)
            {
                throw new InvalidOperationException(
                    $"ArrestHudPresenter '{name}' has missing references.");
            }
        }

        private static string ResolveStatus(
            bool isPolice,
            float progress,
            bool interrupted,
            bool completed)
        {
            if (completed)
            {
                return isPolice
                    ? "ARREST COMPLETE"
                    : "YOU WERE CAUGHT";
            }

            if (progress > 0f)
            {
                int percent = Mathf.RoundToInt(progress * 100f);
                return isPolice
                    ? $"ARRESTING  {percent}%"
                    : $"DANGER  {percent}%";
            }

            if (interrupted)
            {
                return isPolice
                    ? "ARREST INTERRUPTED"
                    : "ARREST ESCAPED";
            }

            return "ARREST READY";
        }

        private void HandleInterrupted(
            ArrestInterruptionReason reason)
        {
            _lastInterruption = reason;
            Refresh();
        }

        private void HandleCompleted()
        {
            Refresh();
        }

        private void Subscribe()
        {
            if (_subscribed
                || progressController == null
                || completionController == null)
            {
                return;
            }

            progressController.ProgressInterrupted +=
                HandleInterrupted;
            completionController.ArrestCompleted +=
                HandleCompleted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (progressController != null)
            {
                progressController.ProgressInterrupted -=
                    HandleInterrupted;
            }

            if (completionController != null)
            {
                completionController.ArrestCompleted -=
                    HandleCompleted;
            }

            _subscribed = false;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }
    }
}
