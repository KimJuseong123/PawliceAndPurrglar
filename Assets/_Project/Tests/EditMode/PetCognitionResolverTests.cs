using PawliceAndPurrglar.Companions;
using NUnit.Framework;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class PetCognitionResolverTests
    {
        [Test]
        public void DogTakesHighConfidenceCandidate()
        {
            var resolver = new PetCognitionResolver();
            var context = new PetCognitionContext
            {
                petKind = CompanionKind.Dog,
                commandSequence = 4,
                classification = new VoiceIntentClassificationResult
                {
                    candidates = new[]
                    {
                        new VoiceIntentCandidate
                        {
                            intent = "SEARCH_AREA",
                            confidence = 0.95f,
                            targetId = "box-1"
                        }
                    }
                }
            };

            PetDecision decision = resolver.Resolve(context, 1234);

            Assert.That(decision.resultType, Is.EqualTo(PetCommandResultType.Correct));
            Assert.That(decision.selectedCommandId, Is.EqualTo(CompanionCommandId.Search));
        }

        [Test]
        public void SameSeedAndInputProduceSameDecision()
        {
            var resolver = new PetCognitionResolver();
            var context = new PetCognitionContext
            {
                petKind = CompanionKind.Cat,
                commandSequence = 2,
                classification = new VoiceIntentClassificationResult
                {
                    candidates = new[]
                    {
                        new VoiceIntentCandidate
                        {
                            intent = "FETCH_OBJECT",
                            confidence = 0.45f
                        }
                    }
                }
            };

            PetDecision first = resolver.Resolve(context, 99);
            PetDecision second = resolver.Resolve(context, 99);

            Assert.That(second.resultType, Is.EqualTo(first.resultType));
            Assert.That(second.reaction, Is.EqualTo(first.reaction));
            Assert.That(second.selectedCommandId, Is.EqualTo(first.selectedCommandId));
        }
    }
}
