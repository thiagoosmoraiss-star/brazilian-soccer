using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Match
{
    /// <summary>A3 passes (GAME_DESIGN §19): ground passes only; Alto (lançamento/cruzamento) comes in A9.</summary>
    public enum PassKind
    {
        /// <summary>Passe: tap = short pass with automatic force, hold = long ground pass with manual force.</summary>
        Ground = 0,
        /// <summary>Enfiada: into the space ahead of the receiver (Visão).</summary>
        Through = 1,
    }

    public readonly struct PassOutcome
    {
        /// <summary>Teammate the cone picked, or -1 when the pass went into space.</summary>
        public readonly int Target;
        /// <summary>Where the pass was meant to go (before error).</summary>
        public readonly Vector3 IntendedPoint;
        /// <summary>Where the ball will stop rolling (closed form, after error).</summary>
        public readonly Vector3 PredictedStop;
        public readonly float Speed;
        public readonly float AngleErrorDegrees;
        public readonly bool WeakFoot;

        public PassOutcome(int target, Vector3 intendedPoint, Vector3 predictedStop, float speed, float angleErrorDegrees, bool weakFoot)
        {
            Target = target;
            IntendedPoint = intendedPoint;
            PredictedStop = predictedStop;
            Speed = speed;
            AngleErrorDegrees = angleErrorDegrees;
            WeakFoot = weakFoot;
        }
    }

    /// <summary>
    /// Every pass of A3 (TECHNICAL_SPEC §5: "PassSystem | Todos os passes, cone, erro"). The direction comes from the
    /// stick, the Semi cone (±25°) snaps it to the best teammate inside, and the error is the product of the GAME_DESIGN
    /// factors (<see cref="KickErrorRules"/>). "O sistema ajuda a executar a escolha, nunca a corrige": with nobody in
    /// the cone the ball goes into space along the stick. Zero allocation.
    /// </summary>
    public static class PassSystem
    {
        private const float AimDeadzone = 0.1f;
        private const float DegToRad = MathF.PI / 180f;
        private const float RadToDeg = 180f / MathF.PI;

        /// <summary>Best teammate inside the cone around <paramref name="aim"/> (score = angle + distance × weight), or -1.</summary>
        public static int FindTarget(int kicker, PlayerBody[] bodies, int count, Vector2 aim, PassParameters p)
        {
            var from = bodies[kicker].Position;
            int best = -1;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (i == kicker) continue;
                float dx = bodies[i].Position.X - from.X;
                float dy = bodies[i].Position.Y - from.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < 1e-3f || dist > p.MaxTargetDistance) continue;
                float cos = MathUtil.Clamp((dx * aim.X + dy * aim.Y) / dist, -1f, 1f);
                float angle = MathF.Acos(cos) * RadToDeg;
                if (angle > p.ConeHalfAngleDegrees) continue;
                float score = angle + dist * p.ConeDistanceWeightDegPerMeter;
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        /// <param name="aim">Stick direction; below the deadzone the body's facing is used.</param>
        /// <param name="power">0-1 from the hold time, or <see cref="ActionCommand.AutoPower"/> for a tap.</param>
        /// <param name="attackDirection">Unit X/Y direction the team attacks (lead of a through ball to a static receiver).</param>
        public static PassOutcome Execute(int kicker, PlayerBody[] bodies, int count, Ball ball, MatchPlayerSetup player,
            Balance balance, KickingDefinition k, BallParameters ballCfg, PassKind kind, Vector2 aim, float power, bool firstTime,
            float nearestOpponentDistance, Vector2 attackDirection, Rng rng)
        {
            var p = k.Pass;
            var body = bodies[kicker];
            var facing = new Vector2(body.Facing.X, body.Facing.Y);
            Vector2 aimDir = aim.LengthSquared() > AimDeadzone * AimDeadzone ? Vector2.Normalize(aim) : facing;
            var from = new Vector2(ball.Position.X, ball.Position.Y);
            bool auto = power < 0f;
            float friction = ballCfg.RollingFrictionDeceleration;
            float arrival = (kind == PassKind.Through ? p.ThroughArrivalSpeed : p.ArrivalSpeed)
                            * balance.Eval(Effect.PassBallSpeed, player.Attributes);

            int target = FindTarget(kicker, bodies, count, aimDir, p);
            Vector2 point;
            if (target >= 0)
            {
                var r = bodies[target];
                var rPos = new Vector2(r.Position.X, r.Position.Y);
                var rVel = new Vector2(r.Velocity.X, r.Velocity.Y);
                // One-step estimate of the travel time, to aim where a moving receiver will be.
                float d0 = Vector2.Distance(from, rPos);
                float v0Estimate = AutoSpeed(arrival, d0, friction, p);
                float travel = 2f * d0 / (v0Estimate + arrival);
                point = rPos + rVel * travel;
                if (kind == PassKind.Through)
                {
                    float rSpeed = rVel.Length();
                    Vector2 leadDir = rSpeed > 0.1f ? rVel / rSpeed : attackDirection;
                    float lead = p.ThroughLeadDistance * (1f + balance.Eval(Effect.LeadCalcError, player.Attributes) * KickErrorRules.Triangular(rng));
                    point += leadDir * lead;
                }
            }
            else
            {
                point = from + aimDir * p.SpacePassDistance;
            }

            Vector2 delta = point - from;
            float distance = delta.Length();
            Vector2 dir = distance > 1e-3f ? delta / distance : aimDir;
            float speed = auto ? AutoSpeed(arrival, distance, friction, p) : MathUtil.Lerp(p.MinSpeed, p.MaxSpeed, MathUtil.Clamp01(power));

            bool weakFoot = KickErrorRules.UsesWeakFoot(facing, dir, player.LeftFooted, k.Common);
            float bodyAngle = MathF.Abs(KickErrorRules.SignedAngleDegrees(facing, dir));
            var ctx = new KickContext(nearestOpponentDistance, bodyAngle, weakFoot, firstTime, body.Energy, distance);
            float mult = KickErrorRules.PassErrorMultiplier(balance, player, k, ctx);

            float baseAngle = kind == PassKind.Through
                ? balance.Eval(Effect.ThroughBallError, player.Attributes)
                : balance.Eval(Effect.PassAngleError, player.Attributes);
            float angleError = baseAngle * mult * KickErrorRules.Triangular(rng);
            float powerError = balance.Eval(Effect.PassPowerError, player.Attributes) * mult * KickErrorRules.Triangular(rng);
            speed = MathF.Max(p.MinSpeed * 0.5f, speed * (1f + powerError));

            Vector2 finalDir = Rotate(dir, angleError * DegToRad);
            var velocity = new Vector3(finalDir.X, finalDir.Y, 0f) * speed;
            Possession.Kick(kicker, body, ball, velocity);

            float rollDistance = speed * speed / (2f * friction);
            var stop = new Vector3(from.X + finalDir.X * rollDistance, from.Y + finalDir.Y * rollDistance, 0f);
            return new PassOutcome(target, new Vector3(point.X, point.Y, 0f), stop, speed, angleError, weakFoot);
        }

        /// <summary>Launch speed so a rolling ball still has <paramref name="arrival"/> m/s after <paramref name="distance"/> m.</summary>
        public static float AutoSpeed(float arrival, float distance, float friction, PassParameters p) =>
            MathUtil.Clamp(MathF.Sqrt(arrival * arrival + 2f * friction * distance), p.MinSpeed, p.MaxSpeed);

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
