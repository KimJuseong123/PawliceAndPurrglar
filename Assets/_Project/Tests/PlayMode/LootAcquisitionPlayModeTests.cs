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
    public sealed class LootAcquisitionPlayModeTests
    {
        [UnityTest]
        public IEnumerator OnlyThiefCanAcquireLootAndTheBagFillsUp()
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

            // The same piece twice is still refused: that is a duplicate request,
            // and it is what stops one interact key press paying twice.
            Assert.That(thief.TryAcquire(first), Is.False);

            // A *second* piece now succeeds. The bag holds twenty-one, and the
            // hands hold the newest — which is what the drop key drops and what
            // `E` sells at the merchant.
            Assert.That(thief.TryAcquire(second), Is.True);
            Assert.That(second.CurrentState, Is.EqualTo(LootState.Carried));
            Assert.That(thief.CarriedCount, Is.EqualTo(2));
            Assert.That(thief.HeldLoot, Is.SameAs(second));

            // Only one piece is on show. Every carried piece parents its model to
            // the same carry point, so the rest have to be stowed or the thief
            // walks around inside a pile of overlapping treasure.
            Assert.That(first.IsStowed, Is.True);
            Assert.That(second.IsStowed, Is.False);

            // And a full bag refuses. Measured on a one-cell carrier rather than
            // by making twenty-two items, because the number that matters is the
            // refusal, not the twenty-one.
            LootCarrier smallBag = CreateCarrier(
                "Thief Small Bag",
                PlayerRole.Thief,
                state);
            smallBag.ConfigureCapacity(1);
            LootItem third = CreateLoot("Third Loot");
            LootItem fourth = CreateLoot("Fourth Loot");
            Assert.That(smallBag.TryAcquire(third), Is.True);
            Assert.That(smallBag.CanCarryMore, Is.False);
            Assert.That(smallBag.TryAcquire(fourth), Is.False);
            Assert.That(fourth.CurrentState, Is.EqualTo(LootState.Available));

            Object.Destroy(first.Definition);
            Object.Destroy(second.Definition);
            Object.Destroy(third.Definition);
            Object.Destroy(fourth.Definition);
            Object.Destroy(first.gameObject);
            Object.Destroy(second.gameObject);
            Object.Destroy(third.gameObject);
            Object.Destroy(fourth.gameObject);
            Object.Destroy(police.gameObject);
            Object.Destroy(thief.gameObject);
            Object.Destroy(smallBag.gameObject);
            yield return null;
        }

        /// <summary>
        /// The bag screen's drag, and the stacking it relies on.
        ///
        /// Both live in the same test because they are the same question asked
        /// twice: dropping a cell onto an empty one swaps, and dropping it onto
        /// the same kind merges. A swap that silently merged, or a merge that
        /// silently swapped, would both look like "the drag worked" on screen —
        /// the count in the corner is the only thing that tells them apart.
        /// </summary>
        [UnityTest]
        public IEnumerator DraggingACellSwapsItAndTheSameKindStacks()
        {
            var state = new MutableMatchState { IsGameplayActive = true };
            LootCarrier thief = CreateCarrier(
                "Thief Bag",
                PlayerRole.Thief,
                state);

            LootItem coinA = CreateLoot("Coin A");
            LootItem coinB = CreateLoot("Coin B", coinA.Definition);
            LootItem watch = CreateLoot("Watch");

            Assert.That(thief.TryAcquire(coinA), Is.True);
            Assert.That(thief.TryAcquire(coinB), Is.True);
            Assert.That(thief.TryAcquire(watch), Is.True);

            // Two coins in cell 0 and the watch in cell 1: the same kind shares a
            // cell, so a bag of twenty-one cells is not a limit of twenty-one
            // things.
            LootBag bag = thief.Cells;
            Assert.That(
                bag.TryGetCell(0, out LootDefinition firstKind, out int firstCount),
                Is.True);
            Assert.That(firstKind, Is.SameAs(coinA.Definition));
            Assert.That(firstCount, Is.EqualTo(2));
            Assert.That(
                bag.TryGetCell(1, out LootDefinition secondKind, out int secondCount),
                Is.True);
            Assert.That(secondKind, Is.SameAs(watch.Definition));
            Assert.That(secondCount, Is.EqualTo(1));

            // Dropping the watch on an empty cell moves it.
            Assert.That(thief.TryRearrange(1, 5), Is.True);
            Assert.That(bag.TryGetCell(1, out _, out _), Is.False);
            Assert.That(bag.TryGetCell(5, out LootDefinition moved, out _), Is.True);
            Assert.That(moved, Is.SameAs(watch.Definition));

            // Dropping the coins on the watch swaps them, because they are
            // different kinds. Refusing instead would be indistinguishable from
            // the drag not registering.
            Assert.That(thief.TryRearrange(0, 5), Is.True);
            Assert.That(bag.TryGetCell(0, out LootDefinition swapped, out int swappedCount), Is.True);
            Assert.That(swapped, Is.SameAs(watch.Definition));
            Assert.That(swappedCount, Is.EqualTo(1));
            Assert.That(bag.TryGetCell(5, out _, out int coinCount), Is.True);
            Assert.That(coinCount, Is.EqualTo(2));

            // Nothing left or joined the bag while it was being rearranged.
            Assert.That(thief.CarriedCount, Is.EqualTo(3));

            Object.Destroy(coinA.Definition);
            Object.Destroy(watch.Definition);
            Object.Destroy(coinA.gameObject);
            Object.Destroy(coinB.gameObject);
            Object.Destroy(watch.gameObject);
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

        /// <summary>
        /// Makes a loot object, optionally sharing another one's definition.
        ///
        /// Sharing matters for the stacking tests: a cell holds one *kind*, and
        /// two pieces made with their own definitions are two kinds no matter what
        /// they are called. A test that made two "coins" the easy way would prove
        /// stacking works and pass whether it did or not.
        /// </summary>
        private static LootItem CreateLoot(
            string name,
            LootDefinition sharedDefinition = null)
        {
            var lootObject = new GameObject(name);
            lootObject.SetActive(false);
            lootObject.AddComponent<BoxCollider>();
            var presentation = new GameObject("PresentationRoot");
            presentation.transform.SetParent(
                lootObject.transform,
                false);
            LootDefinition definition = sharedDefinition;
            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<LootDefinition>();
                definition.Configure(
                    $"{name}-id",
                    name,
                    LootRarity.Common);
            }

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
