using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    public sealed class CompanionCommandValidatorTests
    {
        /// <summary>
        /// A dog command that genuinely walks to the place it was told about.
        ///
        /// `Track` used to be the default and is no longer a targeted command —
        /// its resolver reads the scent trail and ignores the request's target,
        /// so the target rules below would silently stop being exercised.
        /// </summary>
        private static CompanionCommandRequest DogRequest(
            CompanionCommandId commandId =
                CompanionCommandId.Search,
            Vector3? target = null)
        {
            return new CompanionCommandRequest(
                commandId,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f,
                null,
                target ?? new Vector3(3f, 0f, 0f));
        }

        private static CompanionCommandValidator.Context Ready(
            float cooldown = 0f,
            bool reachable = true)
        {
            return new CompanionCommandValidator.Context(
                true,
                true,
                true,
                cooldown,
                reachable);
        }

        [Test]
        public void AcceptsAValidCommand()
        {
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    Ready(),
                    out CompanionCommandRejection rejection),
                Is.True);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.None));
        }

        [Test]
        public void RejectsWhenTheMatchIsNotPlaying()
        {
            var context = new CompanionCommandValidator.Context(
                false,
                true,
                true,
                0f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    context,
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.MatchNotPlaying));
        }

        [Test]
        public void RejectsACatCommandIssuedByThePolice()
        {
            var request = new CompanionCommandRequest(
                CompanionCommandId.Distract,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f,
                null,
                Vector3.zero);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    request,
                    Ready(),
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.WrongFaction));
        }

        [Test]
        public void RejectsWhenTheCompanionIsMissingOrDisabled()
        {
            var missing = new CompanionCommandValidator.Context(
                true,
                false,
                true,
                0f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    missing,
                    out CompanionCommandRejection missingReason),
                Is.False);
            Assert.That(
                missingReason,
                Is.EqualTo(CompanionCommandRejection.CompanionMissing));

            var disabled = new CompanionCommandValidator.Context(
                true,
                true,
                false,
                0f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    disabled,
                    out CompanionCommandRejection disabledReason),
                Is.False);
            Assert.That(
                disabledReason,
                Is.EqualTo(CompanionCommandRejection.CompanionDisabled));
        }

        [Test]
        public void RejectsWhileOnCooldown()
        {
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    Ready(1.2f),
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.OnCooldown));
        }

        [Test]
        public void RejectsAnUnknownCommandBeforeAnythingElse()
        {
            var request = new CompanionCommandRequest(
                CompanionCommandId.None,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f);
            var context = new CompanionCommandValidator.Context(
                false,
                false,
                false,
                5f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    request,
                    context,
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.UnknownCommand));
        }

        /// <summary>
        /// `Search`, not `Track`.
        ///
        /// Track used to stand here, and it was the wrong example: its resolver
        /// reads the scent trail and never looks at the request's target, so
        /// demanding one only made the command unreachable by voice — no voice
        /// path can supply a target. Search genuinely walks to the place it was
        /// told about, so it is the honest case for this rule.
        /// </summary>
        [Test]
        public void RejectsATargetedCommandWithoutADestination()
        {
            var request = new CompanionCommandRequest(
                CompanionCommandId.Search,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    request,
                    Ready(),
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.TargetMissing));
        }

        [Test]
        public void AcceptsBarkWithoutADestination()
        {
            var request = new CompanionCommandRequest(
                CompanionCommandId.Bark,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f);
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    request,
                    Ready(),
                    out CompanionCommandRejection rejection),
                Is.True);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.None));
        }

        [Test]
        public void RejectsAnUnreachableTarget()
        {
            Assert.That(
                CompanionCommandValidator.TryValidate(
                    DogRequest(),
                    Ready(0f, false),
                    out CompanionCommandRejection rejection),
                Is.False);
            Assert.That(
                rejection,
                Is.EqualTo(CompanionCommandRejection.TargetUnreachable));
        }

        [TestCase(PlayerRole.Police, 1, CompanionCommandId.Track)]
        [TestCase(PlayerRole.Police, 4, CompanionCommandId.Bark)]
        [TestCase(PlayerRole.Police, 5, CompanionCommandId.None)]
        [TestCase(PlayerRole.Thief, 1, CompanionCommandId.Scout)]
        [TestCase(PlayerRole.Thief, 2, CompanionCommandId.Distract)]
        [TestCase(PlayerRole.Thief, 0, CompanionCommandId.None)]
        public void NumberKeysMapToTheOwningFactionCommands(
            PlayerRole role,
            int numberKey,
            CompanionCommandId expected)
        {
            Assert.That(
                CompanionCommandCatalog.FromNumberKey(role, numberKey),
                Is.EqualTo(expected));
        }

        [Test]
        public void EachFactionOwnsItsOwnCompanionKind()
        {
            Assert.That(
                CompanionCommandCatalog.GetCompanionKind(PlayerRole.Police),
                Is.EqualTo(CompanionKind.Dog));
            Assert.That(
                CompanionCommandCatalog.GetCompanionKind(PlayerRole.Thief),
                Is.EqualTo(CompanionKind.Cat));
        }

        [Test]
        public void DestinationPrefersTheLiveEntityOverAStalePoint()
        {
            var entity = new GameObject("Target").transform;
            entity.position = new Vector3(5f, 0f, 5f);
            var request = new CompanionCommandRequest(
                CompanionCommandId.Track,
                PlayerRole.Police,
                CompanionKind.Dog,
                CompanionCommandInputSource.Keyboard,
                1f,
                entity,
                Vector3.zero);

            Assert.That(
                request.TryGetDestination(out Vector3 destination),
                Is.True);
            Assert.That(destination, Is.EqualTo(new Vector3(5f, 0f, 5f)));
            Object.DestroyImmediate(entity.gameObject);
        }
    }
}
