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
            slot = FindEmptySlot();
            if (slot < 0)
            {
                return false;
            }

            _slots[slot] = Encode(kind);
            SelectedSlot = slot;
            return true;
        }

        public bool TryConsumeSelected(out ThrowableKind kind)
        {
            kind = ThrowableKind.Rock;
            int encoded = _slots[SelectedSlot];
            if (encoded == EmptyValue)
            {
                return false;
            }

            kind = Decode(encoded);
            _slots[SelectedSlot] = EmptyValue;
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
            if (encoded == EmptyValue)
            {
                return false;
            }

            kind = Decode(encoded);
            return true;
        }

        public bool HasSelectedItem => _slots[SelectedSlot] != EmptyValue;

        public int EncodeSlots()
        {
            int packed = 0;
            for (int index = 0; index < SlotCount; index++)
            {
                packed |= (_slots[index] & 0xF) << (index * 4);
            }

            return packed;
        }

        public void ApplyEncodedSlots(int packed, int selectedSlot)
        {
            for (int index = 0; index < SlotCount; index++)
            {
                _slots[index] = (packed >> (index * 4)) & 0xF;
            }

            SelectedSlot = Math.Max(0, Math.Min(SlotCount - 1, selectedSlot));
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            SelectedSlot = 0;
        }

        private int FindEmptySlot()
        {
            for (int index = 0; index < SlotCount; index++)
            {
                if (_slots[index] == EmptyValue)
                {
                    return index;
                }
            }

            return -1;
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
