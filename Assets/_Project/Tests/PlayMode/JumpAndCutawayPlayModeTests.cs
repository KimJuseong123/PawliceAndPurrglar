using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Animation;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Camera;
using PawsAndLoot.Gameplay.Interiors;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Getting over the furniture, and seeing past the walls.
    ///
    /// Both came out of the same complaint about being indoors: the rooms are the
    /// house model at 2.2x, so a sofa is a metre tall and there is a wall between the
    /// camera and the player at most angles. A jump that does not clear anything and
    /// a wall that never moves are both failures you can only see by looking, so they
    /// are measured here instead.
    /// </summary>
    public sealed class JumpAndCutawayPlayModeTests
    {
        /// <summary>
        /// Batch-mode frames last a fraction of a millisecond, so a jump that takes
        /// under a second would not get off the ground across a hundred of them.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
        }

        private static IEnumerator EnterFirstInterior(
            PlayerRoleIdentity player,
            HouseInterior interior)
        {
            HouseDoorway entrance = Object
                .FindObjectsByType<HouseDoorway>(FindObjectsSortMode.None)
                .First(door =>
                    door.LeadsInside
                    && door.Interior == interior
                    && door.Side == HouseDoorSide.Front);
            Assert.That(
                entrance.TryInteract(new PlayerInteractionContext(player)),
                Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator JumpingLeavesTheGroundAndClearsTheFurniture()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(p => p.Role == PlayerRole.Thief);
            var motor = thief.GetComponent<PlayerMovementMotor>();
            var controller = thief.GetComponent<CharacterController>();
            Assert.That(motor, Is.Not.Null);

            // Let it settle onto the street first: a jump is only allowed from the
            // ground, which is the whole reason it cannot be used to climb.
            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            float standing = controller.bounds.min.y;
            Assert.That(
                motor.IsAirborne,
                Is.False,
                "The thief is not on the ground to begin with.");

            Assert.That(
                motor.TryJump(),
                Is.True,
                "A grounded character has to be able to jump.");

            float highest = standing;
            for (int frame = 0; frame < 90; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
                highest = Mathf.Max(highest, controller.bounds.min.y);
            }

            // High enough to matter: enough to get onto a sofa base, which is a
            // metre tall in a room built at 2.2x.
            float clearance = highest - standing;
            Assert.That(
                clearance,
                Is.GreaterThan(0.8f),
                $"The jump only cleared {clearance:0.00} m, which is not enough "
                + "to get onto anything in a room built at 2.2x.");

            // And came back down.
            for (int frame = 0; frame < 120; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                motor.IsAirborne,
                Is.False,
                "The thief never landed.");
            Assert.That(
                controller.bounds.min.y,
                Is.EqualTo(standing).Within(0.15f),
                "The thief came down somewhere other than where they took off.");
        }

        [UnityTest]
        public IEnumerator AJumpInMidAirIsRefused()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(p => p.Role == PlayerRole.Thief);
            var motor = thief.GetComponent<PlayerMovementMotor>();

            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(motor.TryJump(), Is.True);
            for (int frame = 0; frame < 8; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                motor.TryJump(),
                Is.False,
                "Jumping again in mid-air would let a player climb anything by "
                + "pressing the key repeatedly.");
        }

        /// <summary>
        /// The pose changes while airborne.
        ///
        /// "Comical" is not something a test can check. "Not the same pose as
        /// standing still" is, and that is the failure that would actually happen —
        /// the limbs simply never being told, which is how the leg animator was
        /// silently doing nothing on clients once before (ISSUE-026).
        /// </summary>
        [UnityTest]
        public IEnumerator TheLimbsTakeAJumpPoseWhileAirborne()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(p => p.Role == PlayerRole.Thief);
            var motor = thief.GetComponent<PlayerMovementMotor>();
            var limbs = thief.GetComponent<CompanionLegAnimator>();
            Assert.That(
                limbs,
                Is.Not.Null,
                "No limb animator, so this proves nothing.");
            Assert.That(
                limbs.LegCount,
                Is.GreaterThan(0),
                "No limbs found on the rig.");

            for (int frame = 0; frame < 30; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                limbs.AirborneBlend,
                Is.LessThan(0.05f),
                "Standing on the ground already looks like a jump.");

            motor.TryJump();
            for (int frame = 0; frame < 20; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                limbs.AirborneBlend,
                Is.GreaterThan(0.6f),
                "The limbs never took the jump pose. This is the failure where "
                + "nothing tells the animator anything and the jump is invisible.");

            for (int frame = 0; frame < 150; frame++)
            {
                motor.Move(Vector2.zero, Time.deltaTime);
                yield return null;
            }

            Assert.That(
                limbs.AirborneBlend,
                Is.LessThan(0.05f),
                "The pose stayed on after landing.");
        }

        [UnityTest]
        public IEnumerator AWallBetweenTheCameraAndThePlayerGetsOutOfTheWay()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;

            PlayerRoleIdentity thief = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None)
                .First(p => p.Role == PlayerRole.Thief);
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            var cutaway =
                Object.FindFirstObjectByType<InteriorCutawayView>();
            Assert.That(
                cutaway,
                Is.Not.Null,
                "No cutaway view in the scene.");

            HouseInterior interior =
                Object.FindFirstObjectByType<HouseInterior>();
            InteriorOccluder[] panels =
                interior.GetComponentsInChildren<InteriorOccluder>(true);
            Assert.That(
                panels.Length,
                Is.GreaterThanOrEqualTo(8),
                "The walls are not registered as things that can move aside.");

            // Nothing hidden while outdoors: the street camera is fixed and tuned,
            // and quietly deleting buildings in it is not what was asked for.
            cutaway.Tick();
            Assert.That(
                cutaway.HiddenCount,
                Is.Zero,
                "Walls are being removed outdoors.");

            yield return EnterFirstInterior(thief, interior);

            // Let the interior camera get behind the player, then look.
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
            }

            Assert.That(
                cutaway.HiddenCount,
                Is.GreaterThan(0),
                "Indoors the camera sits outside the room's own walls, so at "
                + "least one of them must be out of the way.");

            // The player is still drawn. Hiding them would be the one thing worse
            // than the wall.
            Renderer[] playerParts =
                thief.GetComponentsInChildren<Renderer>(true);
            Assert.That(
                playerParts.Any(r => r.enabled),
                Is.True,
                "The character was hidden along with the walls.");

            // And the wall that came out is genuinely between the camera and them.
            UnityEngine.Camera view = UnityEngine.Camera.main;
            InteriorOccluder hiddenPanel = panels.First(p => p.IsHidden);
            Vector3 toPlayer =
                thief.transform.position + Vector3.up * 1.1f
                - view.transform.position;
            Vector3 toPanel =
                hiddenPanel.View.bounds.center - view.transform.position;
            Assert.That(
                Vector3.Dot(toPanel.normalized, toPlayer.normalized),
                Is.GreaterThan(0.3f),
                $"'{hiddenPanel.name}' was removed but is not in front of the "
                + "camera at all.");

            // Back on the way out.
            LocalPlayerRoleSelector.ClearOverriddenRole();
            cutaway.enabled = false;
            yield return null;
            Assert.That(
                panels.All(p => !p.IsHidden),
                Is.True,
                "A wall stayed invisible after the view was switched off, which "
                + "is not something a player can work around.");
        }
    }
}
