using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Gameplay.Items;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    /// <summary>
    /// The host's two item decisions: did a throw connect, and did a trap catch
    /// somebody. Both have to answer "no" in the cases that would otherwise let
    /// a player hit themselves, hit through a wall, or trip over their own
    /// banana.
    /// </summary>
    public sealed class ThrowAndTrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator ThrowHitsAnOpponentInFrontOfIt()
        {
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                new Vector3(0f, 0f, 4f));
            yield return null;

            ThrowResolver.Result result = ThrowResolver.Resolve(
                thief,
                Vector3.forward,
                ThrowableCatalog.ThrowRangeMeters,
                0);

            Assert.That(result.Connected, Is.True);
            Assert.That(result.Hit, Is.SameAs(police));

            Object.Destroy(thief.gameObject);
            Object.Destroy(police.gameObject);
        }

        [UnityTest]
        public IEnumerator ThrowMissesBehindAndBesideAndOutOfRange()
        {
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            yield return null;

            // Behind the thrower.
            PlayerRoleIdentity behind = CreatePlayer(
                PlayerRole.Police,
                new Vector3(0f, 0f, -4f));
            yield return null;
            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "A throw must not hit somebody standing behind the thrower.");
            Object.DestroyImmediate(behind.gameObject);

            // Well off to the side of the flight path.
            PlayerRoleIdentity beside = CreatePlayer(
                PlayerRole.Police,
                new Vector3(5f, 0f, 4f));
            yield return null;
            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "The hit radius has to be narrow enough to miss.");
            Object.DestroyImmediate(beside.gameObject);

            // Further away than a throw carries.
            PlayerRoleIdentity far = CreatePlayer(
                PlayerRole.Police,
                new Vector3(0f, 0f, 30f));
            yield return null;
            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "Range has to matter, or the whole map is in reach.");
            Object.DestroyImmediate(far.gameObject);

            Object.Destroy(thief.gameObject);
        }

        [UnityTest]
        public IEnumerator ThrowNeverHitsTheThrowersOwnSide()
        {
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            PlayerRoleIdentity otherThief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0f, 0f, 3f));
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "Friendly fire would let a player stun themselves by proxy.");

            Object.Destroy(thief.gameObject);
            Object.Destroy(otherThief.gameObject);
        }

        [UnityTest]
        public IEnumerator TrapCatchesTheOtherSideOnlyAndOnlyOnce()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            var trapObject = new GameObject("Banana");
            trapObject.transform.position = Vector3.zero;
            PlacedTrap trap = trapObject.AddComponent<PlacedTrap>();
            trap.Configure(1, ThrowableKind.Banana, PlayerRole.Thief, state);

            // The thief placed it, so the thief walks over it safely.
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            yield return null;
            Assert.That(
                trap.FindVictim(),
                Is.Null,
                "A player must not slip on their own banana.");
            Object.DestroyImmediate(thief.gameObject);

            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            yield return null;

            Assert.That(trap.TryTrigger(out PlayerRoleIdentity victim), Is.True);
            Assert.That(victim, Is.SameAs(police));
            Assert.That(trap.IsArmed, Is.False);
            Assert.That(
                trap.TryTrigger(out _),
                Is.False,
                "A spent trap must not fire again while the victim stands on "
                + "it, or one banana holds somebody forever.");

            Object.Destroy(police.gameObject);
            Object.Destroy(trapObject);
        }

        [UnityTest]
        public IEnumerator TrapDoesNothingOutsideAMatch()
        {
            var state = new MutableMatchState { IsGameplayActive = false };
            var trapObject = new GameObject("Banana");
            PlacedTrap trap = trapObject.AddComponent<PlacedTrap>();
            trap.Configure(2, ThrowableKind.Banana, PlayerRole.Thief, state);
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            yield return null;

            Assert.That(trap.FindVictim(), Is.Null);

            state.IsGameplayActive = true;
            Assert.That(trap.FindVictim(), Is.SameAs(police));

            Object.Destroy(police.gameObject);
            Object.Destroy(trapObject);
        }

        /// <summary>
        /// The aim wins over the facing. This is the whole point of throwing with
        /// the cursor: a runner has to be able to fire sideways at whoever is
        /// chasing them without turning to face them first, which on this camera
        /// would mean stopping.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowFollowsTheCursorRatherThanTheFacing()
        {
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            // Running north, chased from the east.
            thief.transform.rotation =
                Quaternion.LookRotation(Vector3.forward);
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                new Vector3(5f, 0f, 0f));
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.right,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Hit,
                Is.SameAs(police),
                "Aimed east, so it has to travel east.");

            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "And the facing on its own must not hit them, or the aim is "
                + "decorative.");

            Object.Destroy(thief.gameObject);
            Object.Destroy(police.gameObject);
        }

        /// <summary>
        /// The corridor is about a character wide either side of the line, so a
        /// graze counts. Asserted as a relationship to the published radius
        /// rather than a literal, so retuning the number does not silently turn
        /// the beam into a pinpoint.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowConnectsOnAGrazeButNotOnAWideMiss()
        {
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                Vector3.zero);
            yield return null;

            PlayerRoleIdentity grazed = CreatePlayer(
                PlayerRole.Police,
                new Vector3(
                    ThrowableCatalog.ThrowHitRadiusMeters * 0.8f,
                    0f,
                    5f));
            yield return null;
            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.True,
                "Aiming with a cursor on a tilted camera is not precise. A "
                + "near miss that does nothing feels stolen.");
            Object.DestroyImmediate(grazed.gameObject);

            PlayerRoleIdentity missed = CreatePlayer(
                PlayerRole.Police,
                new Vector3(
                    ThrowableCatalog.ThrowHitRadiusMeters * 1.8f,
                    0f,
                    5f));
            yield return null;
            Assert.That(
                ThrowResolver.Resolve(
                    thief,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    0).Connected,
                Is.False,
                "It still has to be a corridor rather than a cone, or aiming "
                + "stops mattering.");
            Object.DestroyImmediate(missed.gameObject);

            Object.Destroy(thief.gameObject);
        }

        /// <summary>
        /// A stun that just stops the character is indistinguishable from lag or
        /// a stuck key, and the player's first assumption is that the game broke.
        /// </summary>
        [UnityTest]
        public IEnumerator StunStarsShowOnlyWhileStunned()
        {
            var playerObject = new GameObject("Stunned Player");
            StunState stun = playerObject.AddComponent<StunState>();
            PawsAndLoot.Animation.StunStarsView stars = playerObject
                .AddComponent<PawsAndLoot.Animation.StunStarsView>();
            stars.Configure(stun, null);
            yield return null;

            Assert.That(
                stars.IsShowing,
                Is.False,
                "Nothing over the head of somebody who is fine.");

            Assert.That(stun.TryApply(0.4f), Is.True);
            yield return null;
            Assert.That(stars.IsShowing, Is.True);

            stun.Clear();
            yield return null;
            Assert.That(
                stars.IsShowing,
                Is.False,
                "Stars left spinning after the stun ends would tell the "
                + "thrower to keep pressing on somebody already free.");

            Object.Destroy(playerObject);
        }

        /// <summary>
        /// The arm has to swing back, come through, and end where it started.
        /// Ending anywhere else leaves the character permanently deformed, which
        /// is what a procedural animation with no return path does.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowArmSwingsBackThenThroughAndReturnsToRest()
        {
            var playerObject = new GameObject("Thrower");
            PawsAndLoot.Animation.ThrowPresenter presenter = playerObject
                .AddComponent<PawsAndLoot.Animation.ThrowPresenter>();
            yield return null;

            float windup = presenter.EvaluateArmAngle(0.10f);
            float through = presenter.EvaluateArmAngle(0.19f);
            float settled = presenter.EvaluateArmAngle(2f);

            Assert.That(
                windup,
                Is.GreaterThan(30f),
                "The arm has to go somewhere first, or the throw is a twitch.");
            Assert.That(
                through,
                Is.LessThan(0f),
                "And carry past rest on release. That overshoot is the part "
                + "that reads as throwing rather than pointing.");
            Assert.That(
                settled,
                Is.EqualTo(0f).Within(0.01f),
                "Then back to rest exactly, or the arm drifts a little "
                + "further from the body with every rock.");

            Object.Destroy(playerObject);
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState =>
                IsGameplayActive ? MatchState.Playing : MatchState.Lobby;
            public bool IsGameplayActive { get; set; }
        }

        private static PlayerRoleIdentity CreatePlayer(
            PlayerRole role,
            Vector3 position)
        {
            var player = new GameObject($"{role} Player");
            player.SetActive(false);
            player.transform.position = position;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            player.SetActive(true);
            return identity;
        }
    }
}
