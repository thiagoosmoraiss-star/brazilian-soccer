using System;
using System.Collections.Generic;

namespace Game.Data.Effects
{
    public readonly struct CurvePoint
    {
        public readonly float X;
        public readonly float Y;

        public CurvePoint(float x, float y) { X = x; Y = y; }
    }

    /// <summary>Monotonicity declared for a curve in data; validated on load.</summary>
    public enum Monotonicity
    {
        None = 0,
        NonDecreasing = 1,
        NonIncreasing = 2,
    }

    /// <summary>
    /// Piecewise linear curve. Outside the first/last point the value is clamped to the endpoint.
    /// Construction does not validate; <see cref="BalanceValidator"/> reports invalid curves.
    /// </summary>
    public sealed class PiecewiseLinearCurve
    {
        private readonly CurvePoint[] _points;

        public PiecewiseLinearCurve(IReadOnlyList<CurvePoint> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            _points = new CurvePoint[points.Count];
            for (int i = 0; i < points.Count; i++) _points[i] = points[i];
        }

        public IReadOnlyList<CurvePoint> Points => _points;

        /// <summary>Evaluates the curve. Requires a valid curve (>= 1 point, strictly increasing X).</summary>
        public float Evaluate(float x)
        {
            var p = _points;
            if (p.Length == 0) throw new InvalidOperationException("Curve has no points.");
            if (x <= p[0].X) return p[0].Y;
            int last = p.Length - 1;
            if (x >= p[last].X) return p[last].Y;

            // Linear scan: curves have few points and this keeps evaluation allocation-free.
            for (int i = 1; i <= last; i++)
            {
                if (x <= p[i].X)
                {
                    var a = p[i - 1];
                    var b = p[i];
                    float t = (x - a.X) / (b.X - a.X);
                    return a.Y + (b.Y - a.Y) * t;
                }
            }
            return p[last].Y;
        }
    }
}
