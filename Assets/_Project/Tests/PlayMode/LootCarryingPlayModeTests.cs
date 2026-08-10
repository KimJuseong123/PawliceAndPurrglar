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
    public sealed class LootCarryingPlayModeTests
    {
        [UnityTest]
        public IEnumerator CarriedPresentationFollowsCarryPoint()
        {
            LootCarrier carrier = CreateCarrier();
            LootItem loot = CreateLoot();
            Collider worldCollider = loot.GetComponent<Collider>();

            Assert.That(carrier.TryAcquire(loot), Is.True);
            Assert.That(
                loot.PresentationRoot.parent,
                Is.EqualTo(carrier.CarryPoint));
            Assert.That(
                loot.PresentationRoot.localPosition,
                Is.EqualTo(Vector3.zero));
            Assert.That(worldCollider.enabled, Is.False);

            carrier.transform.position = new Vector3(6f, 0f, -3f);
            yield return null;

            Assert.That(
                loot.PresentationRoot.position,
                Is.EqualTo(carrier.CarryPoint.position));
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(loot.CurrentCarrier, Is.SameAs(carrier));

            DestroyTestObjects(carrier, loot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledCarrierSafelyReturnsLootToWorld()
        {
            LootCarrier carrier = CreateCarrier();
            LootItem loot = CreateLoot();
            Collider worldCollider = loot.GetComponent<Collider>();
            Assert.That(carrier.TryAcquire(loot), Is.True);

            carrier.gameObject.SetActive(false);

            Assert.That(carrier.HeldLoot, Is.Null);
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Dropped));
            Assert.That(loot.PresentationRoot.parent, Is.EqualTo(loot.transform));
            Assert.That(worldCollider.enabled, Is.True);

            DestroyTestObjects(carrier, loot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedCarrierDoesNotDestroyCarriedLoot()
        {
            LootCarrier carrier = CreateCarrier();
            LootItem loot = CreateLoot();
            Assert.That(carrier.TryAcquire(loot), Is.True);

            Object.Destroy(carrier.gameObject);
            yield return null;

            Assert.That(loot, Is.Not.Null);
            Assert.That(loot.CurrentCarrier, Is.Null);
            Assert.That(loot.CurrentState, Is.EqualTo(LootState.Dropped));
            Assert.That(loot.PresentationRoot.parent, Is.EqualTo(loot.transform));

            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            yield return null;
        }

        private static LootCarrier CreateCarrier()
        {
            var player = new GameObject("Thief Player");
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(PlayerRole.Thief);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(player.transform, false);
            carryPoint.transform.localPosition =
                new Vector3(0.7f, 0.8f, 0.4f);
            LootCarrier carrier = player.AddComponent<LootCarrier>();
            carrier.Configure(
                identity,
                new ActiveMatchState(),
                carryPoint.transform);
            return carrier;
        }

        private static LootItem CreateLoot()
        {
            var lootObject = new GameObject("Loot");
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(lootObject.transform, false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                "carrying-test-loot",
                "Carrying Test Loot",
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
        }

        private static void DestroyTestObjects(
            LootCarrier carrier,
            LootItem loot)
        {
            Object.Destroy(loot.Definition);
            Object.Destroy(loot.gameObject);
            Object.Destroy(carrier.gameObject);
        }

        private sealed class ActiveMatchState : IMatchStateReader
        {
            public MatchState CurrentState => MatchState.Playing;
            public bool IsGameplayActive => true;
        }
    }
}
