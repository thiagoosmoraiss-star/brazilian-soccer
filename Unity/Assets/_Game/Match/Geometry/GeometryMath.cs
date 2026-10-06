using System.Numerics;
using Game.Core.Math;

namespace Game.Match.Geometry
{
    /// <summary>Zero-allocation segment math for the ball's post/crossbar collision (A1).</summary>
    public static class GeometryMath
    {
        /// <summary>
        /// Closest points between two segments (Ericson, "Real-Time Collision Detection" §5.1.9) and the squared
        /// distance between them. No allocation, no branching on degenerate (point-like) segments skipped.
        /// </summary>
        public static float ClosestPointSegmentSegment(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float s, t;
            const float epsilon = 1e-9f;

            if (a <= epsilon && e <= epsilon)
            {
                c1 = p1;
                c2 = p2;
                return Vector3.DistanceSquared(c1, c2);
            }

            if (a <= epsilon)
            {
                s = 0f;
                t = MathUtil.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = MathUtil.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > epsilon ? MathUtil.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = MathUtil.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = MathUtil.Clamp01((b - c) / a); }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return Vector3.DistanceSquared(c1, c2);
        }
    }
}
