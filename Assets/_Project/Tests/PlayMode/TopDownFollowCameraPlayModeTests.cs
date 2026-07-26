using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Camera;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class TopDownFollowCameraPlayModeTests
    {
        private static readonly Vector3 SouthOffset = new(0f, 16f, -14f);

        [UnityTest]
        public IEnumerator CameraKeepsFixedRotationWhileTargetMoves()
        {
            var targetObject = new GameObject("Target");
            var cameraObject = new GameObject("Chase Camera");
            TopDownFollowCamera camera =
                cameraObject.AddComponent<TopDownFollowCamera>();
            camera.Configure(targetObject.transform, SouthOffset, 0.12f);

            Quaternion expected = camera.FixedRotation;
            Assert.That(
                Quaternion.Angle(cameraObject.transform.rotation, expected),
                Is.LessThan(0.01f));

            // A due-south camera looks along +Z, so world axes double as the
            // screen axes that WASD is expressed in.
            Assert.That(expected.eulerAngles.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(expected.eulerAngles.x, Is.GreaterThan(0f));

            Vector3[] directions =
            {
                Vector3.forward,
                Vector3.right,
                Vector3.back,
                Vector3.left,
                new(1f, 0f, 1f),
                new(-1f, 0f, -1f)
            };

            foreach (Vector3 direction in directions)
            {
                for (int step = 0; step < 12; step++)
                {
                    targetObject.transform.position +=
                        direction.normalized * 0.6f;
                    camera.Tick(0.016f);
                    Assert.That(
                        Quaternion.Angle(
                            cameraObject.transform.rotation,
                            expected),
                        Is.LessThan(0.01f),
                        $"Camera yawed while the target moved {direction}.");
                }
            }

            // The smoothed position still converges on the fixed offset.
            for (int step = 0; step < 400; step++)
            {
                camera.Tick(0.016f);
            }

            Assert.That(
                Vector3.Distance(
                    cameraObject.transform.position,
                    targetObject.transform.position + SouthOffset),
                Is.LessThan(0.05f));

            Object.Destroy(cameraObject);
            Object.Destroy(targetObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapToTargetUsesTheSameFixedRotation()
        {
            var targetObject = new GameObject("Target");
            var cameraObject = new GameObject("Chase Camera");
            TopDownFollowCamera camera =
                cameraObject.AddComponent<TopDownFollowCamera>();
            camera.Configure(targetObject.transform, SouthOffset, 0.12f);
            Quaternion expected = camera.FixedRotation;

            targetObject.transform.position = new Vector3(20f, 0f, -18f);
            cameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            camera.SnapToTarget();

            Assert.That(
                Quaternion.Angle(cameraObject.transform.rotation, expected),
                Is.LessThan(0.01f));
            Assert.That(
                Vector3.Distance(
                    cameraObject.transform.position,
                    targetObject.transform.position + SouthOffset),
                Is.LessThan(0.001f));

            Object.Destroy(cameraObject);
            Object.Destroy(targetObject);
            yield return null;
        }
    }
}
