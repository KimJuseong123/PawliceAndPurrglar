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
