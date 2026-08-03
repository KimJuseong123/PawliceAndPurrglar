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
                if (available >= safeQuantity)
                {
                    _quantities[existing] += safeQuantity;
                    SelectedSlot = existing;
                    slot = existing;
                    return true;
                }

                if (FindEmptySlot() < 0 || safeQuantity > safeMaximum)
                {
                    return false;
                }
            }

            int empty = FindEmptySlot();
            if (empty < 0)
            {
                return false;
            }

            _slots[empty] = Encode(kind);
            _quantities[empty] = Math.Min(safeQuantity, safeMaximum);
            SelectedSlot = empty;
            slot = empty;
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
                _slots[SelectedSlot] = EmptyValue;
                SelectFirstOccupiedSlot();
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
                if (SelectedSlot == slot)
                {
                    SelectFirstOccupiedSlot();
                }

                return true;
            }

            _slots[slot] = Encode(kind);
            _quantities[slot] = quantity;
            SelectedSlot = slot;
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
