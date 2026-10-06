using System.Numerics;

namespace Game.Match.Geometry
{
    /// <summary>A line segment in world units (meters; x length, y width, z height, TECHNICAL_SPEC §6).</summary>
    public readonly struct Segment3
    {
        public readonly Vector3 A, B;

        public Segment3(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }
    }
}
