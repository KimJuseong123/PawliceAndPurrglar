using System.Text;
using NUnit.Framework;
using PawsAndLoot.Voice;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class WavEncoderTests
    {
        [Test]
        public void EncodeWritesPcm16WavHeaderAndSamples()
        {
            AudioClip clip = AudioClip.Create(
                "Voice Test",
                4,
                1,
                16000,
                false);
            clip.SetData(
                new[] { -1f, -0.5f, 0.5f, 1f },
                0);

            byte[] wav = WavEncoder.Encode(clip, 4);

            Assert.That(wav, Has.Length.EqualTo(52));
            Assert.That(
                Encoding.ASCII.GetString(wav, 0, 4),
                Is.EqualTo("RIFF"));
            Assert.That(
                Encoding.ASCII.GetString(wav, 8, 4),
                Is.EqualTo("WAVE"));
            Assert.That(
                Encoding.ASCII.GetString(wav, 36, 4),
                Is.EqualTo("data"));
            Assert.That(
                System.BitConverter.ToInt32(wav, 40),
                Is.EqualTo(8));

            Object.DestroyImmediate(clip);
        }

        [Test]
        public void EncodeClampsRequestedFramesToClipLength()
        {
            AudioClip clip = AudioClip.Create(
                "Voice Test",
                2,
                2,
                16000,
                false);

            byte[] wav = WavEncoder.Encode(clip, 99);

            Assert.That(wav, Has.Length.EqualTo(52));
            Assert.That(
                System.BitConverter.ToInt32(wav, 40),
                Is.EqualTo(8));

            Object.DestroyImmediate(clip);
        }
    }
}
