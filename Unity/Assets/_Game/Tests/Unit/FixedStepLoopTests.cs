using Game.Core.Loop;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    public class FixedStepLoopTests
    {
        // Test parameters only (the production rate comes from data in the stage that uses the loop).
        private const double Hz = 50.0;
        private const int MaxSteps = 1000;

        private static long Run(double fps, double seconds)
        {
            var loop = new FixedStepLoop(Hz, MaxSteps);
            int frames = (int)System.Math.Round(fps * seconds);
            long steps = 0;
            for (int i = 0; i < frames; i++) steps += loop.Advance(1.0 / fps);
            Assert.AreEqual(steps, loop.StepsExecuted);
            return steps;
        }

        [TestCase(24.0)]
        [TestCase(30.0)]
        [TestCase(50.0)]
        [TestCase(60.0)]
        [TestCase(90.0)]
        [TestCase(120.0)]
        [TestCase(144.0)]
        public void StepCount_IsIndependentOfFrameRate(double fps)
        {
            Assert.AreEqual(500, Run(fps, 10.0), $"fps={fps}");
        }

        [Test]
        public void VariableFrameTimes_ProduceSameStepCount()
        {
            const ulong seed = 42UL;
            var rng = new Game.Core.Random.Rng(seed);
            var loop = new FixedStepLoop(Hz, MaxSteps);
            double total = 0;
            while (total < 10.0 - 1e-12)
            {
                double dt = System.Math.Min(0.005 + rng.NextDouble() * 0.045, 10.0 - total);
                total += dt;
                loop.Advance(dt);
            }
            Assert.AreEqual(500, loop.StepsExecuted, $"seed={seed}");
        }

        [Test]
        public void LongFrame_IsCapped_AndBacklogDropped()
        {
            var loop = new FixedStepLoop(Hz, 5);
            Assert.AreEqual(5, loop.Advance(1.0));
            Assert.AreEqual(45, loop.StepsDropped);
            Assert.AreEqual(1, loop.Advance(1.0 / Hz));
        }

        [Test]
        public void Alpha_ReportsPartialStep()
        {
            var loop = new FixedStepLoop(Hz, MaxSteps);
            loop.Advance(0.5 / Hz);
            Assert.AreEqual(0, loop.StepsExecuted);
            Assert.AreEqual(0.5, loop.Alpha, 1e-9);
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FixedStepLoop(0, 1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FixedStepLoop(Hz, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FixedStepLoop(Hz, 1).Advance(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FixedStepLoop(Hz, 1).Advance(double.NaN));
        }
    }
}
