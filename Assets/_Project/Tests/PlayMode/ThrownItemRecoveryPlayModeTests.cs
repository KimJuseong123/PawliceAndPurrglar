using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Integration.Network;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class ThrownItemRecoveryPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject>
            _spawned = new();

        [TearDown]
        public void DestroySpawnedObjects()
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
        public IEnumerator MissedThrowCreatesTriggerOnlyPickup()
        {
            var coordinatorObject = Track(
                new GameObject("Item Coordinator"));
            NetworkItemCoordinator coordinator =
                coordinatorObject.AddComponent<NetworkItemCoordinator>();

            var trackerObject = Track(
                new GameObject("Throw Flights"));
            ThrowFlightTracker tracker =
                trackerObject.AddComponent<ThrowFlightTracker>();
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            yield return null;

            ThrowFlightTracker.Launch(
                police,
                ThrowableKind.Rock,
                Vector3.zero,
                Vector3.forward,
                4f);
            tracker.Tick(1f);

            ThrowablePickup pickup = null;
            foreach (ThrowablePickup candidate in
                Object.FindObjectsByType<ThrowablePickup>(
                    FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.PickupId < 0)
                {
                    pickup = candidate;
                    break;
                }
            }

            Assert.That(pickup, Is.Not.Null);
            _spawned.Add(pickup.gameObject);
            Assert.That(coordinator.ActiveThrownPickupCount, Is.EqualTo(1));
            Collider[] colliders = pickup.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders, Has.Length.EqualTo(1));
            Assert.That(colliders[0], Is.TypeOf<SphereCollider>());
            Assert.That(colliders[0].isTrigger, Is.True);
        }

        [UnityTest]
        public IEnumerator PickupStoresExactlyOneItemAndCannotBeTakenTwice()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            ToolCarrier carrier = police.gameObject.AddComponent<ToolCarrier>();
            carrier.Configure(police, state);
            ThrowablePickup pickup = ThrownPickupFactory.Create(
                -7,
                ThrowableKind.Rock,
                Vector3.zero);
            _spawned.Add(pickup.gameObject);
            yield return null;

            var context = new PlayerInteractionContext(police);
            Assert.That(pickup.TryInteract(context), Is.True);
            Assert.That(pickup.TryInteract(context), Is.False);
            Assert.That(carrier.HasTool, Is.True);
            Assert.That(carrier.HeldKind, Is.EqualTo(ThrowableKind.Rock));
        }

        [UnityTest]
        public IEnumerator FailedUseKeepsTheHeldItem()
        {
            var state = new MutableMatchState { IsGameplayActive = false };
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            ToolCarrier carrier = police.gameObject.AddComponent<ToolCarrier>();
            carrier.Configure(police, state);
            carrier.ApplyReplicated(true, ThrowableKind.Rock);
            ToolUseAction action =
                police.gameObject.AddComponent<ToolUseAction>();
            action.Configure(police, carrier, 0);
            yield return null;

            Assert.That(action.TryUse(new Vector3(float.NaN, 0f, 1f)), Is.False);
            Assert.That(carrier.HasTool, Is.True);

            state.IsGameplayActive = true;
            Assert.That(action.TryUse(Vector3.forward), Is.True);
            Assert.That(carrier.HasTool, Is.False);
            Assert.That(action.TryUse(Vector3.forward), Is.False);
        }

        [UnityTest]
        public IEnumerator DirectHitDoesNotCreateRecoveryPickup()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            var coordinatorObject = Track(
                new GameObject("Item Coordinator"));
            NetworkItemCoordinator coordinator =
                coordinatorObject.AddComponent<NetworkItemCoordinator>();
            var trackerObject = Track(
                new GameObject("Throw Flights"));
            ThrowFlightTracker tracker =
                trackerObject.AddComponent<ThrowFlightTracker>();
            PlayerRoleIdentity police = CreatePlayer(
                PlayerRole.Police,
                Vector3.zero);
            ToolCarrier carrier = police.gameObject.AddComponent<ToolCarrier>();
            carrier.Configure(police, state);
            carrier.TryPickUp(ThrowableKind.Rock);
            ToolUseAction action =
                police.gameObject.AddComponent<ToolUseAction>();
            action.Configure(police, carrier, 0);
            PlayerRoleIdentity thief = CreatePlayer(
                PlayerRole.Thief,
                new Vector3(0f, 0f, 4f));
            thief.gameObject.AddComponent<StunState>();
            yield return null;

            Assert.That(action.TryUse(Vector3.forward), Is.True);
            tracker.Tick(1f);

            Assert.That(coordinator.ActiveThrownPickupCount, Is.EqualTo(0));
        }

        private GameObject Track(GameObject value)
        {
            _spawned.Add(value);
            return value;
        }

        private PlayerRoleIdentity CreatePlayer(
            PlayerRole role,
            Vector3 position)
        {
            var player = new GameObject($"{role} Recovery Player");
            player.transform.position = position;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            _spawned.Add(player);
            return identity;
        }

        private sealed class MutableMatchState : IMatchStateReader
        {
            public MatchState CurrentState => IsGameplayActive
                ? MatchState.Playing
                : MatchState.Ready;

            public bool IsGameplayActive { get; set; }
        }
    }
}
