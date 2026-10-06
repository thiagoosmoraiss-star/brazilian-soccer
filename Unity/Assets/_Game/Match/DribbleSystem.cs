using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Keeps a controlled ball just ahead of the player's feet (TECHNICAL_SPEC §6: "Posse | Bola presa à frente
    /// do pé"), covering A2's slice of DribbleSystem: conduzir (follow), cortar (direction changes, already
    /// handled by <see cref="Movement"/>'s turning) and arrancada (sprint burst = longer touch, GAME_DESIGN
    /// §21/§18). Finta de corpo and proteção need an opponent to matter against, so they are out of A2's scope
    /// (no other players yet) and come with DefenseSystem (A5).
    /// </summary>
    public static class DribbleSystem
    {
        /// <summary>True while the current touch is a long/sprint-burst one (for <see cref="Movement"/>'s
        /// with-ball speed penalty).</summary>
        public static bool IsLongTouch(PlayerBody body) => body.Sprinting;

        public static void Step(PlayerBody body, Ball ball, Balance balance, MatchPlayerSetup player, MovementDefinition mv)
        {
            if (ball.State != BallState.Controlled) return;

            float touchDistance = balance.Eval(Effect.DribbleTouchDistance, player.Attributes);
            if (body.Sprinting) touchDistance *= mv.SprintBurstTouchMultiplier;

            var dir = body.Velocity.LengthSquared() > 1e-4f ? Vector3.Normalize(body.Velocity) : body.Facing;
            ball.Position = body.Position + dir * (mv.PlayerRadius + touchDistance);
            ball.Position = new Vector3(ball.Position.X, ball.Position.Y, 0f);
            ball.Velocity = body.Velocity;
        }
    }
}
