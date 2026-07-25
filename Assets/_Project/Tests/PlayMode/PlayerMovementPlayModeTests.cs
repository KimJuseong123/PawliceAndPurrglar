using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Camera;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class PlayerMovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator PoliceMovesOnlyDuringPlayingAndHitsWall()
        {
            GameObject floor = CreateBox(
                "Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(20f, 0.5f, 20f));
            GameObject wall = CreateBox(
                "Wall",
                new Vector3(0f, 1f, 2f),
                new Vector3(5f, 2f, 0.5f));
            var state = new MutableMatchStateReader();
            PlayerMovementMotor motor = CreateMotor(
                "Police",
                new Vector3(0f, 1f, 0f),
                state);
            Physics.SyncTransforms();

            Simulate(motor, Vector2.up, 60, 1f / 60f);
            Assert.That(motor.transform.position.z, Is.EqualTo(0f).Within(0.05f));

            state.IsGameplayActive = true;
            Simulate(motor, Vector2.up, 180, 1f / 60f);
            Assert.That(motor.transform.position.z, Is.GreaterThan(0.5f));
            Assert.That(motor.transform.position.z, Is.LessThan(1.45f));

            Object.Destroy(motor.gameObject);
            Object.Destroy(wall);
            Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovementIsFrameIndependentAndClimbsSmallStep()
        {
            GameObject floor = CreateBox(
                "Floor",
                new Vector3(0f, -0.25f, 0f),
                new Vector3(30f, 0.5f, 20f));
            GameObject step = CreateBox(
                "Step",
                new Vector3(0f, 0.125f, 1.5f),
                new Vector3(3f, 0.25f, 1f));
            var state = new MutableMatchStateReader
            {
                IsGameplayActive = true
            };
            PlayerMovementMotor slowTicks = CreateMotor(
                "Slow Ticks",
                new Vector3(-2f, 1f, -2f),
                state);
            PlayerMovementMotor fastTicks = CreateMotor(
                "Fast Ticks",
                new Vector3(2f, 1f, -2f),
                state);

            Simulate(slowTicks, Vector2.up, 30, 1f / 30f);
            Simulate(fastTicks, Vector2.up, 120, 1f / 120f);
            Assert.That(
                slowTicks.transform.position.z,
                Is.EqualTo(fastTicks.transform.position.z).Within(0.12f));

            PlayerMovementMotor stepMotor = CreateMotor(
                "Step Motor",
                new Vector3(0f, 1f, 0f),
                state);
            float highestY = stepMotor.transform.position.y;
            for (int index = 0; index < 90; index++)
            {
                stepMotor.Move(Vector2.up, 1f / 60f);
                highestY = Mathf.Max(
                    highestY,
                    stepMotor.transform.position.y);
            }

            Assert.That(highestY, Is.GreaterThan(1.1f));
            Assert.That(stepMotor.transform.position.z, Is.GreaterThan(3f));

            Object.Destroy(stepMotor.gameObject);
            Object.Destroy(fastTicks.gameObject);
            Object.Destroy(slowTicks.gameObject);
            Object.Destroy(step);
            Object.Destroy(floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerspectiveCameraFollowsPoliceTarget()
        {
            var target = new GameObject("Police Target");
            var cameraObject = new GameObject(
                "Follow Camera",
                typeof(UnityEngine.Camera));
            UnityEngine.Camera camera =
                cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            TopDownFollowCamera follow =
                cameraObject.AddComponent<TopDownFollowCamera>();
            follow.Configure(
                target.transform,
                new Vector3(0f, 16f, -14f),
                0.05f);

            target.transform.position = new Vector3(5f, 0f, 0f);
            for (int index = 0; index < 10; index++)
            {
                follow.Tick(1f / 60f);
            }

            Assert.That(camera.orthographic, Is.False);
            Assert.That(camera.transform.position.x, Is.GreaterThan(1f));

            Object.Destroy(cameraObject);
            Object.Destroy(target);
            yield return null;
        }

        private static PlayerMovementMotor CreateMotor(
            string name,
            Vector3 position,
            IMatchStateReader matchState)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.position = position;
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 45f;

            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, config, matchState, null);
            root.SetActive(true);
            return motor;
        }

        private static GameObject CreateBox(
            string name,
            Vector3 position,
            Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            return box;
        }

        private static void Simulate(
            PlayerMovementMotor motor,
            Vector2 input,
            int frameCount,
            float deltaTime)
        {
            for (int index = 0; index < frameCount; index++)
            {
                motor.Move(input, deltaTime);
            }
        }

        private sealed class MutableMatchStateReader : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }
    }
}
