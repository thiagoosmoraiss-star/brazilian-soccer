using System.Numerics;

namespace Game.Match
{
    /// <summary>
    /// Separation (TECHNICAL_SPEC §5 "Contact | Separação, disputas, contato"; GAME_DESIGN §25 "jogadores como círculos
    /// ~0,4 m; contato por regra"): overlapping players are pushed apart equally so nobody walks through anybody.
    /// Body duels by Força are A9 ("disputa de corpo básica"). Zero allocation.
    /// </summary>
    public static class Contact
    {
        public static void Separate(PlayerBody[] bodies, int count, float playerRadius)
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
                    var push = n * ((min - d) * 0.5f);
                    a.Position -= push;
                    b.Position += push;
                }
            }
        }
    }
}
