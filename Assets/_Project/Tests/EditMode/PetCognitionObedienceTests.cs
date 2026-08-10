using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Config;
using UnityEditor;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// Whether a clearly heard order can be obeyed at all.
    ///
    /// This is asserted against the **shipped configuration asset**, not against
    /// numbers written here, because the defect was arithmetic between the two:
    /// obedience was multiplied into the comprehension score *and* used for the
    /// disobey roll, so with `catObedience 0.52` and `catCorrectThreshold 0.78`
    /// the best score the cat could reach was 0.42 and the correct branch was
    /// unreachable on every roll, for every player, forever. Nothing threw and
    /// the transcript still appeared on screen, so it read as "the cat hears me
    /// and does nothing".
    ///
    /// A test that built its own config would have kept passing through all of
    /// that. This one fails if anybody edits the asset back into a state where
    /// the animal cannot obey.
    /// </summary>
    public sealed class PetCognitionObedienceTests
    {
        private const string ConfigPath =
            "Assets/_Project/Settings/Configs/PetCognitionConfig.asset";

        /// <summary>
        /// Enough seeds that a 52% chance failing every single time is not the
        /// explanation for a failure here.
        /// </summary>
        private const int Seeds = 200;

        [Test]
        public void AClearlyHeardOrderCanBeObeyedByBothAnimals()
        {
            PetCognitionConfig config = LoadConfig();
            foreach (CompanionKind kind in
                new[] { CompanionKind.Dog, CompanionKind.Cat })
            {
                int obeyed = CountObeyed(config, kind, 1f);
                Assert.That(
                    obeyed,
                    Is.GreaterThan(0),
                    $"The {kind} cannot obey a perfectly heard order on any of "
                    + $"{Seeds} rolls, so the command is unreachable rather than "
                    + "unlikely.");
            }
        }

        /// <summary>
        /// The dog is the reliable one and the cat is not. Both halves matter:
        /// a cat that always obeys is a dog, and the numbers in the asset are
        /// meant to read as "about half of what you say gets done".
        /// </summary>
        [Test]
        public void TheDogIsReliableAndTheCatIsNot()
        {
            PetCognitionConfig config = LoadConfig();
            int dog = CountObeyed(config, CompanionKind.Dog, 1f);
            int cat = CountObeyed(config, CompanionKind.Cat, 1f);

            Assert.That(
                dog,
                Is.GreaterThan((int)(Seeds * 0.8f)),
                $"The dog obeyed {dog}/{Seeds} clear orders.");
            Assert.That(
                cat,
                Is.LessThan(dog),
                $"The cat obeyed {cat}/{Seeds} and the dog {dog}/{Seeds}.");
            Assert.That(
                cat,
                Is.GreaterThan((int)(Seeds * 0.25f)),
                $"The cat obeyed only {cat}/{Seeds} clear orders, which is not "
                + "'wilful' any more.");
        }

        /// <summary>
        /// The five safety words are absolute for both animals: they resolve
        /// without the speech model, so a player who has lost their animal can
        /// always call it back.
        /// </summary>
        [Test]
        public void TheSafetyWordsAreAlwaysObeyed()
        {
            PetCognitionConfig config = LoadConfig();
            var resolver = new PetCognitionResolver(config);
            for (int seed = 0; seed < Seeds; seed++)
            {
                PetDecision decision = resolver.Resolve(
                    Context(CompanionKind.Cat, "STOP", 1f, exactMatched: true),
                    seed);
                Assert.That(
                    decision.resultType,
                    Is.EqualTo(PetCommandResultType.Correct),
                    $"'STOP' was not obeyed on seed {seed}.");
                Assert.That(
                    decision.selectedCommandId,
                    Is.EqualTo(CompanionCommandId.Stop));
            }
        }

        private static int CountObeyed(
            PetCognitionConfig config,
            CompanionKind kind,
            float confidence)
        {
            var resolver = new PetCognitionResolver(config);
            string intent = kind == CompanionKind.Dog
                ? "CHASE_TARGET"
                : "FETCH_OBJECT";
            int obeyed = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                PetDecision decision = resolver.Resolve(
                    Context(kind, intent, confidence, exactMatched: false),
                    seed);

                // Obeyed means an order actually left the resolver. A decision
                // with no command id dispatches nothing, which is the shape the
                // bug wore: a result on screen and an animal that never moved.
                if (decision.resultType == PetCommandResultType.Correct
                    && decision.selectedCommandId != CompanionCommandId.None)
                {
                    obeyed++;
                }
            }

            return obeyed;
        }

        private static PetCognitionContext Context(
            CompanionKind kind,
            string intent,
            float confidence,
            bool exactMatched)
        {
            return new PetCognitionContext
            {
                petKind = kind,
                classification = new VoiceIntentClassificationResult
                {
                    normalizedText = intent,
                    exactMatched = exactMatched,
                    candidates = new[]
                    {
                        new VoiceIntentCandidate
                        {
                            intent = intent,
                            confidence = confidence
                        }
                    }
                }
            };
        }

        private static PetCognitionConfig LoadConfig()
        {
            var config =
                AssetDatabase.LoadAssetAtPath<PetCognitionConfig>(ConfigPath);
            Assert.That(config, Is.Not.Null, ConfigPath);
            return config;
        }
    }
}
