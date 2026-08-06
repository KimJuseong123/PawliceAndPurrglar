using System;

namespace PawsAndLoot.Gameplay.Items
{
    /// <summary>
    /// The four-slot item model used by the first integrated build.
    ///
    /// It deliberately stores the small prototype enum rather than a Unity
    /// object reference. That keeps the state serializable over the existing
    /// host-authoritative network path and makes an empty slot unambiguous.
    /// </summary>
    public sealed class QuickSlotController
    {
        public const int SlotCount = 4;
        private const int EmptyValue = 0;

        private readonly int[] _slots = new int[SlotCount];
        private readonly int[] _quantities = new int[SlotCount];

        public int SelectedSlot { get; private set; }

        /// <summary>
        /// Points at a slot only when the player is not already holding one.
        ///
        /// Picking something up used to select it, which took the choice away
        /// mid-chase: a thief holding the firework ran over a banana and threw
        /// the banana. Selecting on the first pickup is still worth doing —
        /// with every slot empty there is no choice to take away.
        /// </summary>
        private void SelectIfNothingHeld(int slot)
        {
            if (_slots[SelectedSlot] == EmptyValue
                || _quantities[SelectedSlot] <= 0)
            {
                SelectedSlot = slot;
            }
        }

        public bool SelectSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return false;
            }

            if (SelectedSlot == slot)
            {
                return true;
            }

            SelectedSlot = slot;
            return true;
        }

        public bool TryStore(ThrowableKind kind, out int slot)
        {
            return TryStore(kind, 1, 1, out slot);
        }

        public bool TryStore(
            ThrowableKind kind,
            int quantity,
            int maximumStackSize,
            out int slot)
        {
            slot = -1;
            int safeQuantity = Math.Max(1, quantity);
            int safeMaximum = Math.Max(1, maximumStackSize);

            int existing = FindStackableSlot(kind, safeMaximum);
            if (existing >= 0)
            {
                int available = safeMaximum - _quantities[existing];
                if (available <= 0)
                {
                    return false;
                }

                _quantities[existing] += Math.Min(safeQuantity, available);
                slot = existing;
                SelectIfNothingHeld(existing);
                return true;
            }

            int empty = FindEmptySlot();
            if (empty < 0)
            {
                return false;
            }

            _slots[empty] = Encode(kind);
            _quantities[empty] = Math.Min(safeQuantity, safeMaximum);
            slot = empty;
            SelectIfNothingHeld(empty);
            return true;
        }

        public bool TryConsumeSelected(out ThrowableKind kind)
        {
            kind = ThrowableKind.Rock;
            int encoded = _slots[SelectedSlot];
            if (encoded == EmptyValue || _quantities[SelectedSlot] <= 0)
            {
                return false;
            }

            kind = Decode(encoded);
            _quantities[SelectedSlot] = Math.Max(
                0,
                _quantities[SelectedSlot] - 1);
            if (_quantities[SelectedSlot] <= 0)
            {
                // Emptied, and the selection stays on it.
                //
                // It used to jump to the first slot that still had something,
                // which meant using the last banana armed whatever was in slot
                // one — and the next press threw it. The player had chosen a
                // slot; running it dry is not them choosing a different one.
                _slots[SelectedSlot] = EmptyValue;
            }

            return true;
        }

        public bool TrySetSlot(
            int slot,
            ThrowableKind kind,
            int quantity)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return false;
            }

            if (quantity <= 0)
            {
                _slots[slot] = EmptyValue;
                _quantities[slot] = 0;
                // Cleared from outside — the network, or a match reset. The
                // selection stays put for the same reason it does when a slot
                // is used up.

                return true;
            }

            _slots[slot] = Encode(kind);
            _quantities[slot] = quantity;
            SelectedSlot = slot;
            return true;
        }

        /// <summary>
        /// Swaps two slots, or merges them when they hold the same kind.
        ///
        /// The selection follows the item rather than staying on the number. A
        /// player who drags their rock from slot 1 to slot 3 has moved the rock,
        /// not chosen the banana that used to be in slot 3 — and the next press of
        /// the use key would have thrown it.
        /// </summary>
        public bool TrySwap(int left, int right, int maximumStackSize)
        {
            if (left == right
                || left < 0
                || right < 0
                || left >= SlotCount
                || right >= SlotCount)
            {
                return false;
            }

            if (_slots[left] == EmptyValue && _slots[right] == EmptyValue)
            {
                return false;
            }

            int safeMaximum = Math.Max(1, maximumStackSize);
            if (_slots[left] == _slots[right]
                && _slots[left] != EmptyValue)
            {
                int moved = Math.Min(
                    _quantities[left],
                    safeMaximum - _quantities[right]);
                if (moved <= 0)
                {
                    return false;
                }

                _quantities[right] += moved;
                _quantities[left] -= moved;
                if (_quantities[left] <= 0)
                {
                    _slots[left] = EmptyValue;
                    _quantities[left] = 0;
                }

                SelectedSlot = right;
                return true;
            }

            (_slots[left], _slots[right]) = (_slots[right], _slots[left]);
            (_quantities[left], _quantities[right]) =
                (_quantities[right], _quantities[left]);
            if (SelectedSlot == left)
            {
                SelectedSlot = right;
            }
            else if (SelectedSlot == right)
            {
                SelectedSlot = left;
            }

            return true;
        }

        public bool TryGet(int slot, out ThrowableKind kind)
        {
            kind = ThrowableKind.Rock;
            if (slot < 0 || slot >= SlotCount)
            {
                return false;
            }

            int encoded = _slots[slot];
            if (encoded == EmptyValue || _quantities[slot] <= 0)
            {
                return false;
            }

            kind = Decode(encoded);
            return true;
        }

        public int GetQuantity(int slot)
        {
            if (slot < 0
                || slot >= SlotCount
                || _slots[slot] == EmptyValue)
            {
                return 0;
            }

            return Math.Max(0, _quantities[slot]);
        }

        public bool CanStore(ThrowableKind kind, int maximumStackSize)
        {
            return CanStore(kind, 1, maximumStackSize);
        }

        public bool CanStore(
            ThrowableKind kind,
            int quantity,
            int maximumStackSize)
        {
            int safeQuantity = Math.Max(1, quantity);
            int safeMaximum = Math.Max(1, maximumStackSize);
            int existing = FindStackableSlot(kind, safeMaximum);
            if (existing >= 0
                && safeMaximum - _quantities[existing] >= safeQuantity)
            {
                return true;
            }

            return FindEmptySlot() >= 0
                && safeQuantity <= safeMaximum;
        }

        public bool TryTakeSlot(
            int slot,
            out ThrowableKind kind,
            out int quantity)
        {
            kind = ThrowableKind.Rock;
            quantity = 0;
            if (!TryGet(slot, out kind))
            {
                return false;
            }

            quantity = Math.Max(1, _quantities[slot]);
            _slots[slot] = EmptyValue;
            _quantities[slot] = 0;
            if (SelectedSlot == slot)
            {
                SelectFirstOccupiedSlot();
            }

            return true;
        }

        public bool TryTakeOne(
            int slot,
            out ThrowableKind kind)
        {
            kind = ThrowableKind.Rock;
            if (!TryGet(slot, out kind))
            {
                return false;
            }

            _quantities[slot] = Math.Max(0, _quantities[slot] - 1);
            if (_quantities[slot] <= 0)
            {
                _slots[slot] = EmptyValue;
                if (SelectedSlot == slot)
                {
                    SelectFirstOccupiedSlot();
                }
            }

            return true;
        }

        public bool HasSelectedItem => TryGet(SelectedSlot, out _);

        public bool HasAnyItem
        {
            get
            {
                for (int index = 0; index < SlotCount; index++)
                {
                    if (TryGet(index, out _))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < SlotCount; index++)
                {
                    if (TryGet(index, out _))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int EncodeSlots()
        {
            int packed = 0;
            for (int index = 0; index < SlotCount; index++)
            {
                int encoded = _quantities[index] > 0
                    ? _slots[index]
                    : EmptyValue;
                packed |= (encoded & 0xF) << (index * 4);
            }

            return packed;
        }

        public int EncodeQuantities()
        {
            int packed = 0;
            for (int index = 0; index < SlotCount; index++)
            {
                int quantity = _slots[index] == EmptyValue
                    ? 0
                    : Math.Max(0, Math.Min(15, _quantities[index]));
                packed |= (quantity & 0xF) << (index * 4);
            }

            return packed;
        }

        public void ApplyEncodedSlots(int packed, int selectedSlot)
        {
            int packedQuantities = 0;
            for (int index = 0; index < SlotCount; index++)
            {
                int encoded = (packed >> (index * 4)) & 0xF;
                if (encoded != EmptyValue)
                {
                    packedQuantities |= 1 << (index * 4);
                }
            }

            ApplyEncodedSlots(packed, packedQuantities, selectedSlot);
        }

        public void ApplyEncodedSlots(
            int packed,
            int packedQuantities,
            int selectedSlot)
        {
            for (int index = 0; index < SlotCount; index++)
            {
                _slots[index] = (packed >> (index * 4)) & 0xF;
                int quantity = (packedQuantities >> (index * 4)) & 0xF;
                _quantities[index] = _slots[index] == EmptyValue
                    ? 0
                    : Math.Max(1, quantity);
            }

            SelectedSlot = Math.Max(0, Math.Min(SlotCount - 1, selectedSlot));
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            Array.Clear(_quantities, 0, _quantities.Length);
            SelectedSlot = 0;
        }

        private int FindStackableSlot(
            ThrowableKind kind,
            int maximumStackSize)
        {
            int encoded = Encode(kind);
            for (int index = 0; index < SlotCount; index++)
            {
                if (_slots[index] == encoded
                    && _quantities[index] > 0
                    && _quantities[index] < maximumStackSize)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindEmptySlot()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if (_slots[index] == EmptyValue || _quantities[index] <= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private void SelectFirstOccupiedSlot()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if (TryGet(index, out _))
                {
                    SelectedSlot = index;
                    return;
                }
            }

            SelectedSlot = 0;
        }

        private static int Encode(ThrowableKind kind)
        {
            return ((int)kind) + 1;
        }

        private static ThrowableKind Decode(int encoded)
        {
            return (ThrowableKind)Math.Max(0, Math.Min(15, encoded - 1));
        }
    }
}
