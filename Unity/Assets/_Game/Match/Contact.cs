using System.Numerics;

namespace Game.Match
{
    /// <summary>
    /// Separation (TECHNICAL_SPEC §5 "Contact | Separação, disputas, contato"; GAME_DESIGN §25 "jogadores como círculos
    /// ~0,4 m; contato por regra"): overlapping players are pushed apart so nobody walks through anybody; with strength
    /// weights (A9b, BodyDuelWin) the stronger body gives less ground. Zero allocation.
    /// </summary>
    public static class Contact
    {
        public static void Separate(PlayerBody[] bodies, int count, float playerRadius) => Separate(bodies, count, playerRadius, null);

        /// <summary>With <paramref name="strength"/> (one weight per body, A9b "disputa de corpo básica"), each body is
        /// moved by the other's share: the stronger one gives less ground. Null = equal split.</summary>
        public static void Separate(PlayerBody[] bodies, int count, float playerRadius, float[] strength)
        {
            float min = 2f * playerRadius;
            float minSqr = min * min;
            for (int i = 0; i < count; i++)
            {
                var a = bodies[i];
                for (int j = i + 1; j < count; j++)
                {
                    var b = bodies[j];
                    float dx = b.Position.X - a.Position.X, dy = b.Position.Y - a.Position.Y;
                    float dSqr = dx * dx + dy * dy;
                    if (dSqr >= minSqr) continue;
                    float d = System.MathF.Sqrt(dSqr);
                    // Exactly on top of each other: split along x, deterministically by index.
                    var n = d > 1e-5f ? new Vector3(dx / d, dy / d, 0f) : Vector3.UnitX;
                    float overlap = min - d;
                    float shareA = 0.5f;
                    if (strength != null)
                    {
                        float wa = strength[i], wb = strength[j];
                        shareA = wa + wb > 1e-6f ? wb / (wa + wb) : 0.5f;
                    }
                    a.Position -= n * (overlap * shareA);
                    b.Position += n * (overlap * (1f - shareA));
                }
            }
        }
    }
}
