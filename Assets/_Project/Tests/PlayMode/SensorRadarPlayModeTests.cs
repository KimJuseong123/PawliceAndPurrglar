using System.Collections;
using System.Linq;
using NUnit.Framework;
using PawsAndLoot.Core;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// Walk a thief into a sensor and check the officer is actually told, in the
    /// real scene.
    ///
    /// This is the test that was missing when the sensor was reported as not
    /// detecting. It was detecting: the trap fired, the reveal ran and the thief
    /// became visible. What failed was the part that says <em>where</em> — the
    /// pointer looked for a sensor still flashing, and the host removes a spent
    /// sensor immediately, so it found nothing and drew nothing.
    ///
    /// Asserting the direction rather than "something appeared", because the
    /// direction is the whole reason the indicator exists.
    /// </summary>
    public sealed class SensorRadarPlayModeTests
    {
        [UnityTest]
        public IEnumerator TrippingASensorPointsTheOfficerAtIt()
        {
            yield return SceneManager.LoadSceneAsync(
                GameSceneCatalog.GetPath(GameSceneId.Game),
                LoadSceneMode.Single);
            yield return null;

            Object.FindFirstObjectByType<MatchRuntimeState>()
                .TryTransitionTo(MatchState.Playing);
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            yield return null;

            PlayerRoleIdentity[] players = Object
                .FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None);
            PlayerRoleIdentity thief =
                players.First(p => p.Role == PlayerRole.Thief);
            PlayerRoleIdentity police =
                players.First(p => p.Role == PlayerRole.Police);

            foreach (PlayerRoleIdentity player in players)
            {
                CharacterController controller =
                    player.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }

            // The officer stands at the origin; the sensor goes due east of them.
            police.transform.position = Vector3.zero;
            var sensorSpot = new Vector3(8f, 0f, 0f);
            PawsAndLoot.Integration.Network.NetworkItemCoordinator.Place(
                ThrowableKind.SensorLight,
                PlayerRole.Police,
                sensorSpot);
            yield return null;

            // The thief walks onto it.
            thief.transform.position = sensorSpot;
            Physics.SyncTransforms();

            // A few frames for the host's own sweep to notice.
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            var visibility =
                Object.FindFirstObjectByType<FlashlightVisibility>();
            Assert.That(
                visibility.IsRevealed,
                Is.True,
                "Walking into a sensor has to trip it.");
            Assert.That(
                visibility.RevealSource,
                Is.EqualTo(sensorSpot).Using<Vector3>(
                    (a, b) => Vector3.Distance(a, b) < 0.5f ? 0 : 1),
                "The reveal has to remember where it came from, or there is "
                + "nothing to point at once the sensor is gone.");

            var radar = Object.FindFirstObjectByType<
                PawsAndLoot.UI.SensorRadarPresenter>();
            Assert.That(radar, Is.Not.Null);
            radar.Refresh(0.02f);

            Assert.That(
                radar.IsShowing,
                Is.True,
                "The officer's indicator has to appear. It looked for a sensor "
                + "still flashing, and a spent sensor is removed at once.");

            // Due east of the officer is a quarter turn. Asserted as a bearing
            // rather than a screen position, because the direction is the point.
            Assert.That(
                Mathf.DeltaAngle(radar.CurrentAngle, -90f),
                Is.EqualTo(0f).Within(5f),
                $"A sensor due east should read as -90°, not "
                + $"{radar.CurrentAngle:0.#}°.");

            // Closeness is read as a count of arcs, so nearer has to light more.
            // Eight metres away is close.
            int nearBars = radar.LitBarCount;
            Assert.That(
                nearBars,
                Is.GreaterThanOrEqualTo(4),
                "Four arcs is the floor; fewer does not read as a signal.");

            // Now trip one from across the map and check it reads weaker.
            visibility.RevealFor(
                ThrowableCatalog.RevealSeconds,
                police.transform.position + new Vector3(0f, 0f, 40f));
            radar.Refresh(0.02f);

            Assert.That(
                radar.LitBarCount,
                Is.LessThan(nearBars),
                $"A sensor 40 m away lit {radar.LitBarCount} arcs and one 8 m "
                + $"away lit {nearBars}. Distance has to read as a count, or "
                + "the officer cannot tell near from far.");
            Assert.That(
                Mathf.DeltaAngle(radar.CurrentAngle, 0f),
                Is.EqualTo(0f).Within(5f),
                "And due north has to read as straight up.");

            // And the sensor survives long enough to flash rather than vanishing
            // the instant it fires.
            Assert.That(
                Object.FindObjectsByType<
                        PawsAndLoot.Animation.PlacedTrapView>(
                        FindObjectsSortMode.None)
                    .Any(view => view.IsFlashing),
                Is.True,
                "The lamp has to light. Destroying the sensor the moment it "
                + "fired took its lamp with it.");

            LocalPlayerRoleSelector.ClearOverriddenRole();
        }
    }
}
