using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using PawliceAndPurrglar.Core;
using PawliceAndPurrglar.Gameplay.Interiors;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// Where the thief's capsule actually is while they are hidden, at every
    /// hiding spot in the town.
    ///
    /// The existing hide test asserts the three flags — cannot move, not drawn,
    /// not arrestable — one frame after the press, and every one of them stays
    /// true all the way down. Nothing looked at the position, and the renderers
    /// are off while hiding, so a thief under the world is invisible in every
    /// sense: no exception, no log, and a suite that passes.
    ///
    /// Measured on the capsule's underside, never on the transform. The pivot is
    /// the capsule's centre — a metre above the soles — so a pivot that looks
    /// like it is standing on the ground is a capsule buried to the waist, which
    /// is the whole mechanism of this bug (<c>ISSUE-040</c> for the third time).
    ///
    /// The reference height is the floor the thief was standing on when they
    /// pressed the key, not a raycast. A ray dropped onto a hiding spot lands on
    /// the bin — the bins are 2.08 m of solid collider — and answers with the
    /// height of the lid. The thief has to be within 1.4 m to interact at all, so
    /// where they stood is a known-good floor, and it is the same number the fix
    /// clamps to.
    /// </summary>
    public sealed class HidingSpotFootingPlayModeTests
    {
        /// <summary>
        /// How far under the floor the soles may sit. The controller's own skin
        /// is 0.08 and a character resolved onto a surface settles a little into
        /// it; a metre is the bug.
        /// </summary>
        private const float AllowedSinkMetres = 0.2f;

        private const int HeldFrames = 30;

        [TearDown]
        public void TearDown()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        /// <summary>
        /// Leaves an empty scene behind rather than the town.
        ///
        /// A fixture that stops with the map loaded hands the next one a scene
        /// full of buildings, and the next one along may create a bare player at
        /// the origin and assert that it can walk. See
        /// <see cref="MatchSceneInstallerPlayModeTests"/>, where exactly that
        /// happened.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDownScene()
        {
            Scene loaded = SceneManager.GetActiveScene();
            if (!loaded.name.Equals(
                    GameSceneCatalog.GetName(GameSceneId.Game),
                    System.StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            Scene empty = SceneManager.CreateScene(
                $"HidingFootingCleanup{_cleanups++}");
            SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(loaded);
        }

        private static int _cleanups;

        /// <summary>
        /// Every spot, and the frame of the press counts.
        ///
        /// The same-frame sample is the one that fails on the old code: entering
        /// put the soles at −1.05 m inside the crates and −0.69 m inside the bins
        /// before anything had a chance to resolve it. Sampling only from the
        /// next frame onwards hides the defect behind the controller's rescue,
        /// which is exactly how it survived.
        /// </summary>
        [UnityTest]
        public IEnumerator HidingNeverPutsTheThiefUnderTheGround()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return LoadPlayingScene();

            PlayerRoleIdentity thief = Player(PlayerRole.Thief);
            var hiding = thief.GetComponent<ThiefHidingState>();
            var controller = thief.GetComponent<CharacterController>();
            Assert.That(hiding, Is.Not.Null, "The thief cannot hide at all.");
            Assert.That(controller, Is.Not.Null);

            // Ordered by name so the report reads the same way twice and a
            // failure names a spot somebody can go and look at. Instance order is
            // not a property of the map.
            PlayerHidingSpot[] spots = Object
                .FindObjectsByType<PlayerHidingSpot>(FindObjectsSortMode.None)
                .OrderBy(spot => spot.name)
                .ThenBy(spot => spot.transform.position.x)
                .ToArray();
            Assert.That(
                spots.Length,
                Is.GreaterThanOrEqualTo(4),
                "The town has nowhere to hide, so this test proves nothing.");

            var report = new StringBuilder();
            var sunk = new StringBuilder();
            foreach (PlayerHidingSpot spot in spots)
            {
                float floor = thief.transform.position.y
                    - InteriorTravel.FeetToPivot(controller, thief.transform);

                Assert.That(
                    spot.TryInteract(new PlayerInteractionContext(thief)),
                    Is.True,
                    $"E at '{spot.name}' did nothing.");

                // Before any yield. The controller has not been asked to move
                // yet, so this is the placement itself rather than the placement
                // plus whatever physics made of it.
                float lowestSole = SoleHeight(thief, controller);
                float onTheFrameOfThePress = lowestSole;
                for (int frame = 0; frame < HeldFrames; frame++)
                {
                    yield return null;
                    lowestSole = Mathf.Min(
                        lowestSole,
                        SoleHeight(thief, controller));
                }

                report.AppendLine(
                    $"  {spot.name}: floor {floor:0.00}, "
                    + $"press {onTheFrameOfThePress:0.00}, "
                    + $"lowest {lowestSole:0.00}");
                if (lowestSole < floor - AllowedSinkMetres)
                {
                    sunk.AppendLine(
                        $"  {spot.name}: soles {lowestSole:0.00} against a floor "
                        + $"of {floor:0.00} ({floor - lowestSole:0.00} m under, "
                        + $"{onTheFrameOfThePress:0.00} on the frame of the press)");
                }

                Assert.That(
                    spot.TryInteract(new PlayerInteractionContext(thief)),
                    Is.True,
                    $"E did not get the thief back out of '{spot.name}'.");
                yield return null;
                Assert.That(hiding.IsHiding, Is.False);

                // On their feet again before the next spot, or one failure reads
                // as several.
                Assert.That(
                    SoleHeight(thief, controller),
                    Is.GreaterThan(floor - AllowedSinkMetres),
                    $"The thief came out of '{spot.name}' below the ground.");
            }

            Debug.Log($"[HIDE-001] hiding footing:\n{report}");
            Assert.That(
                sunk.ToString(),
                Is.Empty,
                $"The hidden thief is under the ground at:\n{sunk}");
        }

        /// <summary>
        /// A long frame while hidden moves them nowhere.
        ///
        /// 0.333 s is <c>Time.maximumDeltaTime</c>, the longest step Unity hands
        /// out, and a browser tab that has just loaded a scene hands out exactly
        /// that. The motor used to keep applying gravity through every frame of
        /// hiding — <c>CanMove</c> gates the direction, not the fall — so the
        /// question "how far can one frame move a hidden thief" had an answer
        /// other than zero.
        /// </summary>
        [UnityTest]
        public IEnumerator ALongFrameWhileHiddenMovesTheThiefNowhere()
        {
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);
            yield return LoadPlayingScene();

            PlayerRoleIdentity thief = Player(PlayerRole.Thief);
            var controller = thief.GetComponent<CharacterController>();
            var motor = thief.GetComponent<PlayerMovementMotor>();
            PlayerHidingSpot spot = Object
                .FindObjectsByType<PlayerHidingSpot>(FindObjectsSortMode.None)
                .OrderBy(candidate => candidate.name)
                .Last();

            Assert.That(
                spot.TryInteract(new PlayerInteractionContext(thief)),
                Is.True,
                $"E at '{spot.name}' did nothing.");
            yield return null;

            Vector3 parked = thief.transform.position;
            for (int frame = 0; frame < 3; frame++)
            {
                motor.Move(Vector2.zero, Time.maximumDeltaTime);
                yield return null;
            }

            float drift = Vector3.Distance(parked, thief.transform.position);
            Debug.Log(
                $"[HIDE-001] three long frames at '{spot.name}': drift "
                + $"{drift:0.000} m, soles {SoleHeight(thief, controller):0.00}");
            Assert.That(
                drift,
                Is.LessThan(0.05f),
                $"Three long frames moved the hidden thief {drift:0.00} m.");
        }

        /// <summary>
        /// The underside of the capsule.
        ///
        /// Computed from the transform and the capsule's own numbers rather than
        /// read from <c>bounds</c>, because the fix switches the controller off
        /// while hidden and a disabled collider's bounds are stale — which would
        /// report the old position and pass for the wrong reason.
        /// </summary>
        private static float SoleHeight(
            PlayerRoleIdentity thief,
            CharacterController controller)
        {
            return thief.transform.position.y
                - InteriorTravel.FeetToPivot(controller, thief.transform);
        }

        private static IEnumerator LoadPlayingScene()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            yield return null;
        }

        private static PlayerRoleIdentity Player(PlayerRole role)
        {
            return Object
                .FindObjectsByType<PlayerRoleIdentity>(FindObjectsSortMode.None)
                .First(identity => identity.Role == role);
        }
    }
}
