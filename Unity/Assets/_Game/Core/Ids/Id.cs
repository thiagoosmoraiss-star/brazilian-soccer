using System;

namespace Game.Core.Ids
{
    /// <summary>
    /// Stable integer identifier of a world entity. Persisted state references entities only by Id
    /// (never by object reference). Id 0 is reserved as "none".
    /// </summary>
    public readonly struct Id : IEquatable<Id>, IComparable<Id>
    {
        public static readonly Id None = default;

        public readonly int Value;

        public Id(int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Id must be >= 0.");
            Value = value;
        }

        public bool IsNone => Value == 0;

        public bool Equals(Id other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Id other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(Id other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool operator ==(Id a, Id b) => a.Value == b.Value;
        public static bool operator !=(Id a, Id b) => a.Value != b.Value;
    }
}
