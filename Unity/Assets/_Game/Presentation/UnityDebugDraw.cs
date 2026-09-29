using Game.Core.Diagnostics;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Renders <see cref="IDebugDraw"/> calls with UnityEngine.Debug lines (Scene view / Game view with Gizmos).
    /// Maps simulation axes (x length, y width, z height) to Unity axes (x, z, y).
    /// </summary>
    public sealed class UnityDebugDraw : IDebugDraw
    {
        private const int CircleSegments = 24; // rendering resolution only

        public void Line(System.Numerics.Vector3 from, System.Numerics.Vector3 to, DebugColor color)
        {
            UnityEngine.Debug.DrawLine(ToUnity(from), ToUnity(to), ToUnity(color));
        }

        public void Circle(System.Numerics.Vector3 center, float radius, DebugColor color)
        {
            var c = ToUnity(color);
            Vector3 prev = ToUnity(center) + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float a = i * (2f * Mathf.PI / CircleSegments);
                Vector3 next = ToUnity(center) + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                UnityEngine.Debug.DrawLine(prev, next, c);
                prev = next;
            }
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);
        private static Color ToUnity(DebugColor c) => new Color(c.R, c.G, c.B, c.A);
    }
}
