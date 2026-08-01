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
            Assert.That(input.LastResult.interpretedCommand, Is.EqualTo("DOG SCENT TRACK"));

            input.ApplyServerFailure("VOICE_PROVIDER_MISSING");
            Assert.That(input.State, Is.EqualTo(VoiceCommandInputState.Failed));
            Assert.That(input.LastError, Is.EqualTo("VOICE_PROVIDER_MISSING"));

            Object.DestroyImmediate(root);
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
