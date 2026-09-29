namespace Game.Core.Math
{
    /// <summary>
    /// Scalar helpers for the pure zone. Vectors use System.Numerics (never the Unity vector types).
    /// </summary>
    public static class MathUtil
    {
        public static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Position of <paramref name="value"/> between a and b (unclamped). Returns 0 when a == b.</summary>
        public static float InverseLerp(float a, float b, float value) =>
            a == b ? 0f : (value - a) / (b - a);
    }
}
