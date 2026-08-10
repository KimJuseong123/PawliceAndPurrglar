using System;

namespace PawliceAndPurrglar.Gameplay.Loot
{
    public readonly struct LootRequestId :
        IEquatable<LootRequestId>
    {
        public LootRequestId(ulong value)
        {
            Value = value;
        }

        public ulong Value { get; }
        public bool IsValid => Value != 0;

        public bool Equals(LootRequestId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is LootRequestId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
