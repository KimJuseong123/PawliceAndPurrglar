using System.Collections;
using NUnit.Framework;
using PawliceAndPurrglar.Config;
using PawliceAndPurrglar.Gameplay.Loot;
using PawliceAndPurrglar.Gameplay.Players;
using PawliceAndPurrglar.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawliceAndPurrglar.Tests.PlayMode
{
    public sealed class LootDropPlayModeTests
    {
        [UnityTest]
        public IEnumerator DropPlacesLootOnGroundAndAllowsReacquisition()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            LootCarrier carrier = CreateCarrier(state);
            LootItem loot = CreateLoot();
            GameObject floor = CreateFloor();
            Physics.SyncTransforms();
            Assert.That(carrier.TryAcquire(loot), Is.True);

            Assert.That(carrier.TryDrop(), Is.True);
            Assert.That(carrier.HeldLoot, Is.Null);
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Dropped));
            Assert.That(loot.transform.position.y, Is.GreaterThan(0f));
            Assert.That(loot.GetComponent<Collider>().enabled, Is.True);

            Assert.That(carrier.TryAcquire(loot), Is.True);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));

            DestroyTestObjects(carrier, loot, floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateAndInactiveDropRequestsAreIgnored()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            LootCarrier carrier = CreateCarrier(state);
            LootItem loot = CreateLoot();
            GameObject floor = CreateFloor();
            Physics.SyncTransforms();
            Assert.That(carrier.TryAcquire(loot), Is.True);

            state.IsGameplayActive = false;
            Assert.That(carrier.TryDrop(), Is.False);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));

            state.IsGameplayActive = true;
            Assert.That(carrier.TryDrop(), Is.True);
            Assert.That(carrier.TryDrop(), Is.False);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Dropped));

            DestroyTestObjects(carrier, loot, floor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DropWithoutGroundKeepsLootCarried()
        {
            var state = new MutableMatchState
            {
                IsGameplayActive = true
            };
            LootCarrier carrier = CreateCarrier(state);
            carrier.transform.position = new Vector3(0f, 50f, 0f);
            LootItem loot = CreateLoot();
            Assert.That(carrier.TryAcquire(loot), Is.True);
            Physics.SyncTransforms();

            Assert.That(carrier.TryDrop(), Is.False);
            Assert.That(carrier.HeldLoot, Is.SameAs(loot));
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));

            DestroyTestObjects(carrier, loot, null);
            yield return null;
        }

        private static LootCarrier CreateCarrier(
            IMatchStateReader state)
        {
            var player = new GameObject("Thief Player");
            player.transform.position = Vector3.up;
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(player.transform, false);
            carryPoint.transform.localPosition =
                new Vector3(0.7f, 0.8f, 0.4f);
            LootCarrier carrier = player.AddComponent<LootCarrier>();
            carrier.Configure(identity, state, carryPoint.transform);
            return carrier;
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            BoxCollider collider = lootObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.75f;
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "drop-test-loot",
                "Drop Test Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static GameObject CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.25f, 1.25f);
            floor.transform.localScale = new Vector3(8f, 0.5f, 8f);
            return floor;
        }

        private static void DestroyTestObjects(
            LootCarrier carrier,
            LootItem loot,
            GameObject floor)
        {
            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            Object.Destroy(carrier.gameObject);
            if (floor != null)
            {
                Object.Destroy(floor);
            }
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
