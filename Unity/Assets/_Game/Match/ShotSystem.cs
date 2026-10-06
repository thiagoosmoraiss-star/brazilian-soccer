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
    public readonly struct ShotOutcome
    {
        /// <summary>Point on the goal the shot was aimed at (before error).</summary>
        public readonly Vector3 AimPoint;
        public readonly float Speed;
        public readonly float HorizontalErrorDegrees;
        public readonly float VerticalErrorDegrees;
        public readonly bool InBox;
        public readonly bool WeakFoot;

        public ShotOutcome(Vector3 aimPoint, float speed, float horizontalErrorDegrees, float verticalErrorDegrees, bool inBox, bool weakFoot)
        {
            AimPoint = aimPoint;
            Speed = speed;
            HorizontalErrorDegrees = horizontalErrorDegrees;
            VerticalErrorDegrees = verticalErrorDegrees;
            InBox = inBox;
            WeakFoot = weakFoot;
        }
    }

    /// <summary>
    /// A3 shot (GAME_DESIGN §20; TECHNICAL_SPEC §5 "ShotSystem"): hold and release, power from the hold time, corner
    /// from the stick at release (neutral = the assistance picks the far corner), no cursor on the goal. Error by
    /// Finalização inside the box / Chute de longe outside, times the GAME_DESIGN factors; above the ideal power band
    /// the ball tends to fly over. Colocado and cabeceio are out of A3 (A9 / post-MVP). Zero allocation.
    /// </summary>
    public static class ShotSystem
    {
        private const float AimDeadzone = 0.1f;
        private const float DegToRad = MathF.PI / 180f;

        public static ShotOutcome Execute(int kicker, PlayerBody body, Ball ball, MatchPlayerSetup player, Balance balance,
            KickingDefinition k, BallParameters ballCfg, Pitch pitch, bool attackingPositiveX, Vector2 aim, float power,
            bool firstTime, float nearestOpponentDistance, Rng rng)
        {
            var s = k.Shot;
            power = MathUtil.Clamp01(power);
            var from = new Vector2(ball.Position.X, ball.Position.Y);
            var facing = new Vector2(body.Facing.X, body.Facing.Y);

            float goalX = pitch.AttackedGoalLineX(attackingPositiveX);
            float cornerY = ChooseCornerY(from, facing, aim, goalX, pitch, s);
            var aimPoint = new Vector3(goalX, cornerY, s.TargetHeight);

            Vector2 delta = new Vector2(aimPoint.X, aimPoint.Y) - from;
            float distance = MathF.Max(delta.Length(), 0.5f);
            Vector2 dir = delta / distance;

            bool inBox = pitch.InPenaltyArea(from.X, from.Y, attackingPositiveX);
            bool weakFoot = KickErrorRules.UsesWeakFoot(facing, dir, player.LeftFooted, k.Common);
            float bodyAngle = MathF.Abs(KickErrorRules.SignedAngleDegrees(facing, dir));
            var ctx = new KickContext(nearestOpponentDistance, bodyAngle, weakFoot, firstTime, body.Energy, distance);
            float mult = KickErrorRules.ShotErrorMultiplier(balance, player, k, ctx);

            float maxSpeed = balance.Eval(Effect.ShotPowerMax, player.Attributes);
            float speed = MathUtil.Lerp(s.MinSpeed, MathF.Max(s.MinSpeed, maxSpeed), power) * KickErrorRules.ShotPowerMultiplier(player, k, ctx);

            float baseAngle = balance.Eval(inBox ? Effect.ShotAngleErrorInBox : Effect.ShotAngleErrorOutOfBox, player.Attributes);
            float hError = baseAngle * mult * KickErrorRules.Triangular(rng);
            float vError = baseAngle * s.VerticalErrorFraction * mult * KickErrorRules.Triangular(rng);
            if (power > s.IdealPowerMax)
            {
                float excess = (power - s.IdealPowerMax) / (1f - s.IdealPowerMax);
                vError += s.OverPowerVerticalErrorDegrees * excess * (0.5f + 0.5f * rng.NextFloat());
            }

            float elevation = LaunchElevation(speed, distance, aimPoint.Z - ball.Position.Z, ballCfg.Gravity) + vError * DegToRad;
            Vector2 h = Rotate(dir, hError * DegToRad);
            float cosE = MathF.Cos(elevation), sinE = MathF.Sin(elevation);
            var velocity = new Vector3(h.X * cosE, h.Y * cosE, sinE) * speed;
            Possession.Kick(kicker, body, ball, velocity);

            return new ShotOutcome(aimPoint, speed, hError, vError, inBox, weakFoot);
        }

        /// <summary>Corner y on the goal line: the stick's sideways component (relative to the line to the goal)
        /// picks left or right; a neutral stick lets the assistance pick the far corner (GAME_DESIGN §20).</summary>
        public static float ChooseCornerY(Vector2 from, Vector2 facing, Vector2 aim, float goalX, Pitch pitch, ShotParameters s)
        {
            float cornerAbs = MathF.Max(0f, pitch.HalfGoalWidth - s.CornerInset);
            Vector2 toGoal = new Vector2(goalX - from.X, -from.Y);
            float len = toGoal.Length();
            Vector2 t = len > 1e-3f ? toGoal / len : new Vector2(MathF.Sign(goalX), 0f);
            var left = new Vector2(-t.Y, t.X);

            float side = 0f;
            if (aim.LengthSquared() > AimDeadzone * AimDeadzone)
            {
                float sideways = Vector2.Dot(Vector2.Normalize(aim), left);
                if (MathF.Abs(sideways) >= s.AimNeutralThreshold) side = sideways;
            }

            if (side == 0f)
            {
                // Far corner: away from the shooter's side of the goal; dead centre → follow the body's lean.
                if (MathF.Abs(from.Y) > 0.5f) return -MathF.Sign(from.Y) * cornerAbs;
                float lean = Vector2.Dot(facing, left);
                side = lean == 0f ? 1f : lean;
            }

            // Of the two corners, the one on the chosen side of the shooter→goal line.
            float scorePlus = Vector2.Dot(new Vector2(goalX - from.X, cornerAbs - from.Y), left);
            float scoreMinus = Vector2.Dot(new Vector2(goalX - from.X, -cornerAbs - from.Y), left);
            bool plusIsLeft = scorePlus > scoreMinus;
            return (side > 0f) == plusIsLeft ? cornerAbs : -cornerAbs;
        }

        /// <summary>Low-arc elevation (rad) to rise <paramref name="height"/> over <paramref name="distance"/> at
        /// <paramref name="speed"/> without drag; 45° when out of range.</summary>
        public static float LaunchElevation(float speed, float distance, float height, float gravity)
        {
            float v2 = speed * speed;
            float disc = v2 * v2 - gravity * (gravity * distance * distance + 2f * height * v2);
            if (disc < 0f) return MathF.PI / 4f;
            return MathF.Atan((v2 - MathF.Sqrt(disc)) / (gravity * distance));
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), sn = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * sn, v.X * sn + v.Y * c);
        }
    }
}
