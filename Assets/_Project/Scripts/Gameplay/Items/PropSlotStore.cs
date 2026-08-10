using UnityEngine;

namespace PawliceAndPurrglar.Gameplay.Items
{
    /// <summary>
    /// A grid of prop slots of any size.
    ///
    /// Written because <see cref="QuickSlotController"/> is four slots and always
    /// will be — its count is a compile-time constant that the network layer packs
    /// four kinds and four quantities into two integers against, and widening it
    /// would change the wire format for every player every frame to give a cat a
    /// bigger bag.
    ///
    /// Deliberately smaller than the quick slots: no selection, no consumption, no
    /// encoding. A container the player moves things in and out of needs none of
    /// them, and every one of them is a second place for the quick slots' rules to
    /// be reimplemented slightly differently.
    /// </summary>
    public sealed class PropSlotStore
    {
        private readonly ThrowableKind[] _kinds;
        private readonly int[] _quantities;
        private readonly int _maximumStackSize;

        public PropSlotStore(int slotCount, int maximumStackSize = 9)
        {
            SlotCount = Mathf.Max(1, slotCount);
            _maximumStackSize = Mathf.Max(1, maximumStackSize);
            _kinds = new ThrowableKind[SlotCount];
            _quantities = new int[SlotCount];
        }

        public int SlotCount { get; }

        public bool TryGet(int index, out ThrowableKind kind)
        {
            kind = default;
            if (index < 0 || index >= SlotCount || _quantities[index] <= 0)
            {
                return false;
            }

            kind = _kinds[index];
            return true;
        }

        public int GetQuantity(int index)
        {
            return index < 0 || index >= SlotCount ? 0 : _quantities[index];
        }

        /// <summary>
        /// Whether <paramref name="quantity"/> of a kind would fit.
        ///
        /// Stacks onto a partly filled slot of the same kind before it takes an
        /// empty one, which is what stops four bananas occupying four slots of a
        /// sixteen-slot bag.
        /// </summary>
        public bool CanStore(ThrowableKind kind, int quantity)
        {
            return RoomFor(kind) >= Mathf.Max(1, quantity);
        }

        public bool TryStore(ThrowableKind kind, int quantity)
        {
            int wanted = Mathf.Max(1, quantity);
            if (RoomFor(kind) < wanted)
            {
                return false;
            }

            for (int index = 0; index < SlotCount && wanted > 0; index++)
            {
                if (_quantities[index] <= 0 || _kinds[index] != kind)
                {
                    continue;
                }

                int room = _maximumStackSize - _quantities[index];
                int moved = Mathf.Min(room, wanted);
                _quantities[index] += moved;
                wanted -= moved;
            }

            for (int index = 0; index < SlotCount && wanted > 0; index++)
            {
                if (_quantities[index] > 0)
                {
                    continue;
                }

                int moved = Mathf.Min(_maximumStackSize, wanted);
                _kinds[index] = kind;
                _quantities[index] = moved;
                wanted -= moved;
            }

            return true;
        }

        public bool TryTakeOne(int index, out ThrowableKind kind)
        {
            if (!TryGet(index, out kind))
            {
                return false;
            }

            _quantities[index]--;
            return true;
        }

        public bool TryTakeSlot(int index, out ThrowableKind kind, out int quantity)
        {
            quantity = 0;
            if (!TryGet(index, out kind))
            {
                return false;
            }

            quantity = _quantities[index];
            _quantities[index] = 0;
            return true;
        }

        public void Clear()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                _quantities[index] = 0;
            }
        }

        private int RoomFor(ThrowableKind kind)
        {
            int room = 0;
            for (int index = 0; index < SlotCount; index++)
            {
                room += _quantities[index] <= 0
                    ? _maximumStackSize
                    : _kinds[index] == kind
                        ? _maximumStackSize - _quantities[index]
                        : 0;
            }

            return room;
        }
    }
}
