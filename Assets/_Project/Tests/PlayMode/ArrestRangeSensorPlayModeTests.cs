using System;
using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Arrest;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class ArrestRangeSensorPlayModeTests
    {
        [UnityTest]
        public IEnumerator DetectionChecksRangeObstacleAndEvents()
        {
            ArrestFixture fixture = CreateFixture();
            int entered = 0;
            int exited = 0;
            fixture.Sensor.TargetEntered += _ => entered++;
            fixture.Sensor.TargetExited += _ => exited++;

            fixture.Thief.transform.position =
                new Vector3(3f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Sensor.IsTargetDetected, Is.False);

            fixture.Thief.transform.position =
                new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Sensor.IsTargetDetected, Is.True);
            Assert.That(entered, Is.EqualTo(1));

            GameObject wall = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            wall.name = "Sight Obstacle";
            wall.transform.position = new Vector3(0.5f, 1f, 0f);
            wall.transform.localScale =
                new Vector3(0.2f, 3f, 2f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Sensor.IsTargetDetected, Is.False);
            Assert.That(exited, Is.EqualTo(1));

            UnityEngine.Object.Destroy(wall);
            DestroyFixture(fixture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidOrInactiveTargetsAreNotDetected()
        {
            ArrestFixture fixture = CreateFixture();
            fixture.Thief.transform.position =
                new Vector3(1f, 0f, 0f);
            Physics.SyncTransforms();
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Sensor.IsTargetDetected, Is.True);

            fixture.Thief.SetActive(false);
            fixture.Sensor.Evaluate();
            Assert.That(fixture.Sensor.IsTargetDetected, Is.False);

            Assert.Throws<InvalidOperationException>(
                () => fixture.Sensor.Configure(
                    fixture.PoliceIdentity,
                    fixture.PoliceIdentity,
                    fixture.Config,
                    Physics.AllLayers));

            DestroyFixture(fixture);
            yield return null;
        }

        private static ArrestFixture CreateFixture()
        {
            GameObject police = CreatePlayer(
                "Police",
                PlayerRole.Police);
            GameObject thief = CreatePlayer(
                "Thief",
                PlayerRole.Thief);
            thief.transform.position = new Vector3(3f, 0f, 0f);
            ArrestConfig config =
                ScriptableObject.CreateInstance<ArrestConfig>();

            police.SetActive(false);
            ArrestRangeSensor sensor =
                police.AddComponent<ArrestRangeSensor>();
            PlayerRoleIdentity policeIdentity =
                police.GetComponent<PlayerRoleIdentity>();
            PlayerRoleIdentity thiefIdentity =
                thief.GetComponent<PlayerRoleIdentity>();
            sensor.Configure(
                policeIdentity,
                thiefIdentity,
                config,
                Physics.AllLayers);
            police.SetActive(true);
            return new ArrestFixture(
                police,
                thief,
                policeIdentity,
                config,
                sensor);
        }

        private static GameObject CreatePlayer(
            string name,
            PlayerRole role)
        {
            var player = new GameObject(name);
            player.SetActive(false);
            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;
            controller.center = Vector3.up;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            player.SetActive(true);
            return player;
        }

        private static void DestroyFixture(ArrestFixture fixture)
        {
            UnityEngine.Object.Destroy(fixture.Config);
            UnityEngine.Object.Destroy(fixture.Police);
            UnityEngine.Object.Destroy(fixture.Thief);
        }

        private readonly struct ArrestFixture
        {
            public ArrestFixture(
                GameObject police,
                GameObject thief,
                PlayerRoleIdentity policeIdentity,
                ArrestConfig config,
                ArrestRangeSensor sensor)
            {
                Police = police;
                Thief = thief;
                PoliceIdentity = policeIdentity;
                Config = config;
                Sensor = sensor;
            }

            public GameObject Police { get; }
            public GameObject Thief { get; }
            public PlayerRoleIdentity PoliceIdentity { get; }
            public ArrestConfig Config { get; }
            public ArrestRangeSensor Sensor { get; }
        }
    }
}
