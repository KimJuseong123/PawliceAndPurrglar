using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// A stun stops a player moving and then gives them back. The re-stun guard
    /// is the part worth holding onto: without it whoever has the most rocks can
    /// keep the other player frozen indefinitely, and losing control is only
    /// funny when it ends.
    /// </summary>
    public sealed class StunPlayModeTests
    {
        [UnityTest]
        public IEnumerator StunStopsMovementAndWearsOff()
        {
            Fixture fixture = Create();

            // Moves normally to begin with.
            Vector3 start = fixture.Root.transform.position;
            fixture.Motor.Move(Vector2.up, 0.1f);
            Assert.That(
                fixture.Root.transform.position,
                Is.Not.EqualTo(start),
                "The player has to be moving before a stun means anything.");

            Assert.That(fixture.Stun.TryApply(0.3f), Is.True);
            Assert.That(fixture.Motor.CanMove, Is.False);
            Assert.That(fixture.Motor.IsStunned, Is.True);

            Vector3 frozen = fixture.Root.transform.position;
            for (int step = 0; step < 3; step++)
            {
                fixture.Motor.Move(Vector2.up, 0.02f);
            }

            // Horizontal only. The capsule still falls under gravity while
            // stunned, which is correct — being held still is not the same as
            // hanging in the air — so vertical travel is not the measure.
            Vector3 travelled =
                fixture.Root.transform.position - frozen;
            travelled.y = 0f;
            Assert.That(
                travelled.magnitude,
                Is.LessThan(0.01f),
                "A stunned player must not walk.");
            Assert.That(
                fixture.Motor.LastPlanarVelocity,
                Is.EqualTo(Vector3.zero),
                "The motor should report no intent to move at all.");

            yield return WaitSeconds(0.5f);

            Assert.That(
                fixture.Stun.IsStunned,
                Is.False,
                "The stun has to end on its own.");
            Assert.That(fixture.Motor.CanMove, Is.True);

            Object.Destroy(fixture.Root);
        }

        [UnityTest]
        public IEnumerator ASecondStunIsRefusedUntilTheGuardExpires()
        {
            Fixture fixture = Create();

            Assert.That(fixture.Stun.TryApply(0.2f), Is.True);
            Assert.That(
                fixture.Stun.TryApply(0.2f),
                Is.False,
                "Stacking a stun onto a running one would extend it for free.");

            yield return WaitSeconds(0.3f);
            Assert.That(fixture.Stun.IsStunned, Is.False);
            Assert.That(
                fixture.Stun.IsImmune,
                Is.True,
                "Immediately after a stun the player is briefly immune, or a "
                + "second throw chains straight into another lockout.");
            Assert.That(fixture.Stun.TryApply(0.2f), Is.False);

            Object.Destroy(fixture.Root);
        }

        [UnityTest]
        public IEnumerator AMissingStunComponentLeavesThePlayerMobile()
        {
            // The guard reads as "no stun component means never stunned". If it
            // ever inverted, every player without one would be frozen forever.
            var state = new MutableMatchState { IsGameplayActive = true };
            var root = new GameObject("Player");
            root.SetActive(false);
            CharacterController controller =
                root.AddComponent<CharacterController>();
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, config, state, null);
            root.SetActive(true);

            yield return null;

            Assert.That(motor.IsStunned, Is.False);
            Assert.That(motor.CanMove, Is.True);

            Object.Destroy(config);
            Object.Destroy(root);
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState =>
                IsGameplayActive ? MatchState.Playing : MatchState.Lobby;
            public bool IsGameplayActive { get; set; }
        }

        private sealed class Fixture
        {
            public GameObject Root;
            public PlayerMovementMotor Motor;
            public StunState Stun;
            public PlayerConfig Config;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private static Fixture Create()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            var root = new GameObject("Player");
            root.SetActive(false);
            CharacterController controller =
                root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.up;
            PlayerConfig config =
                ScriptableObject.CreateInstance<PlayerConfig>();
            PlayerMovementMotor motor =
                root.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, config, state, null);
            StunState stun = root.AddComponent<StunState>();
            root.SetActive(true);

            return new Fixture
            {
                Root = root,
                Motor = motor,
                Stun = stun,
                Config = config
            };
        }
    }
}
