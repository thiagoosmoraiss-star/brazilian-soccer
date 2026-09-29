using System;

namespace Game.Core.Loop
{
    /// <summary>
    /// Generic fixed-step accumulator. The caller feeds variable frame time; the loop reports how many
    /// fixed steps to run so the number of steps depends only on total elapsed time, never on frame rate.
    /// Step rate and the per-frame step cap are supplied by the caller (from data), not hard-coded.
    /// </summary>
    public sealed class FixedStepLoop
    {
        // Tolerance for floating-point accumulation (e.g. 60 * (1/60) summing to 0.9999999).
        private const double Epsilon = 1e-9;

        private readonly double _stepsPerSecond;
        private readonly int _maxStepsPerAdvance;
        private double _elapsedSeconds;

        public double StepSeconds { get; }
        public long StepsExecuted { get; private set; }
        public long StepsDropped { get; private set; }

        /// <param name="stepsPerSecond">Fixed simulation rate (Hz). Must be positive.</param>
        /// <param name="maxStepsPerAdvance">Cap on steps per <see cref="Advance"/> call; excess time is dropped.</param>
        public FixedStepLoop(double stepsPerSecond, int maxStepsPerAdvance)
        {
            if (!(stepsPerSecond > 0) || double.IsInfinity(stepsPerSecond))
                throw new ArgumentOutOfRangeException(nameof(stepsPerSecond), stepsPerSecond, "Must be a positive finite rate.");
            if (maxStepsPerAdvance < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance), maxStepsPerAdvance, "Must be >= 1.");
            _stepsPerSecond = stepsPerSecond;
            _maxStepsPerAdvance = maxStepsPerAdvance;
            StepSeconds = 1.0 / stepsPerSecond;
        }

        /// <summary>Adds frame time and returns how many fixed steps must be executed now.</summary>
        public int Advance(double frameSeconds)
        {
            if (frameSeconds < 0 || double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds))
                throw new ArgumentOutOfRangeException(nameof(frameSeconds), frameSeconds, "Frame time must be finite and >= 0.");

            _elapsedSeconds += frameSeconds;
            long target = (long)System.Math.Floor(_elapsedSeconds * _stepsPerSecond + Epsilon);
            long due = target - StepsExecuted;
            if (due <= 0) return 0;

            int steps = due > _maxStepsPerAdvance ? _maxStepsPerAdvance : (int)due;
            if (steps < due)
            {
                // Drop the backlog (spiral-of-death guard): rebase elapsed time on the steps actually run.
                StepsDropped += due - steps;
                _elapsedSeconds = (StepsExecuted + steps) * StepSeconds;
            }
            StepsExecuted += steps;
            return steps;
        }

        /// <summary>Fraction [0,1) of the next step already accumulated; used by presentation to interpolate.</summary>
        public double Alpha
        {
            get
            {
                double a = _elapsedSeconds * _stepsPerSecond - StepsExecuted;
                return a < 0 ? 0 : (a >= 1 ? 0.999999 : a);
            }
        }
    }
}
