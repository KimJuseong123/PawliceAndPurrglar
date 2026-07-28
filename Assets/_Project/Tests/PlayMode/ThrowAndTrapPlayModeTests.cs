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
        private readonly System.Collections.Generic.List<GameObject>
            _spawned = new();

        /// <summary>
        /// Clears everything a test put in the world, immediately.
        ///
        /// <c>Object.Destroy</c> only takes effect at the end of the frame, and
        /// the resolver finds its candidates with <c>FindObjectsByType</c> and its
        /// obstacles with a raycast — so a player or a wall left behind by one
        /// test is still live in the next one. That cost real time here: three
        /// tests failed against a neighbouring test's leftover thief and wall,
        /// which reads exactly like a resolver bug and is not one.
        ///
        /// Tracked explicitly rather than swept by type, because the props are
        /// plain cubes and spheres with nothing to search for.
        /// </summary>
        [TearDown]
        public void DestroyEverythingSpawned()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Registers an object for teardown and makes it visible to physics now.
        ///
        /// The sync matters: a collider created and moved in the same frame is not
        /// in the physics scene yet, so a raycast passes straight through the wall
        /// the test just built. One test was asserting that walls stop throws
        /// against a wall the query could not see.
        /// </summary>
        private GameObject Track(GameObject spawned)
        {
            _spawned.Add(spawned);
            Physics.SyncTransforms();
            return spawned;
        }

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

        }

        [UnityTest]
        public IEnumerator TrapCatchesTheOtherSideOnlyAndOnlyOnce()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            var trapObject = Track(new GameObject("Banana"));
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

        }

        [UnityTest]
        public IEnumerator TrapDoesNothingOutsideAMatch()
        {
            var state = new MutableMatchState { IsGameplayActive = false };
            var trapObject = Track(new GameObject("Banana"));
            PlacedTrap trap = trapObject.AddComponent<PlacedTrap>();
            trap.Configure(2, ThrowableKind.Banana, PlayerRole.Thief, state);
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            yield return null;

            Assert.That(trap.FindVictim(), Is.Null);

            state.IsGameplayActive = true;
            Assert.That(trap.FindVictim(), Is.SameAs(police));

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
            // Cleared before the next case: a grazing target left standing would
            // answer the wide-miss question instead of the one placed for it.
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

        }

        /// <summary>
        /// A stun that just stops the character is indistinguishable from lag or
        /// a stuck key, and the player's first assumption is that the game broke.
        /// </summary>
        [UnityTest]
        public IEnumerator StunStarsShowOnlyWhileStunned()
        {
            var playerObject = Track(new GameObject("Stunned Player"));
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

        }

        /// <summary>
        /// The arm has to swing back, come through, and end where it started.
        /// Ending anywhere else leaves the character permanently deformed, which
        /// is what a procedural animation with no return path does.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowArmSwingsBackThenThroughAndReturnsToRest()
        {
            var playerObject = Track(new GameObject("Thrower"));
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

        }

        /// <summary>
        /// A trigger volume is not a wall.
        ///
        /// The map is full of them — every pickup, stash, sale point and ladder
        /// is an invisible trigger sphere — and stopping a throw at the first one
        /// it crosses means most throws down a street die a metre from the
        /// thrower's hand for no visible reason.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowPassesThroughTriggerVolumes()
        {
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0f, 0f, 6f));

            var triggerObject = Track(new GameObject("Pickup Trigger"));
            triggerObject.transform.position = new Vector3(0f, 0.9f, 2f);
            SphereCollider trigger =
                triggerObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;
            Physics.SyncTransforms();
            yield return null;

            ThrowResolver.Result result = ThrowResolver.Resolve(
                police,
                Vector3.forward,
                ThrowableCatalog.ThrowRangeMeters,
                Physics.AllLayers);

            Assert.That(
                result.Hit,
                Is.SameAs(thief),
                "A rock lying in the road must not block a throw over it.");

        }

        /// <summary>
        /// Nor is the thrower's own dog. It runs at their heel, so treating it as
        /// cover means the officer can never throw anything at all.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowPassesThroughCharactersOnTheWay()
        {
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0f, 0f, 7f));

            // A solid body in the way, standing where a heeling companion would.
            // A CharacterController specifically, because that is what both
            // players and companions move on and what the resolver looks for.
            var companion = Track(new GameObject("Companion Body"));
            companion.transform.position = new Vector3(0f, 0f, 1.6f);
            CharacterController controller =
                companion.AddComponent<CharacterController>();
            controller.height = 1.4f;
            controller.radius = 0.5f;
            controller.center = new Vector3(0f, 0.7f, 0f);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    police,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    Physics.AllLayers).Hit,
                Is.SameAs(thief),
                "The officer's own dog must not be cover for the thief.");

        }

        /// <summary>
        /// A real wall still stops it. That is what makes breaking line of sight
        /// worth doing, so the trigger fix must not turn every throw into one
        /// that passes through buildings.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrowIsStillStoppedByASolidWall()
        {
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0f, 0f, 6f));

            var wall = Track(
                GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1f, 3f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.5f);
            Physics.SyncTransforms();
            yield return null;

            Assert.That(
                ThrowResolver.Resolve(
                    police,
                    Vector3.forward,
                    ThrowableCatalog.ThrowRangeMeters,
                    Physics.AllLayers).Connected,
                Is.False,
                "Breaking line of sight has to work, or the alleys are "
                + "pointless.");

        }

        /// <summary>
        /// The stun actually lands. The two halves are resolved separately — the
        /// resolver finds the victim and the action applies the stun — so a hit
        /// that stuns nobody is a real possibility worth pinning down.
        /// </summary>
        [UnityTest]
        public IEnumerator AConnectedThrowStunsTheVictim()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            ToolCarrier carrier =
                police.gameObject.AddComponent<ToolCarrier>();
            carrier.Configure(police, state);
            ToolUseAction action =
                police.gameObject.AddComponent<ToolUseAction>();
            action.Configure(police, carrier, Physics.AllLayers);

            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(4f, 0f, 0f));
            StunState stun = thief.gameObject.AddComponent<StunState>();
            yield return null;

            Assert.That(carrier.TryPickUp(ThrowableKind.Rock), Is.True);
            Assert.That(
                action.TryUse(Vector3.right),
                Is.True,
                "Aimed east at somebody standing east.");

            Assert.That(
                stun.IsStunned,
                Is.True,
                "A throw that connects and stuns nobody is the same as a "
                + "throw that missed, except the rock is gone.");
            Assert.That(
                stun.RemainingSeconds,
                Is.EqualTo(ThrowableCatalog.RockStunSeconds).Within(0.01f));

        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState =>
                IsGameplayActive ? MatchState.Playing : MatchState.Lobby;
            public bool IsGameplayActive { get; set; }
        }

        private PlayerRoleIdentity CreatePlayer(
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
            Track(player);
            return identity;
        }
    }
}
