using System.Collections.Generic;
using UnityEngine;

// Spelled out rather than left to `using System`, which UnityEngine.Random makes
// ambiguous anyway. The alias is the point of this file: the seeded generator is
// the only one allowed here, because the host has to be able to reproduce a
// container and a client must never roll its own.
using Random = System.Random;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// One rolled stack: what came up and how many of it.
    /// </summary>
    public readonly struct LootRoll
    {
        public LootRoll(ThrowableKind kind, int quantity)
        {
            Kind = kind;
            Quantity = Mathf.Max(1, quantity);
        }

        public ThrowableKind Kind { get; }
        public int Quantity { get; }
    }

    /// <summary>
    /// Draws from a loot table.
    ///
    /// Given a <c>System.Random</c> rather than owning one, and never touching
    /// <c>UnityEngine.Random</c>. Two things depend on that. The host has to be able
    /// to reproduce a container's contents from the match seed so that a bug can be
    /// looked at twice, and the host is the only machine allowed to roll at all —
    /// a client that rolled its own would show the player items nobody else can
    /// see. A caller that has to hand over the generator cannot forget either.
    /// </summary>
    public static class LootRoller
    {
        /// <summary>
        /// Rolls a container's worth of items.
        ///
        /// Returns an empty list rather than throwing when the table has nothing
        /// drawable in it. An empty cupboard is a legitimate thing to walk up to,
        /// and a missing table is the validator's job to shout about before anybody
        /// plays — not the searching thief's problem mid-chase.
        /// </summary>
        public static List<LootRoll> Roll(LootTable table, Random random)
        {
            var rolled = new List<LootRoll>();
            if (table == null || random == null)
            {
                return rolled;
            }

            int totalWeight = table.TotalWeight;
            if (totalWeight <= 0 || table.Entries.Count == 0)
            {
                return rolled;
            }

            int wanted = random.Next(
                table.MinimumItemCount,
                table.MaximumItemCount + 1);
            if (wanted <= 0)
            {
                return rolled;
            }

            var drawnCounts = new Dictionary<ThrowableKind, int>();
            int cap = table.MaximumDuplicateCount;

            // Bounded rather than looping until it succeeds. With duplicates capped
            // a table can run out of things it is still allowed to give — six slots
            // against three entries capped at two each — and "keep drawing until
            // you have enough" is an editor hang rather than a short cupboard.
            int attempts = 0;
            int attemptLimit = wanted * 8 + table.Entries.Count * 4;
            while (rolled.Count < wanted && attempts < attemptLimit)
            {
                attempts++;
                if (!TryDrawEntry(table, random, totalWeight, out LootTableEntry entry))
                {
                    break;
                }

                drawnCounts.TryGetValue(entry.Kind, out int already);
                if (already >= cap)
                {
                    continue;
                }

                drawnCounts[entry.Kind] = already + 1;
                rolled.Add(new LootRoll(
                    entry.Kind,
                    random.Next(
                        entry.MinimumQuantity,
                        entry.MaximumQuantity + 1)));
            }

            return rolled;
        }

        /// <summary>
        /// Walks the weights until the draw is used up. Entries weighted zero are
        /// skipped by the arithmetic rather than by a check: a zero-weight entry
        /// never advances past the running total, so it can never be the one the
        /// draw lands on.
        /// </summary>
        private static bool TryDrawEntry(
            LootTable table,
            Random random,
            int totalWeight,
            out LootTableEntry drawn)
        {
            int draw = random.Next(totalWeight);
            int running = 0;
            foreach (LootTableEntry entry in table.Entries)
            {
                running += Mathf.Max(0, entry.Weight);
                if (draw < running)
                {
                    drawn = entry;
                    return true;
                }
            }

            drawn = default;
            return false;
        }
    }
}
