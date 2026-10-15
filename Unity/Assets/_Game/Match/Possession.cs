using System;
using System.Numerics;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>Only ways <see cref="Ball.State"/> can change ownership this step (TECHNICAL_SPEC §7:
    /// "Possession | ... | Dono, eventos"; acceptance: "posse só muda por evento válido").</summary>
    public enum PossessionEvent
    {
        None = 0,
        Captured = 1,
        Released = 2,
    }

    /// <summary>
    /// Picks up a loose ball within capture range, lets a player let go of it, and records who kicked it.
    /// Who wins a ball several players reach is decided by the match (A9b body duel, <see cref="CanCapture"/>).
    /// </summary>
    public static class Possession
    {
        /// <param name="playerIndex">Slot index of <paramref name="body"/>; becomes <see cref="Ball.Owner"/> on capture.</param>
        /// <param name="intercepting">The ball was last kicked by the other side: a fast ball is only caught close to the
        /// body (<see cref="MovementDefinition.InterceptReferenceSpeed"/>).</param>
        public static PossessionEvent Step(int playerIndex, PlayerBody body, Ball ball, MovementDefinition mv, bool intercepting = false)
        {
            if (!CanCapture(body, ball, mv, intercepting, out _, out _)) return PossessionEvent.None;
            ball.State = BallState.Controlled;
            ball.Velocity = Vector3.Zero;
            ball.Spin = 0f;
            ball.Owner = playerIndex;
            ball.LastTouch = playerIndex;
            return PossessionEvent.Captured;
        }

        /// <summary>True when <paramref name="body"/> would capture the loose ball this step (the checks of
        /// <see cref="Step"/>, without taking it; a kicker's <see cref="PlayerBody.IgnoreBallUntilClear"/> may clear).
        /// <paramref name="distance"/> = ground distance to the ball, <paramref name="radius"/> = the capture radius used.</summary>
        public static bool CanCapture(PlayerBody body, Ball ball, MovementDefinition mv, bool intercepting, out float distance, out float radius)
        {
            distance = float.PositiveInfinity;
            radius = mv.PossessionCaptureRadius;
            if (ball.State == BallState.Controlled || ball.State == BallState.Dead) return false;
            float dx = body.Position.X - ball.Position.X;
            float dy = body.Position.Y - ball.Position.Y;
            float distSqr = dx * dx + dy * dy;
            if (intercepting)
            {
                float speed = MathF.Sqrt(ball.Velocity.X * ball.Velocity.X + ball.Velocity.Y * ball.Velocity.Y);
                if (speed > mv.InterceptReferenceSpeed) radius *= MathF.Max(mv.InterceptMinRadiusFraction, mv.InterceptReferenceSpeed / speed);
            }
            float radiusSqr = radius * radius;

            if (body.IgnoreBallUntilClear)
            {
                // Clear only once the ball is outside the radius AND moving away: a dribbled ball already sits at the
                // edge of the radius when kicked, and a kick across the body would otherwise come straight back.
                bool movingAway = -dx * ball.Velocity.X - dy * ball.Velocity.Y > 0f;
                if (distSqr > radiusSqr && (movingAway || ball.Velocity.LengthSquared() < 1e-4f)) body.IgnoreBallUntilClear = false;
                return false;
            }
            if (distSqr > radiusSqr) return false;
            if (ball.Position.Z > mv.PossessionCaptureMaxHeight) return false;
            distance = MathF.Sqrt(distSqr);
            return true;
        }

        /// <summary>Lets go of a controlled ball; it keeps its current velocity and becomes Rolling or Airborne
        /// depending on height.</summary>
        public static PossessionEvent Release(Ball ball)
        {
            if (ball.State != BallState.Controlled) return PossessionEvent.None;
            ball.Owner = Ball.NoPlayer;
            ball.State = ball.Position.Z > 0f || ball.Velocity.Z > 0f ? BallState.Airborne : BallState.Rolling;
            return PossessionEvent.Released;
        }

        /// <summary>A pass or shot leaving <paramref name="body"/>'s foot: records the touch and keeps the kicker
        /// from catching his own kick on the same step.</summary>
        public static void Kick(int playerIndex, PlayerBody body, Ball ball, Vector3 velocity)
        {
            ball.Kick(velocity, 0f);
            ball.LastTouch = playerIndex;
            body.IgnoreBallUntilClear = true;
        }
    }
}
