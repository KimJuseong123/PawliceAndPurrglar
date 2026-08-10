using System.Collections.Generic;
using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    /// <summary>
    /// The thief's numbered loot cells: what is in each one, in the order the
    /// player put it there.
    ///
    /// Cells rather than a sorted list, because the bag screen lets the player
    /// drag things about and an arrangement they chose has to survive the next
    /// pickup. A grid derived by sorting has nowhere to write a move to, so every
    /// drag would spring back on the following frame — which reads as the drag
    /// being broken rather than as the grid being computed.
    ///
    /// One cell holds one kind. Two rubies stack; a ruby and a watch do not. The
    /// objects themselves are kept rather than a count, because each piece is a
    /// real <see cref="LootItem"/> with its own state machine, its own
    /// replication and its own sale — the count in the corner of the cell is the
    /// only part of it that is a number.
    /// </summary>
    public sealed class LootBag
    {
        private readonly List<LootItem>[] _cells;
        private readonly List<LootItem> _order = new();

        public LootBag(int capacity)
        {
            int cells = Mathf.Max(1, capacity);
            _cells = new List<LootItem>[cells];
            for (int index = 0; index < cells; index++)
            {
                _cells[index] = new List<LootItem>();
            }
        }

        public int CellCount => _cells.Length;

        /// <summary>
        /// How many pieces are in the bag, counting every copy.
        /// </summary>
        public int Count => _order.Count;

        /// <summary>
        /// Pieces in the order they were taken. The last one is in the hands.
        /// </summary>
        public IReadOnlyList<LootItem> InAcquisitionOrder => _order;

        public LootItem Newest => _order.Count > 0 ? _order[_order.Count - 1] : null;

        /// <summary>
        /// Whether one more piece of this kind fits.
        ///
        /// A kind already in the bag always fits: it joins that stack, so it costs
        /// no cell. Refusing it because every cell is occupied would mean a bag
        /// full of single items rejects a second coin while showing the player a
        /// cell that plainly has room in it.
        /// </summary>
        public bool CanStore(LootDefinition definition)
        {
            return definition != null
                && (IndexOfKind(definition) >= 0 || IndexOfEmpty() >= 0);
        }

        public bool Contains(LootItem loot)
        {
            return loot != null && _order.Contains(loot);
        }

        public bool TryAdd(LootItem loot)
        {
            if (loot == null
                || loot.Definition == null
                || _order.Contains(loot))
            {
                return false;
            }

            int cell = IndexOfKind(loot.Definition);
            if (cell < 0)
            {
                cell = IndexOfEmpty();
            }

            if (cell < 0)
            {
                return false;
            }

            _cells[cell].Add(loot);
            _order.Add(loot);
            return true;
        }

        public bool Remove(LootItem loot)
        {
            if (loot == null || !_order.Remove(loot))
            {
                return false;
            }

            foreach (List<LootItem> cell in _cells)
            {
                if (cell.Remove(loot))
                {
                    break;
                }
            }

            return true;
        }

        public void Clear()
        {
            _order.Clear();
            foreach (List<LootItem> cell in _cells)
            {
                cell.Clear();
            }
        }

        public bool TryGetCell(
            int index,
            out LootDefinition definition,
            out int count)
        {
            definition = null;
            count = 0;
            if (index < 0 || index >= _cells.Length)
            {
                return false;
            }

            Compact(_cells[index]);
            if (_cells[index].Count == 0)
            {
                return false;
            }

            definition = _cells[index][0].Definition;
            count = _cells[index].Count;
            return definition != null;
        }

        public IReadOnlyList<LootItem> ItemsInCell(int index)
        {
            if (index < 0 || index >= _cells.Length)
            {
                return System.Array.Empty<LootItem>();
            }

            Compact(_cells[index]);
            return _cells[index];
        }

        public int IndexOf(LootItem loot)
        {
            if (loot == null)
            {
                return -1;
            }

            for (int index = 0; index < _cells.Length; index++)
            {
                if (_cells[index].Contains(loot))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Moves a cell onto another: merged if they hold the same kind, swapped
        /// otherwise.
        ///
        /// Swapped rather than refused, because a refusal has no explanation the
        /// player can see. Dropping a watch on a gemstone and having nothing at
        /// all happen is indistinguishable from the drag not having registered,
        /// and they will try it again rather than aim somewhere else.
        /// </summary>
        public bool TryMove(int from, int to)
        {
            if (from == to
                || from < 0
                || to < 0
                || from >= _cells.Length
                || to >= _cells.Length
                || _cells[from].Count == 0)
            {
                return false;
            }

            List<LootItem> source = _cells[from];
            List<LootItem> target = _cells[to];
            if (target.Count > 0
                && target[0] != null
                && source[0] != null
                && target[0].Definition == source[0].Definition)
            {
                target.AddRange(source);
                source.Clear();
                return true;
            }

            _cells[from] = target;
            _cells[to] = source;
            return true;
        }

        private int IndexOfKind(LootDefinition definition)
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                Compact(_cells[index]);
                if (_cells[index].Count > 0
                    && _cells[index][0] != null
                    && _cells[index][0].Definition == definition)
                {
                    return index;
                }
            }

            return -1;
        }

        private int IndexOfEmpty()
        {
            for (int index = 0; index < _cells.Length; index++)
            {
                Compact(_cells[index]);
                if (_cells[index].Count == 0)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Drops destroyed pieces out of a cell.
        ///
        /// A <c>LootItem</c> can be destroyed by a scene unload while the bag
        /// still lists it, and a destroyed reference is not null — it compares
        /// equal to null but still occupies the cell. Left there it makes a cell
        /// look full of a kind that no longer exists, which is worse than an empty
        /// cell because the player can see it and cannot use it.
        /// </summary>
        private static void Compact(List<LootItem> cell)
        {
            for (int index = cell.Count - 1; index >= 0; index--)
            {
                if (cell[index] == null)
                {
                    cell.RemoveAt(index);
                }
            }
        }
    }
}
