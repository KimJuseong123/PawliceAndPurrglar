using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.TechnicalValidation;
using NUnit.Framework;
using UnityEngine;

namespace PawsAndLoot.Tests.EditMode
{
    public sealed class ThrowInteractionValidationTests
    {
        [Test]
        public void ThrowStateContainsRequiredValidationStates()
        {
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Idle));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Aiming));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Charging));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Ready));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Throwing));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Cooldown));
            Assert.That(System.Enum.IsDefined(typeof(ThrowState), ThrowState.Cancelled));
        }

        [Test]
        public void SolverProducesLandingForSelectedThrowRange()
        {
            ThrowTrajectorySolver.Solution solution =
                ThrowTrajectorySolver.Solve(
                    new Vector3(0f, 1.9f, 0f),
                    Vector3.forward,
                    ThrowableCatalog.MinimumThrowRangeMeters,
                    0,
                    0f);

            Assert.That(solution.FlightTime, Is.GreaterThan(0f));
            Assert.That(solution.TravelledTime, Is.EqualTo(solution.FlightTime));
            Assert.That(solution.Landing.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(solution.Landing.z, Is.EqualTo(4f).Within(0.001f));
            Assert.That(solution.Blocked, Is.False);
        }

        [Test]
        public void TrajectoryPreviewUsesSerializedResourceMaterials()
        {
            GameObject previewObject = new("Trajectory Preview Test");
            ThrowTrajectoryPreview preview =
                previewObject.AddComponent<ThrowTrajectoryPreview>();

            preview.Show(
                new Vector3(0f, 1.9f, 0f),
                Vector3.forward,
                ThrowableCatalog.ThrowRangeMeters,
                0);

            Assert.That(preview.Line, Is.Not.Null);
            Assert.That(preview.Line.enabled, Is.True);
            Assert.That(preview.Line.positionCount, Is.GreaterThan(1));
            AssertBuildSafeMaterial(preview.Line.sharedMaterial);

            Assert.That(preview.LandingMarker, Is.Not.Null);
            Assert.That(preview.LandingMarker.activeSelf, Is.True);
            Renderer landingRenderer =
                preview.LandingMarker.GetComponent<Renderer>();
            Assert.That(landingRenderer, Is.Not.Null);
            AssertBuildSafeMaterial(landingRenderer.sharedMaterial);

            preview.Hide();
            Assert.That(preview.Line.enabled, Is.False);
            Assert.That(preview.LandingMarker.activeSelf, Is.False);

            Object.DestroyImmediate(previewObject);
        }

        [Test]
        public void HoldTargetUsesHoldContractAndActionText()
        {
            GameObject playerObject = new("Validation Test Player");
            PlayerRoleIdentity identity =
                playerObject.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Police);

            GameObject targetObject = new("Validation Drawer");
            ValidationInteractionTarget target =
                targetObject.AddComponent<ValidationInteractionTarget>();
            target.Configure(ValidationInteractionKind.Drawer, 0.5f);

            PlayerInteractionContext context =
                new(identity);
            Assert.That(target.RequiresHold, Is.True);
            Assert.That(target.Prompt, Is.EqualTo("Search"));
            Assert.That(target.CanBeginHold(context), Is.True);
            Assert.That(target.CompleteHold(context), Is.True);
            Assert.That(target.InteractionCount, Is.EqualTo(1));

            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(playerObject);
        }

        private static void AssertBuildSafeMaterial(Material material)
        {
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.isSupported, Is.True);
            Assert.That(
                material.shader.name,
                Does.Not.Contain("InternalError"));
        }
    }
}
