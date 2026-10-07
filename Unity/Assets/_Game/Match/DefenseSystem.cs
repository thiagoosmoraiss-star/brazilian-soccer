using System;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Standing tackle (GAME_DESIGN §21/§22: "desarme em pé automático com bola exposta a &lt; ~1 m"; "1×1 sem dado:
    /// desarme só acerta se alcança a bola exposta"; X-53). No dice: a defender wins the ball when it is within his
    /// reach (<c>Effect.TackleReach</c>, Desarme) and he is closer to it than the carrier, who gets a head start from
    /// his body (<c>Effect.ShieldStrength</c>, Força). How far the carrier lets the ball run (Drible, the dribble touch
    /// distance) is what exposes it. Carrinho, the second defender and fouls come later (A8/A9). Zero allocation.
    /// </summary>
    public static class DefenseSystem
    {
        /// <summary>True when <paramref name="defender"/> can reach the carrier's ball and it is not covered by his body.</summary>
        public static bool BallExposedTo(PlayerBody defender, MatchPlayerSetup defenderSetup, PlayerBody carrier, MatchPlayerSetup carrierSetup,
            Ball ball, Balance balance, DefenseDefinition d)
        {
            float toDefender = Distance2D(defender, ball);
            if (toDefender > balance.Eval(Effect.TackleReach, defenderSetup.Attributes)) return false;
            float shield = balance.Eval(Effect.ShieldStrength, carrierSetup.Attributes) * d.ShieldMeters;
            return toDefender + shield < Distance2D(carrier, ball);
        }

        /// <summary>Automatic standing tackle; on success the defender owns the ball and the carrier stumbles.</summary>
        public static bool TryStandingTackle(int defenderIndex, PlayerBody defender, MatchPlayerSetup defenderSetup, PlayerBody carrier,
            MatchPlayerSetup carrierSetup, Ball ball, Balance balance, DefenseDefinition d)
        {
            if (ball.State != BallState.Controlled || defender.TackleCooldown > 0f) return false;
            if (!BallExposedTo(defender, defenderSetup, carrier, carrierSetup, ball, balance, d)) return false;

            ball.Owner = defenderIndex;
            ball.LastTouch = defenderIndex;
            ball.Velocity = System.Numerics.Vector3.Zero;
            defender.IgnoreBallUntilClear = false;
            carrier.TackleCooldown = d.ReTackleCooldownSeconds;
            carrier.TurnStunRemaining = MathF.Max(carrier.TurnStunRemaining, d.DispossessedStunSeconds);
            return true;
        }

        public static void Tick(PlayerBody body, float dt) => body.TackleCooldown = MathF.Max(0f, body.TackleCooldown - dt);

        private static float Distance2D(PlayerBody p, Ball b)
        {
            float dx = p.Position.X - b.Position.X, dy = p.Position.Y - b.Position.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
