using NUnit.Framework;
using PawsAndLoot.Companions;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class CoreCommandMatcherTests
    {
        [TestCase("강아지 냄새 추적", CompanionCommandId.DogScentTrack)]
        [TestCase("지금 자리를 지켜", CompanionCommandId.DogGuard)]
        [TestCase("도둑을 쫓아가", CompanionCommandId.DogChase)]
        [TestCase("물어", CompanionCommandId.DogBite)]
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

        [TestCase("지붕으로 올라가", CompanionCommandId.CatClimbRoof)]
        [TestCase("할퀴어", CompanionCommandId.CatScratch)]
        [TestCase("소리 질러", CompanionCommandId.CatScream)]
        [TestCase("은신처를 찾아", CompanionCommandId.CatFindHideout)]
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
