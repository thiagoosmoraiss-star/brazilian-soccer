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
    /// Picks up a loose ball within capture range and lets a player let go of it. A1 has no other players yet,
    /// so there is no contest here — DefenseSystem (A5) adds that layer later.
    /// </summary>
    public static class Possession
    {
        public static PossessionEvent Step(PlayerBody body, Ball ball, MovementDefinition mv)
        {
            if (ball.State == BallState.Controlled || ball.State == BallState.Dead) return PossessionEvent.None;
            float dx = body.Position.X - ball.Position.X;
            float dy = body.Position.Y - ball.Position.Y;
            float distSqr = dx * dx + dy * dy;
            if (distSqr > mv.PossessionCaptureRadius * mv.PossessionCaptureRadius) return PossessionEvent.None;

            ball.State = BallState.Controlled;
            ball.Velocity = Vector3.Zero;
            ball.Spin = 0f;
            return PossessionEvent.Captured;
        }

        /// <summary>Lets go of a controlled ball (e.g. a pass/shot in a later stage); it keeps its current
        /// velocity and becomes Rolling or Airborne depending on height.</summary>
        public static PossessionEvent Release(Ball ball)
        {
            if (ball.State != BallState.Controlled) return PossessionEvent.None;
            ball.State = ball.Position.Z > 0f || ball.Velocity.Z > 0f ? BallState.Airborne : BallState.Rolling;
            return PossessionEvent.Released;
        }
    }
}
