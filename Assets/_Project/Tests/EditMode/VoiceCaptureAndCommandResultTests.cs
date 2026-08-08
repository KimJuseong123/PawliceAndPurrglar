using NUnit.Framework;
using PawsAndLoot.Integration.Voice;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class VoiceCaptureAndCommandResultTests
    {
        [Test]
        public void WavEncoderWritesPcmWaveHeader()
        {
            byte[] wav = WavAudioEncoder.Encode(
                new[] { 0f, 0.5f, -0.5f, 1f },
                4,
                1,
                16000);

            Assert.That(wav.Length, Is.EqualTo(52));
            Assert.That(System.Text.Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(System.Text.Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(System.Text.Encoding.ASCII.GetString(wav, 36, 4), Is.EqualTo("data"));
        }

        [Test]
        public void VoiceInputUsesExplicitCompletedAndFailedStates()
        {
            GameObject root = new("Voice Input Test");
            VoiceCommandInput input = root.AddComponent<VoiceCommandInput>();

            input.ApplyServerTranscript("dog scent track");
            Assert.That(input.State, Is.EqualTo(VoiceCommandInputState.Interpreting));

            input.ApplyServerDecision(true, new VoiceCommandResult
            {
                transcript = "dog scent track",
                interpretedCommand = "DOG SCENT TRACK",
                confidence = 1f,
                animalFeedback = "Command accepted"
            });
            Assert.That(input.State, Is.EqualTo(VoiceCommandInputState.Completed));
            Assert.That(input.CooldownRemainingSeconds, Is.EqualTo(30f));
            Assert.That(input.PostCommandCooldownSeconds, Is.EqualTo(30f));
            Assert.That(input.LastResult.interpretedCommand, Is.EqualTo("DOG SCENT TRACK"));

            input.ApplyServerFailure("VOICE_PROVIDER_MISSING");
            Assert.That(input.State, Is.EqualTo(VoiceCommandInputState.Failed));
            Assert.That(input.LastError, Is.EqualTo("VOICE_PROVIDER_MISSING"));

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// A failed attempt must cost nothing. The cooldown used to be started
        /// the moment the player stopped speaking, before the result was known,
        /// so a recording that was too quiet or a gateway that was not running
        /// locked the key for thirty seconds and the second press did nothing
        /// either (`ISSUE-069`).
        /// </summary>
        [Test]
        public void AFailedCommandDoesNotSpendTheCooldown()
        {
            GameObject root = new("Voice Cooldown Test");
            VoiceCommandInput input = root.AddComponent<VoiceCommandInput>();

            input.ApplyServerFailure("MIC_RETURNED_ONLY_ZEROS");
            Assert.That(input.CooldownRemainingSeconds, Is.EqualTo(0f));

            input.ApplyServerDecision(true, new VoiceCommandResult
            {
                transcript = "따라와",
                interpretedCommand = "DOG FOLLOW",
                confidence = 1f
            });
            Assert.That(input.CooldownRemainingSeconds, Is.EqualTo(30f));

            input.ApplyServerFailure("GATEWAY_NOT_READY");
            Assert.That(
                input.CooldownRemainingSeconds,
                Is.EqualTo(0f),
                "A failure must leave the key usable.");

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// One of these components is attached to each role object, so both
        /// exist on both machines. Without a locality gate one key press opened
        /// the recording device twice and one of the two captures received
        /// silence.
        /// </summary>
        [Test]
        public void ARoleThisMachineDoesNotPlayCannotOpenTheMicrophone()
        {
            GameObject root = new("Voice Locality Test");
            VoiceCommandInput input = root.AddComponent<VoiceCommandInput>();

            input.IsLocallyControlled = true;
            Assert.That(input.CanCaptureLocally(), Is.True);

            input.IsLocallyControlled = false;
            Assert.That(input.CanCaptureLocally(), Is.False);

            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// The failure panel used to print the raw code, so a missing local
        /// server, a muted microphone, and a denied permission all read the
        /// same to the player. They are fixed in three different places.
        /// </summary>
        [Test]
        public void EachVoiceFailureExplainsWhatToDoAboutIt()
        {
            Assert.That(
                UI.VoiceCommandFeedView.DescribeError("MIC_RETURNED_ONLY_ZEROS"),
                Does.Contain("무음"));
            Assert.That(
                UI.VoiceCommandFeedView.DescribeError("GATEWAY_NOT_READY"),
                Does.Contain("서버"));
            Assert.That(
                UI.VoiceCommandFeedView.DescribeError("MIC_REQUIRES_HTTPS"),
                Does.Contain("https"));

            // The detail suffix must not defeat the lookup.
            Assert.That(
                UI.VoiceCommandFeedView.DescribeError("VOICE_AUDIO_SILENT:peak=0.0012"),
                Does.Contain("볼륨"));

            // An unmapped code is still shown rather than swallowed.
            Assert.That(
                UI.VoiceCommandFeedView.DescribeError("SOMETHING_NEW"),
                Is.EqualTo("SOMETHING_NEW"));
        }

        [Test]
        public void CompatibilityAliasesDoNotCreateAdditionalRuntimeStates()
        {
            Assert.That(
                VoiceCommandInputState.CommandAccepted,
                Is.EqualTo(VoiceCommandInputState.Completed));
            Assert.That(
                VoiceCommandInputState.CommandConfused,
                Is.EqualTo(VoiceCommandInputState.Failed));
            Assert.That(
                VoiceCommandInputState.Uploading,
                Is.EqualTo(VoiceCommandInputState.Uploading));
        }
    }
}
