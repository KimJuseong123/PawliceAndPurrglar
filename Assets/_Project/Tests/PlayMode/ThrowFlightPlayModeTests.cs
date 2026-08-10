using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    /// <summary>
    /// A thrown rock has to travel, and travelling has to mean it can be dodged.
    ///
    /// The throw used to be settled the moment it left the hand, so the arc the
    /// players watched was a replay of a hit that had already happened. Watching a
    /// rock come and stepping aside did nothing, which is worse than having no
    /// rock: it teaches the player that what they see is not what is happening.
    /// </summary>
    public sealed class ThrowFlightPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject>
            _spawned = new();

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
        }

        [UnityTest]
        public IEnumerator ARockTakesTimeToArriveAndStunsOnArrival()
        {
            ThrowFlightTracker tracker = CreateTracker();
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, Vector3.zero);
            PlayerRoleIdentity thief =
                CreatePlayer(PlayerRole.Thief, new Vector3(0f, 0f, 9f));
            StunState stun = thief.gameObject.AddComponent<StunState>();
            yield return null;

            ThrowFlightTracker.Launch(
                police,
                ThrowableKind.Rock,
                Vector3.zero,
                Vector3.forward,
                12f);
            Assert.That(tracker.ActiveFlightCount, Is.EqualTo(1));

            // A tenth of a second in, the rock has covered about 1.6 m of the
            // 9 m gap. Nothing may have happened yet — this is the assertion the
            // instant version could never have passed.
            tracker.Tick(0.1f);
            Assert.That(
                stun.IsStunned,
                Is.False,
                "The rock cannot arrive before it has flown.");
            Assert.That(tracker.ActiveFlightCount, Is.EqualTo(1));

            // Now let it cover the rest.
            for (int step = 0; step < 10; step++)
            {
                tracker.Tick(0.05f);
            }

            Assert.That(
                stun.IsStunned,
                Is.True,
                "A rock that reaches somebody has to stun them.");
            Assert.That(
                tracker.ActiveFlightCount,
                Is.EqualTo(0),
                "A spent rock must not keep flying and hit again.");
        }

        /// <summary>
        /// The point of the flight time. Somebody who steps out of the corridor
        /// while the rock is in the air is missed.
        /// </summary>
        [UnityTest]
        public IEnumerator SteppingAsideDuringTheFlightAvoidsTheRock()
        {
            ThrowFlightTracker tracker = CreateTracker();
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, Vector3.zero);
            PlayerRoleIdentity thief =
                CreatePlayer(PlayerRole.Thief, new Vector3(0f, 0f, 10f));
            StunState stun = thief.gameObject.AddComponent<StunState>();
            yield return null;

            ThrowFlightTracker.Launch(
                police,
                ThrowableKind.Rock,
                Vector3.zero,
                Vector3.forward,
                12f);

            // Two frames of flight, then dodge sideways by more than the
            // corridor's half-width.
            tracker.Tick(0.1f);
            tracker.Tick(0.1f);
            thief.transform.position = new Vector3(
                ThrowableCatalog.ThrowHitRadiusMeters * 2.5f,
                0f,
                10f);

            for (int step = 0; step < 20; step++)
            {
                tracker.Tick(0.05f);
            }

            Assert.That(
                stun.IsStunned,
                Is.False,
                "Stepping out of the way while the rock is in the air has to "
                + "work, or the flight time is decoration.");
            Assert.That(tracker.ActiveFlightCount, Is.EqualTo(0));
        }

        /// <summary>
        /// A rock that passes right through somebody between two frames would make
        /// dodging a matter of frame rate rather than reaction.
        /// </summary>
        [UnityTest]
        public IEnumerator ARockCannotSkipThroughSomebodyInOneStep()
        {
            ThrowFlightTracker tracker = CreateTracker();
            PlayerRoleIdentity police =
                CreatePlayer(PlayerRole.Police, Vector3.zero);
            PlayerRoleIdentity thief =
                CreatePlayer(PlayerRole.Thief, new Vector3(0f, 0f, 5f));
            StunState stun = thief.gameObject.AddComponent<StunState>();
            yield return null;

            ThrowFlightTracker.Launch(
                police,
                ThrowableKind.Rock,
                Vector3.zero,
                Vector3.forward,
                12f);

            // One enormous step, far past the target. The segment travelled has
            // to be tested, not just the point reached.
            tracker.Tick(1f);

            Assert.That(
                stun.IsStunned,
                Is.True,
                "A single long frame must not let the rock tunnel through.");
        }

        private ThrowFlightTracker CreateTracker()
        {
            var trackerObject = new GameObject("Throw Flights");
            _spawned.Add(trackerObject);
            return trackerObject.AddComponent<ThrowFlightTracker>();
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
            _spawned.Add(player);
            return identity;
        }
    }
}
