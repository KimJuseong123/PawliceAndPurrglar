using System;
using System.Collections.Generic;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// One entry in a loot table: what can come out, how often, and how many.
    /// </summary>
    [Serializable]
    public struct LootTableEntry
    {
        [SerializeField]
        private ThrowableKind kind;

        /// <summary>
        /// Relative chance, not a percentage. Weights are compared against the
        /// other entries in the same table, so adding a rare item to a table does
        /// not require rebalancing every number already in it — which is what a
        /// column of percentages summing to a hundred would have meant.
        /// </summary>
        [SerializeField, Min(0)]
        private int weight;

        [SerializeField, Min(1)]
        private int minimumQuantity;

        [SerializeField, Min(1)]
        private int maximumQuantity;

        public ThrowableKind Kind => kind;
        public int Weight => weight;
        public int MinimumQuantity => Mathf.Max(1, minimumQuantity);

        public int MaximumQuantity =>
            Mathf.Max(MinimumQuantity, maximumQuantity);

        public LootTableEntry(
            ThrowableKind entryKind,
            int entryWeight,
            int entryMinimum = 1,
            int entryMaximum = 1)
        {
            kind = entryKind;
            weight = entryWeight;
            minimumQuantity = entryMinimum;
            maximumQuantity = entryMaximum;
        }
    }

    /// <summary>
    /// What a cupboard, a till or a bedroom floor can turn up, as data.
    ///
    /// An asset rather than a table in code, because which items belong in a
    /// kitchen is a question for whoever dresses the kitchen and they should not
    /// need a compile to answer it.
    ///
    /// The entries are quick-slot props rather than sellable treasure. That is the
    /// current shape of the thief's bag: treasure goes in <c>LootCarrier</c>, which
    /// holds exactly one thing at a time, and a cupboard that can only ever give
    /// you one item is not worth opening. Selling what you searched out is a
    /// separate job and needs a bag that does not exist yet.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LootTable",
        menuName = "PawliceAndPurrglar/Loot Table",
        order = 40)]
    public sealed class LootTable : ScriptableObject
    {
        [SerializeField]
        private string displayName = "보관함";

        [SerializeField]
        private List<LootTableEntry> entries = new();

        /// <summary>
        /// How many items a container filled from this table starts with. Rolled
        /// per container, so two cupboards on the same table are not the same
        /// cupboard.
        /// </summary>
        [SerializeField, Min(0)]
        private int minimumItemCount = 1;

        [SerializeField, Min(0)]
        private int maximumItemCount = 3;

        /// <summary>
        /// Whether the same item may come up twice in one container, and how
        /// often. Six of one thing in a six-slot cupboard reads as a bug even when
        /// the weights were obeyed exactly.
        /// </summary>
        [SerializeField]
        private bool allowDuplicateItems = true;

        [SerializeField, Min(1)]
        private int maximumDuplicateCount = 2;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public IReadOnlyList<LootTableEntry> Entries => entries;
        public int MinimumItemCount => Mathf.Max(0, minimumItemCount);

        public int MaximumItemCount =>
            Mathf.Max(MinimumItemCount, maximumItemCount);

        public bool AllowDuplicateItems => allowDuplicateItems;

        public int MaximumDuplicateCount =>
            allowDuplicateItems ? Mathf.Max(1, maximumDuplicateCount) : 1;

        /// <summary>
        /// The total of every weight, which is what a draw is taken against. Zero
        /// means nothing in this table can ever be drawn — a table of entries all
        /// weighted zero is as empty as a table with no entries, and the validator
        /// reports both.
        /// </summary>
        public int TotalWeight
        {
            get
            {
                int total = 0;
                foreach (LootTableEntry entry in entries)
                {
                    total += Mathf.Max(0, entry.Weight);
                }

                return total;
            }
        }

        /// <summary>
        /// Fills a table in code, for the asset generator and for tests. Tests
        /// that build their own table do not depend on an asset staying as it was
        /// when they were written.
        /// </summary>
        public void Configure(
            string configuredDisplayName,
            IEnumerable<LootTableEntry> configuredEntries,
            int configuredMinimum,
            int configuredMaximum,
            bool configuredAllowDuplicates = true,
            int configuredMaximumDuplicates = 2)
        {
            displayName = configuredDisplayName;
            entries = new List<LootTableEntry>(configuredEntries);
            minimumItemCount = Mathf.Max(0, configuredMinimum);
            maximumItemCount = Mathf.Max(minimumItemCount, configuredMaximum);
            allowDuplicateItems = configuredAllowDuplicates;
            maximumDuplicateCount = Mathf.Max(1, configuredMaximumDuplicates);
        }
    }
}
