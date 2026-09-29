using System.Collections.Generic;
using Game.Core.Random;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class RngTests
    {
        private const ulong Seed = 20260929UL;

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new Rng(Seed);
            var b = new Rng(Seed);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong(), $"seed={Seed} index={i}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new Rng(Seed);
            var b = new Rng(Seed + 1);
            int equal = 0;
            for (int i = 0; i < 100; i++) if (a.NextULong() == b.NextULong()) equal++;
            Assert.Less(equal, 2, $"seeds={Seed},{Seed + 1}");
        }

        [Test]
        public void KnownSequence_IsStableAcrossRuns()
        {
            // Golden values: guards against accidental algorithm changes (would break saves and replays).
            var rng = new Rng(0UL);
            var first = new[] { rng.NextULong(), rng.NextULong(), rng.NextULong() };
            var again = new Rng(0UL);
            CollectionAssert.AreEqual(first, new[] { again.NextULong(), again.NextULong(), again.NextULong() });
            // Independent reference: Python implementation of SplitMix64 + xoshiro256** (seed 0).
            Assert.AreEqual(11091344671253066420UL, first[0], "seed=0");
        }

        [Test]
        public void StateRoundTrip_ContinuesIdentically()
        {
            var rng = new Rng(Seed);
            for (int i = 0; i < 17; i++) rng.NextULong();
            var restored = Rng.FromState(rng.GetState());
            for (int i = 0; i < 100; i++) Assert.AreEqual(rng.NextULong(), restored.NextULong(), $"seed={Seed}");
        }

        [Test]
        public void NextInt_StaysInRange_AndCoversAllValues()
        {
            var rng = new Rng(Seed);
            var seen = new HashSet<int>();
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(-3, 4);
                Assert.That(v, Is.InRange(-3, 3), $"seed={Seed}");
                seen.Add(v);
            }
            Assert.AreEqual(7, seen.Count, $"seed={Seed}");
        }

        [Test]
        public void NextInt_FullIntRange_DoesNotOverflow()
        {
            var rng = new Rng(Seed);
            for (int i = 0; i < 1000; i++) rng.NextInt(int.MinValue, int.MaxValue);
            Assert.Pass();
        }

        [Test]
        public void NextDouble_And_NextFloat_AreInUnitInterval()
        {
            var rng = new Rng(Seed);
            for (int i = 0; i < 10000; i++)
            {
                double d = rng.NextDouble();
                float f = rng.NextFloat();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0), $"seed={Seed}");
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), $"seed={Seed}");
            }
        }

        [Test]
        public void Streams_AreReproducibleFromMasterSeed()
        {
            var a = new RngStreams(Seed);
            var b = new RngStreams(Seed);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.Get("Market").NextULong(), b.Get("Market").NextULong(), $"seed={Seed}");
        }

        [Test]
        public void Streams_AreIndependent_ConsumptionInOneDoesNotShiftAnother()
        {
            var reference = new RngStreams(Seed);
            var expected = new ulong[50];
            for (int i = 0; i < expected.Length; i++) expected[i] = reference.Get("Development").NextULong();

            var streams = new RngStreams(Seed);
            // Heavy, interleaved use of other streams (including one created later) must not affect "Development".
            for (int i = 0; i < 1000; i++) streams.Get("Matchday").NextULong();
            for (int i = 0; i < expected.Length; i++)
            {
                streams.Get("Market").NextInt(0, 100);
                Assert.AreEqual(expected[i], streams.Get("Development").NextULong(), $"seed={Seed} index={i}");
                streams.Get("Youth").NextDouble();
            }
        }

        [Test]
        public void Streams_WithDifferentNames_Differ()
        {
            var s = new RngStreams(Seed);
            Assert.AreNotEqual(s.Get("Market").NextULong(), s.Get("Youth").NextULong(), $"seed={Seed}");
            Assert.AreNotEqual(RngStreams.DeriveSeed(Seed, "Match", 1), RngStreams.DeriveSeed(Seed, "Match", 2));
        }

        [Test]
        public void Streams_SnapshotRestore_ContinuesIdentically()
        {
            var s = new RngStreams(Seed);
            s.Get("B").NextULong();
            s.Get("A").NextULong();
            var snapshot = s.Snapshot();
            Assert.AreEqual("A", snapshot[0].Key);

            var restored = new RngStreams(Seed);
            foreach (var kv in snapshot) restored.Restore(kv.Key, kv.Value);
            Assert.AreEqual(s.Get("A").NextULong(), restored.Get("A").NextULong(), $"seed={Seed}");
            Assert.AreEqual(s.Get("B").NextULong(), restored.Get("B").NextULong(), $"seed={Seed}");
        }

        [Test]
        public void StableHash_IsProcessIndependent()
        {
            // FNV-1a 64 of "" is the offset basis; fixed value, unlike string.GetHashCode.
            Assert.AreEqual(14695981039346656037UL, StableHash.Fnv1a64(string.Empty));
            Assert.AreEqual(StableHash.Fnv1a64("Market"), StableHash.Fnv1a64("Market"));
        }
    }
}
