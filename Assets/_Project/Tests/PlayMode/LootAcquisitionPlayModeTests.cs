using System.Collections;
using NUnit.Framework;
using PawsAndLoot.Config;
using PawsAndLoot.Gameplay.Loot;
using PawsAndLoot.Gameplay.Players;
using PawsAndLoot.Match;
using UnityEngine;
using UnityEngine.TestTools;

namespace PawsAndLoot.Tests.PlayMode
{
    public sealed class LootAcquisitionPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnlyThiefCanAcquireOneLootDuringPlaying()
        {
            var state = new MutableMatchState();
            LootCarrier police = CreateCarrier(
                "Police",
                PlayerRole.Police,
                state);
            LootCarrier thief = CreateCarrier(
                "Thief",
                PlayerRole.Thief,
                state);
            LootItem first = CreateLoot("First Loot");
            LootItem second = CreateLoot("Second Loot");

            Assert.That(police.TryAcquire(first), Is.False);
            Assert.That(thief.TryAcquire(first), Is.False);
            Assert.That(first.CurrentState, Is.EqualTo(LootState.Available));

            state.IsGameplayActive = true;
            Assert.That(police.TryAcquire(first), Is.False);
            Assert.That(thief.TryAcquire(first), Is.True);
            Assert.That(first.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(first.CurrentCarrier, Is.SameAs(thief));
            Assert.That(thief.HeldLoot, Is.SameAs(first));

            Assert.That(thief.TryAcquire(first), Is.False);
            Assert.That(thief.TryAcquire(second), Is.False);
            Assert.That(second.CurrentState, Is.EqualTo(LootState.Available));

            Object.Destroy(first.Definition);
            Object.Destroy(second.Definition);
            Object.Destroy(first.gameObject);
            Object.Destroy(second.gameObject);
            Object.Destroy(police.gameObject);
            Object.Destroy(thief.gameObject);
            yield return null;
        }

        private static LootCarrier CreateCarrier(
            string name,
            PlayerRole role,
            IMatchStateReader state)
        {
            var player = new GameObject(name);
            PlayerRoleIdentity identity =
                player.AddComponent<PlayerRoleIdentity>();
            identity.Configure(role);
            var carryPoint = new GameObject("CarryPoint");
            carryPoint.transform.SetParent(player.transform, false);
            LootCarrier carrier = player.AddComponent<LootCarrier>();
            carrier.Configure(identity, state, carryPoint.transform);
            return carrier;
        }

        private static LootItem CreateLoot(string name)
        {
            var lootObject = new GameObject(name);
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(
                lootObject.transform,
                false);
            LootDefinition definition =
                ScriptableObject.CreateInstance<LootDefinition>();
            definition.Configure(
                $"{name}-id",
                name,
                LootRarity.Common);
            LootItem loot = lootObject.AddComponent<LootItem>();
            loot.Configure(definition, presentation.transform);
            lootObject.SetActive(true);
            return loot;
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
