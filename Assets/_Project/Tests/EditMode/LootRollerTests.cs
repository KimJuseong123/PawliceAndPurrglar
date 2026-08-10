using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PawliceAndPurrglar.Gameplay.Items;
using UnityEngine;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// What a container may hand out.
    ///
    /// Every one of these fails silently in play. A weight that is ignored looks
    /// like luck, a duplicate cap that is ignored looks like luck, and a roller that
    /// draws differently from the same seed looks like luck twice. None of them can
    /// be told apart from a fair draw by watching, which is the whole reason they are
    /// pinned here.
    /// </summary>
    public sealed class LootRollerTests
    {
        private readonly List<LootTable> _tables = new();

        [TearDown]
        public void TearDown()
        {
            foreach (LootTable table in _tables)
            {
                Object.DestroyImmediate(table);
            }

            _tables.Clear();
        }

        private LootTable Table(
            IEnumerable<LootTableEntry> entries,
            int minimum,
            int maximum,
            bool allowDuplicates = true,
            int maximumDuplicates = 2)
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            table.Configure(
                "테스트 보관함",
                entries,
                minimum,
                maximum,
                allowDuplicates,
                maximumDuplicates);
            _tables.Add(table);
            return table;
        }

        /// <summary>
        /// The same seed has to give the same cupboard.
        ///
        /// The host rolls and the clients are told, so a roller that wandered would
        /// be unreproducible the moment anybody asked why a container held what it
        /// held. This is also what keeps `UnityEngine.Random` out: it is global
        /// state, and a container filled from it changes when anything else draws.
        /// </summary>
        [Test]
        public void TheSameSeedGivesTheSameContents()
        {
            LootTable table = Table(
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 30, 1, 3),
                    new LootTableEntry(ThrowableKind.Banana, 20, 1, 2),
                    new LootTableEntry(ThrowableKind.TunaCan, 10)
                },
                2,
                4);

            List<LootRoll> first = LootRoller.Roll(table, new System.Random(4242));
            List<LootRoll> second = LootRoller.Roll(table, new System.Random(4242));

            Assert.That(first, Is.Not.Empty);
            Assert.That(
                second.Select(roll => (roll.Kind, roll.Quantity)),
                Is.EqualTo(first.Select(roll => (roll.Kind, roll.Quantity))),
                "The same seed drew a different cupboard.");
        }

        /// <summary>
        /// Weight has to decide how often, not merely who is eligible.
        ///
        /// Asserted as a wide band rather than an exact ratio: this is a statement
        /// about the draw being weighted at all, and a tight bound on a random
        /// process is a test that fails on a Tuesday.
        /// </summary>
        [Test]
        public void HeavierEntriesComeUpMoreOften()
        {
            LootTable table = Table(
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 90),
                    new LootTableEntry(ThrowableKind.FrozenOctopus, 10)
                },
                1,
                1,
                allowDuplicates: true,
                maximumDuplicates: 99);

            var random = new System.Random(7);
            int rocks = 0;
            int octopuses = 0;
            for (int round = 0; round < 400; round++)
            {
                foreach (LootRoll roll in LootRoller.Roll(table, random))
                {
                    if (roll.Kind == ThrowableKind.Rock)
                    {
                        rocks++;
                    }
                    else
                    {
                        octopuses++;
                    }
                }
            }

            Assert.That(rocks + octopuses, Is.EqualTo(400));
            Assert.That(
                rocks,
                Is.GreaterThan(octopuses * 3),
                $"A 90/10 table drew {rocks} rocks against {octopuses} octopuses, "
                + "which is not a weighted draw.");
        }

        /// <summary>
        /// Six of one item in a six-slot cupboard reads as a bug even when the
        /// weights were obeyed exactly, which is why the cap exists.
        /// </summary>
        [Test]
        public void NoItemExceedsItsDuplicateCap()
        {
            LootTable table = Table(
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 100),
                    new LootTableEntry(ThrowableKind.Banana, 1)
                },
                6,
                6,
                allowDuplicates: true,
                maximumDuplicates: 2);

            for (int seed = 0; seed < 40; seed++)
            {
                List<LootRoll> rolled =
                    LootRoller.Roll(table, new System.Random(seed));
                foreach (IGrouping<ThrowableKind, LootRoll> group in
                    rolled.GroupBy(roll => roll.Kind))
                {
                    Assert.That(
                        group.Count(),
                        Is.LessThanOrEqualTo(2),
                        $"Seed {seed} drew {group.Count()} of {group.Key} against a "
                        + "cap of two.");
                }
            }
        }

        /// <summary>
        /// A capped table can run out of things it is still allowed to give, and
        /// "keep drawing until you have enough" is an editor hang rather than a
        /// short cupboard. Two entries capped at two cannot fill eight slots.
        /// </summary>
        [Test]
        public void AskingForMoreThanTheCapAllowsStillReturns()
        {
            LootTable table = Table(
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 50),
                    new LootTableEntry(ThrowableKind.Banana, 50)
                },
                8,
                8,
                allowDuplicates: true,
                maximumDuplicates: 2);

            List<LootRoll> rolled = LootRoller.Roll(table, new System.Random(1));

            Assert.That(rolled, Is.Not.Null);
            Assert.That(
                rolled.Count,
                Is.LessThanOrEqualTo(4),
                "Two entries capped at two cannot produce more than four items.");
        }

        /// <summary>
        /// A table nobody has filled in yet opens an empty cupboard rather than
        /// throwing. Walking up to a container is not the moment to find out that
        /// somebody forgot the data — that is what the validator is for.
        /// </summary>
        [Test]
        public void AnUnusableTableRollsNothingRatherThanThrowing()
        {
            Assert.That(
                LootRoller.Roll(null, new System.Random(1)),
                Is.Empty);

            LootTable empty = Table(
                System.Array.Empty<LootTableEntry>(),
                1,
                3);
            Assert.That(LootRoller.Roll(empty, new System.Random(1)), Is.Empty);

            // Populated but unusable: every weight is zero, so nothing can be
            // reached. This is the one that looks fine in the inspector.
            LootTable weightless = Table(
                new[]
                {
                    new LootTableEntry(ThrowableKind.Rock, 0),
                    new LootTableEntry(ThrowableKind.Banana, 0)
                },
                1,
                3);
            Assert.That(
                LootRoller.Roll(weightless, new System.Random(1)),
                Is.Empty,
                "A table of zero-weight entries drew something.");
        }

        [Test]
        public void QuantitiesStayInsideTheEntrysRange()
        {
            LootTable table = Table(
                new[] { new LootTableEntry(ThrowableKind.TunaCan, 10, 2, 4) },
                1,
                1,
                allowDuplicates: true,
                maximumDuplicates: 99);

            for (int seed = 0; seed < 30; seed++)
            {
                foreach (LootRoll roll in
                    LootRoller.Roll(table, new System.Random(seed)))
                {
                    Assert.That(roll.Quantity, Is.InRange(2, 4));
                }
            }
        }
    }
}
