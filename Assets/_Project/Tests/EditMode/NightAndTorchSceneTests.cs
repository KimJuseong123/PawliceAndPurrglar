using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Animation;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PawsAndLoot.Tests.EditMode
{
    /// <summary>
    /// The night has to stay playable and the torch has to stay honest.
    ///
    /// Both of these went wrong once already and neither showed up in a test.
    /// The first night pass was legible in a screenshot and oppressive to play,
    /// and the torch's light opened narrower than the rule that hides the thief,
    /// so there was a band where the thief was invisible over lit ground. These
    /// assert the properties rather than the numbers, so retuning the look does
    /// not have to fight the tests but re-breaking the relationship does.
    /// </summary>
    public sealed class NightAndTorchSceneTests
    {
        [Test]
        public void TorchLightMatchesTheRuleThatHidesTheThief()
        {
            Scene scene = OpenGameScene();
            Light beam = FindAll<Light>(scene)
                .FirstOrDefault(light => light.type == LightType.Spot);

            Assert.That(
                beam,
                Is.Not.Null,
                "The officer's torch is the only spot light in the scene.");
            Assert.That(
                beam.spotAngle,
                Is.EqualTo(FlashlightCone.SpotAngleDegrees).Within(0.01f),
                "The lit floor is how a player judges the cone. If the light "
                + "opens narrower than the visibility rule, the thief appears "
                + "over ground that was never lit.");
            Assert.That(
                beam.range,
                Is.EqualTo(FlashlightCone.RangeMeters).Within(0.01f),
                "Same for reach: a shorter light than rule makes the thief "
                + "materialise out of the dark at the edge.");
        }

        [Test]
        public void TorchReachIsDrawnOnTheGroundForBothPlayers()
        {
            Scene scene = OpenGameScene();
            FlashlightConeView[] views = FindAll<FlashlightConeView>(scene);

            Assert.That(
                views.Length,
                Is.EqualTo(1),
                "One wedge, on the officer. It is drawn for both players to "
                + "read, but only one of them carries a torch.");
        }

        [Test]
        public void EveryPlayerCanShowAThrowAndAStun()
        {
            Scene scene = OpenGameScene();
            PlayerRoleIdentity[] players =
                FindAll<PlayerRoleIdentity>(scene);

            Assert.That(players.Length, Is.EqualTo(2));
            foreach (PlayerRoleIdentity player in players)
            {
                Assert.That(
                    player.GetComponent<ThrowPresenter>(),
                    Is.Not.Null,
                    $"{player.Role} has no throw to show. A stun that "
                    + "appears with no arm swing and no rock reads as the "
                    + "game freezing.");
                Assert.That(
                    player.GetComponent<StunStarsView>(),
                    Is.Not.Null,
                    $"{player.Role} would be stunned with no sign of it.");
                Assert.That(
                    player.GetComponent<NightVisionFill>(),
                    Is.Not.Null,
                    $"{player.Role} has no night adaptation.");
            }
        }

        /// <summary>
        /// The asymmetry itself, not the numbers behind it. The thief is the one
        /// being hunted in the dark and this is their compensation for it, so a
        /// retune that quietly equalises the two is a regression even if both
        /// values look reasonable.
        /// </summary>
        [Test]
        public void TheThiefSeesBetterInTheDarkThanThePolice()
        {
            Scene scene = OpenGameScene();
            float thief = 0f;
            float police = 0f;

            foreach (PlayerRoleIdentity player in
                FindAll<PlayerRoleIdentity>(scene))
            {
                Light fill = player
                    .GetComponentsInChildren<Light>(true)
                    .FirstOrDefault(light =>
                        light.type == LightType.Directional);
                Assert.That(
                    fill,
                    Is.Not.Null,
                    $"{player.Role} has no night vision fill light.");
                if (player.Role == PlayerRole.Thief)
                {
                    thief = fill.intensity;
                }
                else
                {
                    police = fill.intensity;
                }
            }

            Assert.That(thief, Is.GreaterThan(police));
            Assert.That(
                police,
                Is.GreaterThan(0f),
                "The police still needs some dark adaptation. A torch and "
                + "nothing else leaves them unable to cross their own town.");
        }

        /// <summary>
        /// A floor under the night, because the first pass was measurably too
        /// dark to play and nothing caught it. Deliberately a floor and not an
        /// exact value: the look is still being tuned and street lamps are
        /// coming, but the map has to stay readable without them.
        /// </summary>
        [Test]
        public void TheNightStaysBrightEnoughToNavigate()
        {
            Scene scene = OpenGameScene();
            Light moon = FindAll<Light>(scene)
                .FirstOrDefault(light =>
                    light.type == LightType.Directional
                    && light.transform.parent == null);

            Assert.That(
                moon,
                Is.Not.Null,
                "The moonlight is the scene's own directional light; the "
                + "per-player fills are parented to a player.");
            Assert.That(
                moon.intensity,
                Is.GreaterThanOrEqualTo(0.5f),
                "0.32 read as night in a screenshot and was oppressive to "
                + "actually play in: on a fixed camera an unreadable town "
                + "means losing track of where you are, not feeling hunted.");
            Assert.That(
                RenderSettings.ambientLight.grayscale,
                Is.GreaterThanOrEqualTo(0.15f),
                "Ambient is what keeps unlit walls and alley faces off solid "
                + "black.");
        }

        /// <summary>
        /// Nothing a player has to walk up to may be sealed inside geometry.
        ///
        /// This is the test that was missing. Four of the five rocks were placed
        /// inside buildings — two in shop bodies, two inside houses — and every
        /// existing check passed: the scene built, MAP-001 validated, and the log
        /// cheerfully reported five pickups placed. It took playing the game to
        /// find out only one of them could be picked up.
        ///
        /// Asserted for every pickup rather than the rocks specifically, so the
        /// banana off the shop shelf inherits the guard for free.
        /// </summary>
        [Test]
        public void NoPickupIsSealedInsideGeometry()
        {
            Scene scene = OpenGameScene();
            Physics.SyncTransforms();

            foreach (PawsAndLoot.Gameplay.Items.ThrowablePickup pickup in
                FindAll<PawsAndLoot.Gameplay.Items.ThrowablePickup>(scene))
            {
                // Above the road surface and below waist height: the ground and
                // the road tiles are not obstacles, a wall or a shop body is.
                Collider[] blockers = Physics
                    .OverlapSphere(
                        pickup.transform.position + Vector3.up * 0.05f,
                        0.25f,
                        Physics.AllLayers,
                        QueryTriggerInteraction.Collide)
                    .Where(collider =>
                        collider != null
                        && !collider.transform.IsChildOf(
                            pickup.transform))
                    .ToArray();

                Assert.That(
                    blockers,
                    Is.Empty,
                    $"{pickup.name} at {pickup.transform.position} is inside "
                    + $"'{blockers.FirstOrDefault()?.name}'. A pickup nobody "
                    + "can reach is indistinguishable from one that does not "
                    + "work.");
            }
        }

        private static Scene OpenGameScene()
        {
            return EditorSceneManager.OpenScene(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                OpenSceneMode.Single);
        }

        private static T[] FindAll<T>(Scene scene)
            where T : Component
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(true))
                .ToArray();
        }
    }
}
