using NUnit.Framework;
using PawsAndLoot.TechnicalValidation;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class TechnicalRoleAssignmentTests
    {
        [Test]
        public void ServerAndRemoteClientReceiveDifferentRoles()
        {
            Assert.That(
                TechnicalRoleAssignment.GetRole(0, 0),
                Is.EqualTo(TechnicalPlayerRole.Police));
            Assert.That(
                TechnicalRoleAssignment.GetRole(1, 0),
                Is.EqualTo(TechnicalPlayerRole.Thief));
        }

        [Test]
        public void RoleSpawnPositionsAreDistinct()
        {
            float distance = UnityEngine.Vector3.Distance(
                TechnicalRoleAssignment.GetSpawnPosition(
                    TechnicalPlayerRole.Police),
                TechnicalRoleAssignment.GetSpawnPosition(
                    TechnicalPlayerRole.Thief));

            Assert.That(distance, Is.GreaterThan(2f));
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        public void ApprovalAllowsAtMostTwoPlayers(
            int connectedClientCount,
            bool expected)
        {
            Assert.That(
                TechnicalRoleAssignment.CanApprove(connectedClientCount),
                Is.EqualTo(expected));
        }
    }
}
