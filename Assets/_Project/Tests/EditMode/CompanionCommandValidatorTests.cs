using NUnit.Framework;
using PawsAndLoot.Companions;
using PawsAndLoot.Gameplay.Players;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class CompanionCommandValidatorTests
    {
        [TestCase(CompanionCommandId.Track, PlayerRole.Police)]
        [TestCase(CompanionCommandId.Distract, PlayerRole.Thief)]
        public void ImplementedRoleCommandIsAccepted(
            CompanionCommandId commandId,
            PlayerRole role)
        {
            CompanionCommandFailure failure =
                CompanionCommandValidator.Validate(
                    commandId,
                    role,
                    true,
                    true,
                    true,
                    true,
                    true);

            Assert.That(
                failure,
                Is.EqualTo(CompanionCommandFailure.None));
        }

        [TestCase(CompanionCommandId.Track, PlayerRole.Thief)]
        [TestCase(CompanionCommandId.Distract, PlayerRole.Police)]
        public void OtherRolesCommandIsRejected(
            CompanionCommandId commandId,
            PlayerRole role)
        {
            CompanionCommandFailure failure =
                CompanionCommandValidator.Validate(
                    commandId,
                    role,
                    true,
                    true,
                    true,
                    true,
                    true);

            Assert.That(
                failure,
                Is.EqualTo(CompanionCommandFailure.WrongRole));
        }

        [Test]
        public void DeclaredButUnimplementedCommandIsRejected()
        {
            CompanionCommandFailure failure =
                CompanionCommandValidator.Validate(
                    CompanionCommandId.Guard,
                    PlayerRole.Police,
                    true,
                    true,
                    true,
                    true,
                    true);

            Assert.That(
                failure,
                Is.EqualTo(
                    CompanionCommandFailure.UnsupportedCommand));
        }

        [Test]
        public void UnreachableTargetIsRejected()
        {
            CompanionCommandFailure failure =
                CompanionCommandValidator.Validate(
                    CompanionCommandId.Track,
                    PlayerRole.Police,
                    true,
                    true,
                    true,
                    true,
                    false);

            Assert.That(
                failure,
                Is.EqualTo(
                    CompanionCommandFailure.TargetUnreachable));
        }
    }
}
