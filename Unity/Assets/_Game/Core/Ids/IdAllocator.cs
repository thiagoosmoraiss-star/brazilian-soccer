using System;

namespace Game.Core.Ids
{
    /// <summary>
    /// Monotonic Id source. Ids are never reused: the allocator only moves forward, and its state
    /// (<see cref="LastIssued"/>) is meant to be persisted so a reloaded save keeps allocating fresh Ids.
    /// </summary>
    public sealed class IdAllocator
    {
        public int LastIssued { get; private set; }

        public IdAllocator(int lastIssued = 0)
        {
            if (lastIssued < 0) throw new ArgumentOutOfRangeException(nameof(lastIssued));
            LastIssued = lastIssued;
        }

        public Id Next()
        {
            if (LastIssued == int.MaxValue) throw new InvalidOperationException("Id space exhausted.");
            LastIssued++;
            return new Id(LastIssued);
        }
    }
}
