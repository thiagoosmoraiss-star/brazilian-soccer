using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Acceleration, turning, sprint and the with-ball penalty (TECHNICAL_SPEC §7: "Movement | Aceleração,
    /// curva, sprint, com bola"; GAME_DESIGN §18). Zero allocation; deterministic given the same inputs.
    /// </summary>
    public static class Movement
    {
        private const float DeadzoneMagnitude = 0.1f;
        private const float RadToDeg = 180f / MathF.PI;

        /// <param name="moveIntent">Desired direction (X/Y; magnitude ignored beyond the deadzone — the floating
        /// stick picks a direction, sprint is a separate hold, GAME_DESIGN §17).</param>
        /// <param name="hasBall">True while this player controls the ball (<see cref="Possession"/>).</param>
        /// <param name="longTouch">True during a sprint burst ("arrancada, toque longo"); a shorter, tighter
        /// penalty applies otherwise.</param>
        public static void Step(PlayerBody body, Balance balance, MatchPlayerSetup player, Vector2 moveIntent,
            bool sprintIntent, bool hasBall, bool longTouch, MovementDefinition mv, FatigueDefinition fatigue, float dt)
        {
            var raw = new Vector3(moveIntent.X, moveIntent.Y, 0f);
            float rawMag = raw.Length();
            bool moving = rawMag > DeadzoneMagnitude;
            Vector3 desiredDir = moving ? raw / rawMag : Vector3.Zero;

            float sprintSpeed = balance.Eval(Effect.SprintSpeed, player.Attributes);
            float jogRatio = balance.Eval(Effect.JogSpeed, player.Attributes);
            float targetSpeed = moving ? (sprintIntent ? sprintSpeed : jogRatio * sprintSpeed) : 0f;
            if (hasBall) targetSpeed *= 1f - (longTouch ? mv.WithBallSpeedPenaltyLongTouch : mv.WithBallSpeedPenaltyShortTouch);
            targetSpeed *= Fatigue.SpeedMultiplier(body, fatigue);

            float currentSpeed = new Vector3(body.Velocity.X, body.Velocity.Y, 0f).Length();
            Vector3 currentDir = currentSpeed > mv.StopSpeedEpsilon ? Vector3.Normalize(body.Velocity) : body.Facing;

            body.TurnStunRemaining = MathF.Max(0f, body.TurnStunRemaining - dt);
            if (body.TurnStunRemaining <= 0f && moving && currentSpeed > mv.StopSpeedEpsilon)
            {
                float dot = MathUtil.Clamp(Vector3.Dot(currentDir, desiredDir), -1f, 1f);
                float angleDeg = MathF.Acos(dot) * RadToDeg;
                if (angleDeg > mv.TurnMediumLossMaxDegrees)
                {
                    float loss = hasBall
                        ? balance.Eval(Effect.TurnSpeedLossWithBall, player.Attributes)
                        : balance.Eval(Effect.TurnSpeedLoss, player.Attributes);
                    currentSpeed *= 1f - loss;
                    body.TurnStunRemaining = balance.Eval(Effect.TurnRecoverTime, player.Attributes);
                }
                else if (angleDeg > mv.TurnNoLossMaxDegrees)
                {
                    currentSpeed *= 1f - mv.MediumTurnSpeedLossFlat;
                }
            }

            if (body.TurnStunRemaining > 0f)
            {
                // Stumbling from a sharp turn: keep moving along the old direction at the reduced speed, no
                // further acceleration until the stun clears.
                body.Velocity = currentDir * currentSpeed;
            }
            else
            {
                float accelTime = balance.Eval(Effect.AccelTime, player.Attributes);
                float decelTime = balance.Eval(Effect.DecelTime, player.Attributes);
                float accelRate = sprintSpeed / MathF.Max(accelTime, 0.01f);
                float decelRate = sprintSpeed / MathF.Max(decelTime, 0.01f);
                float newSpeed = targetSpeed > currentSpeed
                    ? MathF.Min(targetSpeed, currentSpeed + accelRate * dt)
                    : MathF.Max(targetSpeed, currentSpeed - decelRate * dt);
                Vector3 finalDir = moving ? desiredDir : currentDir;
                body.Velocity = finalDir * newSpeed;
                if (moving) body.Facing = desiredDir;
            }

            body.Position += body.Velocity * dt;
            body.Sprinting = sprintIntent && moving;
        }
    }
}
