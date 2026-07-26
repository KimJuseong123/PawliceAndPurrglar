using PawsAndLoot.Companions;
using PawsAndLoot.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace PawsAndLoot.UI
{
    public sealed class VoiceCommandHudPresenter : MonoBehaviour
    {
        [SerializeField]
        private VoiceCommandController controller;

        [SerializeField]
        private CompanionCommandDispatcher dispatcher;

        [SerializeField]
        private Text statusText;

        [SerializeField]
        private Text transcriptText;

        public void Configure(
            VoiceCommandController configuredController,
            CompanionCommandDispatcher configuredDispatcher,
            Text configuredStatusText,
            Text configuredTranscriptText)
        {
            if (isActiveAndEnabled && controller != null)
            {
                controller.FeedbackChanged -= OnFeedbackChanged;
            }
            if (isActiveAndEnabled && dispatcher != null)
            {
                dispatcher.CommandResolved -= OnCommandResolved;
            }

            controller = configuredController;
            dispatcher = configuredDispatcher;
            statusText = configuredStatusText;
            transcriptText = configuredTranscriptText;
            if (isActiveAndEnabled && controller != null)
            {
                controller.FeedbackChanged += OnFeedbackChanged;
                Refresh(controller.LastFeedback);
            }
            if (isActiveAndEnabled && dispatcher != null)
            {
                dispatcher.CommandResolved += OnCommandResolved;
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.FeedbackChanged += OnFeedbackChanged;
                Refresh(controller.LastFeedback);
            }
            if (dispatcher != null)
            {
                dispatcher.CommandResolved += OnCommandResolved;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.FeedbackChanged -= OnFeedbackChanged;
            }
            if (dispatcher != null)
            {
                dispatcher.CommandResolved -= OnCommandResolved;
            }
        }

        private void OnFeedbackChanged(
            VoiceCommandFeedback feedback)
        {
            Refresh(feedback);
        }

        private void OnCommandResolved(
            CompanionCommandResult result)
        {
            if (result.Request.Source
                != CompanionCommandSource.Keyboard)
            {
                return;
            }

            if (statusText != null)
            {
                string command = result.Request.CommandId
                    .ToString()
                    .ToUpperInvariant();
                statusText.text =
                    $"KEYBOARD COMMAND [{command}] "
                    + (result.Accepted ? "ACCEPTED" : "REJECTED")
                    + $"\n{result.Message}";
            }

            if (transcriptText != null)
            {
                transcriptText.text = "입력: 숫자키";
            }
        }

        private void Refresh(VoiceCommandFeedback feedback)
        {
            if (statusText != null)
            {
                string command = feedback.CommandId
                    == CompanionCommandId.None
                    ? "-"
                    : feedback.CommandId.ToString().ToUpperInvariant();
                statusText.text =
                    $"VOICE [{feedback.Status}]  COMMAND [{command}]\n"
                    + feedback.Message;
            }

            if (transcriptText != null)
            {
                transcriptText.text =
                    string.IsNullOrWhiteSpace(feedback.Transcript)
                        ? "들은 말: -"
                        : $"들은 말: {feedback.Transcript}";
            }
        }
    }
}
