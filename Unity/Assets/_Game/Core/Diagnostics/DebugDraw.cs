using System.Numerics;

namespace Game.Core.Diagnostics
{
    /// <summary>RGBA color for debug drawing (components in [0,1]).</summary>
    public readonly struct DebugColor
    {
        public readonly float R, G, B, A;

        public DebugColor(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public static readonly DebugColor White = new DebugColor(1f, 1f, 1f);
        public static readonly DebugColor Red = new DebugColor(1f, 0f, 0f);
        public static readonly DebugColor Green = new DebugColor(0f, 1f, 0f);
        public static readonly DebugColor Blue = new DebugColor(0f, 0f, 1f);
        public static readonly DebugColor Yellow = new DebugColor(1f, 1f, 0f);
    }

    /// <summary>
    /// Debug drawing abstraction usable from the pure zone (world units: meters; x length, y width, z height).
    /// The Unity zone renders it; headless runs use <see cref="NullDebugDraw"/>.
    /// </summary>
    public interface IDebugDraw
    {
        void Line(Vector3 from, Vector3 to, DebugColor color);

        /// <summary>Circle on the ground plane (z constant) around <paramref name="center"/>.</summary>
        void Circle(Vector3 center, float radius, DebugColor color);
    }

    public sealed class NullDebugDraw : IDebugDraw
    {
        public static readonly NullDebugDraw Instance = new NullDebugDraw();
        private NullDebugDraw() { }
        public void Line(Vector3 from, Vector3 to, DebugColor color) { }
        public void Circle(Vector3 center, float radius, DebugColor color) { }
    }
}
