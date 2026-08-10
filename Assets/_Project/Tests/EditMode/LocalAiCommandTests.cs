using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Integration.Voice;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class LocalAiCommandTests
    {
        [Test]
        public void VoiceAliasesMapToExistingDogCommands()
        {
            Assert.That(
                VoiceCommandMapper.TryMap("TRACK_SCENT", CompanionKind.Dog, out CompanionCommandId track),
                Is.True);
            Assert.That(track, Is.EqualTo(CompanionCommandId.Track));
            Assert.That(
                VoiceCommandMapper.TryMap("BITE", CompanionKind.Dog, out _),
                Is.False);
        }

        [Test]
        public void DogFilterIsDeterministicForFixedSeed()
        {
            DogBehaviorProfile profile = ScriptableObject.CreateInstance<DogBehaviorProfile>();
            var filter = new DogBehaviorFilter(profile);
            var context = new DogBehaviorContext(
                new[] { "BONE" },
                false,
                false,
                7);

            DogBehaviorDecision first = filter.Decide(
                CompanionCommandId.Track,
                "THIEF_02",
                0.9f,
                context);
            DogBehaviorDecision second = filter.Decide(
                CompanionCommandId.Track,
                "THIEF_02",
                0.9f,
                context);

            Assert.That(first.ActualCommand, Is.EqualTo(second.ActualCommand));
            Assert.That(first.MistakeType, Is.EqualTo(second.MistakeType));
            Object.DestroyImmediate(profile);
        }
    }
}
