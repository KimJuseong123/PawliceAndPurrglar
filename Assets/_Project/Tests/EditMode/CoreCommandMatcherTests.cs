using NUnit.Framework;
using PawliceAndPurrglar.Companions;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class CoreCommandMatcherTests
    {
        [TestCase("강아지 냄새 추적", CompanionCommandId.Track)]
        [TestCase("지금 자리를 지켜", CompanionCommandId.Guard)]
        [TestCase("도둑을 쫓아가", CompanionCommandId.Track)]
        public void MatchesDogCoreCommands(
            string text,
            CompanionCommandId expected)
        {
            CoreCommandMatch result = CoreCommandMatcher.Match(
                text,
                CompanionKind.Dog);

            Assert.That(result.CommandId, Is.EqualTo(expected));
            Assert.That(result.Accepted, Is.True);
        }

        [TestCase("지붕으로 올라가", CompanionCommandId.Steal)]
        [TestCase("할퀴어", CompanionCommandId.Distract)]
        [TestCase("소리 질러", CompanionCommandId.Distract)]
        [TestCase("은신처를 찾아", CompanionCommandId.Hide)]
        public void MatchesCatCoreCommands(
            string text,
            CompanionCommandId expected)
        {
            CoreCommandMatch result = CoreCommandMatcher.Match(
                text,
                CompanionKind.Cat);

            Assert.That(result.CommandId, Is.EqualTo(expected));
            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public void BiteIsRejectedWhenNoBiteExecutorExists()
        {
            CoreCommandMatch result = CoreCommandMatcher.Match(
                "물어",
                CompanionKind.Dog);

            Assert.That(result.CommandId, Is.EqualTo(CompanionCommandId.None));
            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public void UnknownSpeechIsReportedAsMisunderstood()
        {
            CoreCommandMatch result = CoreCommandMatcher.Match(
                "오늘 날씨가 좋다",
                CompanionKind.Dog);

            Assert.That(result.Accepted, Is.False);
            Assert.That(
                result.Understanding,
                Is.EqualTo(VoiceUnderstanding.Misunderstood));
        }
    }
}
