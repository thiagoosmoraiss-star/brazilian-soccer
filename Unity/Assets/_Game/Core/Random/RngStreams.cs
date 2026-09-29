using System;
using System.Collections.Generic;

namespace Game.Core.Random
{
    /// <summary>
    /// Independent RNG streams derived from one master seed (career or match seed).
    /// Each system (e.g. "Matchday", "Development", "Market", "Youth") draws only from its own stream,
    /// so changing how much one system consumes never shifts another system's sequence.
    /// Stream seeds depend only on (master seed, stream name) - not on creation order.
    /// </summary>
    public sealed class RngStreams
    {
        private readonly Dictionary<string, Rng> _streams = new Dictionary<string, Rng>(StringComparer.Ordinal);

        public ulong MasterSeed { get; }

        public RngStreams(ulong masterSeed)
        {
            MasterSeed = masterSeed;
        }

        /// <summary>Returns the stream for <paramref name="name"/>, creating it on first use.</summary>
        public Rng Get(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Stream name is required.", nameof(name));
            if (!_streams.TryGetValue(name, out var rng))
            {
                rng = new Rng(DeriveSeed(MasterSeed, name));
                _streams.Add(name, rng);
            }
            return rng;
        }

        /// <summary>Restores a stream from a persisted state (e.g. on save load).</summary>
        public void Restore(string name, RngState state)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Stream name is required.", nameof(name));
            _streams[name] = Rng.FromState(state);
        }

        /// <summary>Snapshot of all created streams, ordered by name (stable for persistence).</summary>
        public IReadOnlyList<KeyValuePair<string, RngState>> Snapshot()
        {
            var names = new List<string>(_streams.Keys);
            names.Sort(StringComparer.Ordinal);
            var result = new List<KeyValuePair<string, RngState>>(names.Count);
            foreach (var n in names) result.Add(new KeyValuePair<string, RngState>(n, _streams[n].GetState()));
            return result;
        }

        /// <summary>
        /// Deterministic seed for a named sub-stream. Uses a stable FNV-1a hash of the name
        /// (string.GetHashCode is randomized per process and must never be used here).
        /// </summary>
        public static ulong DeriveSeed(ulong masterSeed, string name)
        {
            return SplitMix64.Mix(masterSeed ^ SplitMix64.Mix(StableHash.Fnv1a64(name)));
        }

        /// <summary>Deterministic seed for an indexed sub-stream (e.g. one per fixture).</summary>
        public static ulong DeriveSeed(ulong masterSeed, string name, ulong index)
        {
            return SplitMix64.Mix(DeriveSeed(masterSeed, name) ^ SplitMix64.Mix(index + 1));
        }
    }

    public static class StableHash
    {
        public static ulong Fnv1a64(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= (byte)c;
                hash *= 1099511628211UL;
                hash ^= (byte)(c >> 8);
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
