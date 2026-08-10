using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PawliceAndPurrglar.Animation;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// Three item effects that worked and said nothing.
    ///
    /// The banana, the glue trap and the sensor light have all been in the game
    /// for weeks. The first two were drawn as the rock's four stars and the third
    /// was drawn on the other player's screen only — so from inside the character
    /// all three were "I stopped, or something, maybe".
    ///
    /// These assert the screen rather than the component's opinion of the screen.
    /// <c>IsShowing</c> was true for the whole life of the stun stars while the
    /// mesh was wound backwards and every triangle was being culled, which is the
    /// reason this file reads transforms and alphas.
    /// </summary>
    public sealed class SlipSpinAndTrapStatusPlayModeTests
    {
        private readonly List<GameObject> _spawned = new();

        private GameObject Track(GameObject spawned)
        {
            _spawned.Add(spawned);
            return spawned;
        }

        /// <summary>
        /// Clears the town an earlier test left standing.
        ///
        /// Several tests in this assembly load the real <c>Game</c> scene with
        /// <c>LoadSceneMode.Single</c> and never unload it, so its two players
        /// are still here when this class starts. That would be harmless except
        /// that a reveal is a **wall-clock** window — <c>Time.time + 2.5</c> —
        /// and the whole Play Mode run takes about a minute, so the officer's
        /// watcher is frequently still revealed from the sensor-radar test. The
        /// overlay then correctly reports a reveal that belongs to somebody
        /// else's test, and this class fails for a reason that is not about it.
        ///
        /// Only the watchers go. Anything that reloads the scene gets them back.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            LocalPlayerRoleSelector.ClearOverriddenRole();
            foreach (FlashlightVisibility leftover in
                Object.FindObjectsByType<FlashlightVisibility>(
                    FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover);
            }

            foreach (StunState leftover in
                Object.FindObjectsByType<StunState>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover);
            }

            // And the identities, because the banner finds the local player by
            // role. A leftover officer answers to `Police` just as well as the
            // one this test built, and whichever the scan reaches first is the
            // one the banner then watches — so the test drives its own motor
            // and reads somebody else's.
            foreach (PlayerRoleIdentity leftover in
                Object.FindObjectsByType<PlayerRoleIdentity>(
                    FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy is deferred to the end of the frame, so a player left by
            // one test is still standing in the next one.
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();

            foreach (ScriptableObject config in _configs)
            {
                if (config != null)
                {
                    Object.DestroyImmediate(config);
                }
            }

            _configs.Clear();

            // The local role outlives a scene load. Left set, it decides which
            // character the next test's overlay follows.
            LocalPlayerRoleSelector.ClearOverriddenRole();
        }

        /// <summary>
        /// A player, with the visual child the spin turns.
        /// </summary>
        private GameObject Player(PlayerRole role, out StunState stun)
        {
            var player = Track(new GameObject($"{role} Player"));
            player.AddComponent<PlayerRoleIdentity>().Configure(role);
            stun = player.AddComponent<StunState>();

            var visual = new GameObject("VisualRoot");
            visual.transform.SetParent(player.transform, false);
            return player;
        }

        /// <summary>
        /// The banana's whole joke is the fall. A second of not moving with four
        /// stars overhead is the rock's drawing, and the rock is a different
        /// thing that happens to take the same second.
        /// </summary>
        [UnityTest]
        public IEnumerator AbananaSpinsTheCharacterInsteadOfStarringThem()
        {
            GameObject player = Player(PlayerRole.Police, out StunState stun);
            StunStarsView stars = player.AddComponent<StunStarsView>();
            stars.Configure(stun, null);
            SlipSpinView spin = player.AddComponent<SlipSpinView>();
            spin.Configure(stun, player.transform.Find("VisualRoot"));
            yield return null;

            Assert.That(
                stun.TryApply(
                    ThrowableCatalog.GetStunSeconds(ThrowableKind.Banana),
                    ThrowableCatalog.GetStunCause(ThrowableKind.Banana)),
                Is.True);
            yield return null;

            Assert.That(
                stun.IsStunned,
                Is.True,
                "The rule is untouched: the second of control still goes.");

            Assert.That(
                stars.IsShowing,
                Is.False,
                "Stars say somebody hit you, and nobody did.");

            Assert.That(spin.IsSpinning, Is.True);

            // Halfway through the second, the character has actually turned.
            // Asking IsSpinning alone would pass with the transform untouched.
            stun.Tick(ThrowableCatalog.BananaSlipSeconds * 0.5f);
            yield return null;

            float yaw = spin.Visual.localRotation.eulerAngles.y;
            Assert.That(
                yaw,
                Is.GreaterThan(60f).And.LessThan(300f),
                "Halfway through the turn the character faces away from where "
                + $"they started; it read {yaw:0}°.");

            Assert.That(
                Quaternion.Angle(
                    player.transform.rotation,
                    Quaternion.identity),
                Is.LessThan(0.01f),
                "The player root is where the motor writes heading. Spinning "
                + "it would leave the character facing a random direction when "
                + "the stun ended, which reads as eaten input.");

            // And it ends upright. A character left leaning is worse than no
            // effect at all, and nothing else would ever straighten them.
            stun.Clear();

            // Counted in seconds, not frames. Batch mode with -nographics runs
            // frames as fast as it can, so twenty of them can be twenty
            // milliseconds — a frame budget here passes on a developer's machine
            // and fails on the build server for reasons that have nothing to do
            // with the recovery.
            float elapsed = 0f;
            while (elapsed < 1.5f
                && Quaternion.Angle(
                    spin.Visual.localRotation,
                    Quaternion.identity) > 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.That(spin.IsSpinning, Is.False);
            Assert.That(
                Quaternion.Angle(
                    spin.Visual.localRotation,
                    Quaternion.identity),
                Is.LessThan(0.5f),
                "Back to upright after the slip.");
        }

        /// <summary>
        /// A rock still puts stars up and does not spin anybody. The split is
        /// only worth having if the old drawing survived it.
        /// </summary>
        [UnityTest]
        public IEnumerator ArockStillDrawsTheStars()
        {
            GameObject player = Player(PlayerRole.Thief, out StunState stun);
            StunStarsView stars = player.AddComponent<StunStarsView>();
            stars.Configure(stun, null);
            SlipSpinView spin = player.AddComponent<SlipSpinView>();
            spin.Configure(stun, player.transform.Find("VisualRoot"));
            yield return null;

            Assert.That(
                stun.TryApply(
                    ThrowableCatalog.GetStunSeconds(ThrowableKind.Rock),
                    ThrowableCatalog.GetStunCause(ThrowableKind.Rock)),
                Is.True);
            yield return null;

            Assert.That(stars.IsShowing, Is.True);
            Assert.That(stars.StarCount, Is.EqualTo(4));
            Assert.That(spin.IsSpinning, Is.False);
            Assert.That(
                Quaternion.Angle(
                    spin.Visual.localRotation,
                    Quaternion.identity),
                Is.LessThan(0.01f));
        }

        /// <summary>
        /// Glue draws the stars too.
        ///
        /// When the causes were split, glue fell between them: the banana got
        /// the spin, the rock kept the stars, and standing in glue got nothing
        /// over the character at all. The screen-edge banner was the only sign,
        /// and it is easy to miss while looking at your feet — which is where
        /// you look when you have stopped moving (`ISSUE-073`).
        ///
        /// The banana stays the exception, because it has a drawing of its own.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGlueTrapDrawsTheStarsAsWell()
        {
            GameObject player = Player(PlayerRole.Thief, out StunState stun);
            StunStarsView stars = player.AddComponent<StunStarsView>();
            stars.Configure(stun, null);
            SlipSpinView spin = player.AddComponent<SlipSpinView>();
            spin.Configure(stun, player.transform.Find("VisualRoot"));
            yield return null;

            Assert.That(
                stun.TryApply(
                    ThrowableCatalog.GetStunSeconds(ThrowableKind.GlueTrap),
                    ThrowableCatalog.GetStunCause(ThrowableKind.GlueTrap)),
                Is.True);
            yield return null;

            Assert.That(stun.Cause, Is.EqualTo(StunCause.Stuck));
            Assert.That(stars.IsShowing, Is.True);
            Assert.That(stars.StarCount, Is.EqualTo(4));

            // Stuck, not spun. Two cartoons for one event would read as two
            // things having happened.
            Assert.That(spin.IsSpinning, Is.False);
        }

        /// <summary>
        /// Being stuck to the floor is a fact about you, and the only place it
        /// was written was four stars that mean something else.
        /// </summary>
        [UnityTest]
        public IEnumerator TheGlueTrapMarksTheScreenOfWhoeverStoodInIt()
        {
            Player(PlayerRole.Police, out StunState policeStun);
            Player(PlayerRole.Thief, out StunState thiefStun);

            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            var overlayObject = Track(new GameObject("Player Status Banner"));
            PlayerStatusBannerView overlay =
                overlayObject.AddComponent<PlayerStatusBannerView>();
            yield return null;

            // The officer stands in their own glue. Nothing happens to the
            // thief, whose screen this is.
            Assert.That(
                policeStun.TryApply(
                    ThrowableCatalog.GlueHoldSeconds,
                    StunCause.Stuck),
                Is.True);
            overlay.Refresh(1f);

            Assert.That(
                overlay.Showing,
                Is.Null,
                "Both role objects exist on both machines. Taking the first "
                + "StunState found banners the player who was not caught.");

            Assert.That(
                thiefStun.TryApply(
                    ThrowableCatalog.GlueHoldSeconds,
                    StunCause.Stuck),
                Is.True);
            overlay.Refresh(1f);

            Assert.That(
                overlay.Showing,
                Is.EqualTo(PlayerStatusBannerView.Status.Stuck));
            Assert.That(
                overlay.Alpha,
                Is.GreaterThan(0.9f),
                "A banner that is drawn at alpha zero is the failure this "
                + "whole file exists for.");
            Assert.That(
                overlay.LabelText,
                Does.Contain("끈끈이"),
                "The player has to be told which prop, or the next three "
                + "seconds teach them nothing.");

            // And it goes away. Left up, it would report the last trap for the
            // rest of the match.
            thiefStun.Clear();
            overlay.Refresh(1f);
            Assert.That(overlay.Showing, Is.Null);
            Assert.That(overlay.Alpha, Is.LessThan(0.01f));
        }

        /// <summary>
        /// The sensor light takes nothing away — its entire consequence happens
        /// on the other player's screen, so the person it happened to had no way
        /// to know. Being revealed is stored on the watcher, which is why this
        /// asserts the thief's screen off the officer's component.
        /// </summary>
        [UnityTest]
        public IEnumerator TheSensorLightTellsThePersonItExposed()
        {
            var officer = Track(new GameObject("Officer"));
            PlayerRoleIdentity officerIdentity =
                officer.AddComponent<PlayerRoleIdentity>();
            officerIdentity.Configure(PlayerRole.Police);
            FlashlightVisibility watcher =
                officer.AddComponent<FlashlightVisibility>();
            watcher.Configure(officerIdentity, null);

            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Thief);

            var overlayObject = Track(new GameObject("Player Status Banner"));
            PlayerStatusBannerView overlay =
                overlayObject.AddComponent<PlayerStatusBannerView>();
            yield return null;

            overlay.Refresh(1f);
            Assert.That(overlay.Showing, Is.Null);

            watcher.RevealFor(ThrowableCatalog.RevealSeconds, Vector3.zero);
            overlay.Refresh(1f);

            Assert.That(
                overlay.Showing,
                Is.EqualTo(PlayerStatusBannerView.Status.Revealed));
            Assert.That(overlay.Alpha, Is.GreaterThan(0.9f));
            Assert.That(
                overlay.LabelText,
                Does.Contain("센서등"));

            // The officer's own screen says nothing here — they already have the
            // radar and the alert line, and telling them they are visible would
            // be false.
            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);
            overlay.Refresh(1f);
            Assert.That(overlay.Showing, Is.Null);
        }

        /// <summary>
        /// Twelve seconds of 1.25× is long enough to end before you notice it
        /// started, and nothing on the officer's screen said it had.
        ///
        /// The boost is driven directly rather than through the alarm: the
        /// alarm's own half of this lives in
        /// <c>DisplayCaseAndAlarmPlayModeTests</c> where the player fixtures
        /// already exist. What is being asserted here is the banner.
        /// </summary>
        [UnityTest]
        public IEnumerator TheAlarmSprintIsOnTheOfficersScreen()
        {
            GameObject police = Player(PlayerRole.Police, out StunState _);
            PlayerMovementMotor motor = MotorOn(police);

            LocalPlayerRoleSelector.OverrideRole(PlayerRole.Police);

            var overlayObject = Track(new GameObject("Player Status Banner"));
            PlayerStatusBannerView overlay =
                overlayObject.AddComponent<PlayerStatusBannerView>();
            yield return null;

            overlay.Refresh(1f);
            Assert.That(overlay.Showing, Is.Null);

            motor.ApplyBoost(
                PawliceAndPurrglar.Gameplay.Loot.LootAlarm.PoliceBoostMultiplier,
                PawliceAndPurrglar.Gameplay.Loot.LootAlarm.PoliceBoostSeconds);
            overlay.Refresh(1f);

            Assert.That(
                overlay.Showing,
                Is.EqualTo(PlayerStatusBannerView.Status.Sprinting));
            Assert.That(overlay.LabelText, Does.Contain("질주"));

            motor.ClearBoost();
            overlay.Refresh(1f);
            Assert.That(overlay.Showing, Is.Null);
        }

        /// <summary>
        /// A motor that survives its own <c>Awake</c>.
        ///
        /// It throws on a missing <c>CharacterController</c>, config or match
        /// state, and an unhandled exception in <c>AddComponent</c> fails the
        /// test before a single assert runs. Built inactive and configured
        /// first, which is the pattern the older fixtures already use.
        /// </summary>
        private PlayerMovementMotor MotorOn(GameObject player)
        {
            bool wasActive = player.activeSelf;
            player.SetActive(false);
            CharacterController controller =
                player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.45f;

            var config = ScriptableObject.CreateInstance<PawliceAndPurrglar.Config.PlayerConfig>();
            _configs.Add(config);

            PlayerMovementMotor motor =
                player.AddComponent<PlayerMovementMotor>();
            motor.Configure(controller, config, new AlwaysPlaying(), null);
            player.SetActive(wasActive);
            return motor;
        }

        private readonly List<ScriptableObject> _configs = new();

        private sealed class AlwaysPlaying : PawliceAndPurrglar.Match.IMatchStateReader
        {
            public PawliceAndPurrglar.Match.MatchState CurrentState =>
                PawliceAndPurrglar.Match.MatchState.Playing;

            public bool IsGameplayActive => true;
        }
    }
}
