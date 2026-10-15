using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Standing tackle (GAME_DESIGN §21/§22: "desarme em pé automático com bola exposta a &lt; ~1 m"; "1×1 sem dado:
    /// desarme só acerta se alcança a bola exposta"; X-53). No dice: a defender wins the ball when it is within his
    /// reach (<c>Effect.TackleReach</c>, Desarme) and he is closer to it than the carrier, who gets a head start from
    /// his body (<c>Effect.ShieldStrength</c>, Força). How far the carrier lets the ball run (Drible, the dribble touch
    /// distance) is what exposes it. Carrinho (A9b): a slide that meets the ball first wins it; body contact first is a foul (judged by the match). Zero allocation.
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

        // ---------------- carrinho (A9b) ----------------

        /// <summary>How far ahead a slide can meet the ball: the slide plus the outstretched leg (TackleReach × factor).</summary>
        public static float SlideRange(MatchPlayerSetup setup, Balance balance, DuelsDefinition d) =>
            d.SlideDistance + SlideReach(setup, balance, d);

        public static float SlideReach(MatchPlayerSetup setup, Balance balance, DuelsDefinition d) =>
            balance.Eval(Effect.TackleReach, setup.Attributes) * d.SlideReachFactor;

        /// <summary>Starts a slide along <paramref name="direction"/> (unit, Z=0) at the body's speed (within the data
        /// bounds), decelerating evenly so it covers <see cref="DuelsDefinition.SlideDistance"/>.</summary>
        public static void StartSlide(PlayerBody body, Vector3 direction, DuelsDefinition d)
        {
            float speed = MathUtil.Clamp(new Vector2(body.Velocity.X, body.Velocity.Y).Length(), d.SlideMinSpeed, d.SlideMaxSpeed);
            body.SlideDirection = direction;
            body.SlideSpeed = speed;
            body.SlideDeceleration = speed * speed / (2f * d.SlideDistance);
            body.SlideRemaining = speed / body.SlideDeceleration;
            body.SlideWonBall = false;
            body.GroundRemaining = 0f;
            body.Facing = direction;
            body.Sprinting = false;
            body.TurnStunRemaining = 0f;
        }

        /// <summary>One step of a slide or of lying on the ground (instead of <see cref="Movement.Step"/>).</summary>
        public static void SlideStep(PlayerBody body, DuelsDefinition d, float dt)
        {
            if (body.SlideRemaining > 0f)
            {
                float step = MathF.Min(dt, body.SlideRemaining);
                float v0 = body.SlideSpeed;
                float v1 = MathF.Max(0f, v0 - body.SlideDeceleration * step);
                body.Position += body.SlideDirection * (0.5f * (v0 + v1) * step);
                body.SlideSpeed = v1;
                body.SlideRemaining -= dt;
                body.Velocity = body.SlideDirection * v1;
                if (body.SlideRemaining <= 0f)
                {
                    body.SlideRemaining = 0f;
                    body.GroundRemaining = body.SlideWonBall ? d.SlideRecoverSeconds : d.SlideGroundSeconds;
                    body.Velocity = Vector3.Zero;
                }
                return;
            }
            body.GroundRemaining = MathF.Max(0f, body.GroundRemaining - dt);
            body.Velocity = Vector3.Zero;
        }

        /// <summary>The sliding leg meets the ball: within the slide reach and low enough.</summary>
        public static bool SlideMeetsBall(PlayerBody body, MatchPlayerSetup setup, Ball ball, Balance balance, DuelsDefinition d)
        {
            if (body.SlideRemaining <= 0f || body.SlideWonBall || ball.Position.Z > d.SlideBallMaxHeight) return false;
            return Distance2D(body, ball) <= SlideReach(setup, balance, d);
        }

        /// <summary>The slide wins the ball: it is poked on along the slide, the carrier (if any) stumbles.</summary>
        public static void SlideWin(int sliderIndex, PlayerBody slider, PlayerBody carrier, Ball ball, DefenseDefinition def, DuelsDefinition d)
        {
            slider.SlideWonBall = true;
            ball.Owner = Ball.NoPlayer;
            ball.State = BallState.Rolling;
            ball.Position = new Vector3(ball.Position.X, ball.Position.Y, 0f);
            ball.Velocity = slider.SlideDirection * d.SlidePokeSpeed;
            ball.Spin = 0f;
            ball.LastTouch = sliderIndex;
            slider.IgnoreBallUntilClear = true;
            if (carrier == null) return;
            carrier.TackleCooldown = def.ReTackleCooldownSeconds;
            carrier.TurnStunRemaining = MathF.Max(carrier.TurnStunRemaining, def.DispossessedStunSeconds);
            carrier.IgnoreBallUntilClear = true;
        }

        public static void Tick(PlayerBody body, float dt) => body.TackleCooldown = MathF.Max(0f, body.TackleCooldown - dt);

        private static float Distance2D(PlayerBody p, Ball b)
        {
            float dx = p.Position.X - b.Position.X, dy = p.Position.Y - b.Position.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }
}
