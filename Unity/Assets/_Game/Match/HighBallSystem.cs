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
    /// <summary>
    /// "Alto" (A9a; GAME_DESIGN §19: "Lançamento | Alto fora da zona de cruzamento"; "Cruzamento alto | Alto na zona de
    /// cruzamento"). The Semi cone picks the target as for a ground pass; a lançamento lands at his feet, a cross arrives
    /// at head height. The launch speed for the wanted range comes from the closed-form flight under the same gravity and
    /// linear air drag the ball physics integrates. Error: CrossAngleError (cross) or PassAngleError × a long-ball factor,
    /// times the GAME_DESIGN factors (<see cref="KickErrorRules"/>), plus a speed error. Zero allocation.
    /// </summary>
    public static class HighBallSystem
    {
        private const float AimDeadzone = 0.1f;
        private const float DegToRad = MathF.PI / 180f;
        private const int SolveIterations = 32;
        private const float MinLaunchSpeed = 1f;

        /// <summary>Near the opponents' goal line and out wide: an Alto there is a cross.</summary>
        public static bool InCrossZone(Vector3 position, bool attackingPositiveX, Pitch pitch, AerialDefinition a)
        {
            float goalX = pitch.AttackedGoalLineX(attackingPositiveX);
            return MathF.Abs(goalX - position.X) <= a.CrossZoneDepth && MathF.Abs(position.Y) >= a.CrossZoneMinLateral;
        }

        /// <param name="aim">Stick direction; below the deadzone the body's facing is used.</param>
        /// <param name="power">0-1 from the hold time, or <see cref="ActionCommand.AutoPower"/> for a tap.</param>
        /// <param name="maxRange">Cap on the range (a throw-in), or <see cref="float.PositiveInfinity"/>.</param>
        public static PassOutcome Execute(int kicker, PlayerBody[] bodies, int count, Ball ball, MatchPlayerSetup player, Balance balance,
            KickingDefinition k, AerialDefinition a, BallParameters ballCfg, Pitch pitch, bool attackingPositiveX, bool cross,
            Vector2 aim, float power, bool firstTime, float nearestOpponentDistance, float maxRange, Rng rng)
        {
            var body = bodies[kicker];
            var facing = new Vector2(body.Facing.X, body.Facing.Y);
            Vector2 aimDir = aim.LengthSquared() > AimDeadzone * AimDeadzone ? Vector2.Normalize(aim) : facing;
            var from = new Vector2(ball.Position.X, ball.Position.Y);
            float g = ballCfg.Gravity, drag = ballCfg.AirDragCoefficient;
            float elevation = (cross ? a.CrossElevationDegrees : a.LongElevationDegrees) * DegToRad;
            float height = (cross ? a.CrossArrivalHeight : 0f) - ball.Position.Z;

            int target = PassSystem.FindTarget(kicker, bodies, count, aimDir, k.Pass.ConeHalfAngleDegrees,
                k.Pass.ConeDistanceWeightDegPerMeter, a.MaxTargetDistance);
            Vector2 point;
            if (target >= 0)
            {
                // Where the target will be when the ball comes down (one refinement of the flight time).
                var r = bodies[target];
                var rPos = new Vector2(r.Position.X, r.Position.Y);
                var rVel = new Vector2(r.Velocity.X, r.Velocity.Y);
                float d0 = Vector2.Distance(from, rPos);
                float t0 = FlightTime(d0, LaunchSpeed(d0, height, elevation, g, drag, a.MaxLaunchSpeed), elevation, drag);
                point = rPos + rVel * t0;
            }
            else if (cross)
            {
                // Nobody in the cone: into the middle of the box, in front of goal.
                float goalX = pitch.AttackedGoalLineX(attackingPositiveX);
                point = new Vector2(goalX - MathF.Sign(goalX) * a.CrossDefaultDepth, 0f);
            }
            else point = from + aimDir * a.SpaceDistance;

            Vector2 delta = point - from;
            float distance = delta.Length();
            Vector2 dir = distance > 1e-3f ? delta / distance : aimDir;
            float range = power >= 0f ? MathUtil.Lerp(a.MinDistance, a.MaxDistance, MathUtil.Clamp01(power)) : distance;
            range = MathF.Min(MathF.Max(range, 1f), maxRange);
            float speed = LaunchSpeed(range, height, elevation, g, drag, a.MaxLaunchSpeed);

            bool weakFoot = KickErrorRules.UsesWeakFoot(facing, dir, player.LeftFooted, k.Common);
            float bodyAngle = MathF.Abs(KickErrorRules.SignedAngleDegrees(facing, dir));
            var ctx = new KickContext(nearestOpponentDistance, bodyAngle, weakFoot, firstTime, body.Energy, range);
            float mult = KickErrorRules.PassErrorMultiplier(balance, player, k, ctx);
            float baseAngle = cross
                ? balance.Eval(Effect.CrossAngleError, player.Attributes)
                : balance.Eval(Effect.PassAngleError, player.Attributes) * a.LongErrorMultiplier;
            float angleError = baseAngle * mult * KickErrorRules.Triangular(rng);
            float speedError = balance.Eval(Effect.PassPowerError, player.Attributes) * mult * a.PowerErrorScale * KickErrorRules.Triangular(rng);
            speed = MathUtil.Clamp(speed * (1f + speedError), MinLaunchSpeed, a.MaxLaunchSpeed);

            Vector2 h = Rotate(dir, angleError * DegToRad);
            float cosE = MathF.Cos(elevation), sinE = MathF.Sin(elevation);
            Possession.Kick(kicker, body, ball, new Vector3(h.X * speed * cosE, h.Y * speed * cosE, speed * sinE));

            var landing = LandingPoint(ball, g, drag);
            return new PassOutcome(target, new Vector3(point.X, point.Y, 0f), landing, speed, angleError, weakFoot);
        }

        /// <summary>Launch speed at <paramref name="elevation"/> (rad) so the ball is <paramref name="height"/> m above its
        /// start after <paramref name="range"/> m (gravity <paramref name="g"/>, linear drag <paramref name="drag"/>);
        /// <paramref name="maxSpeed"/> when even that falls short.</summary>
        public static float LaunchSpeed(float range, float height, float elevation, float g, float drag, float maxSpeed)
        {
            float lo = MinLaunchSpeed, hi = maxSpeed;
            if (HeightAt(range, hi, elevation, g, drag) < height) return hi;
            for (int i = 0; i < SolveIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (HeightAt(range, mid, elevation, g, drag) < height) lo = mid;
                else hi = mid;
            }
            return hi;
        }

        /// <summary>Height above the start when the ball has travelled <paramref name="range"/> m horizontally
        /// (−∞ when drag stops it short).</summary>
        public static float HeightAt(float range, float speed, float elevation, float g, float drag)
        {
            float vz = speed * MathF.Sin(elevation);
            float t = FlightTime(range, speed, elevation, drag);
            if (float.IsInfinity(t)) return float.NegativeInfinity;
            if (drag <= 1e-6f) return vz * t - 0.5f * g * t * t;
            float e = 1f - MathF.Exp(-drag * t);
            return (vz + g / drag) / drag * e - g / drag * t;
        }

        /// <summary>Seconds to cover <paramref name="range"/> m horizontally (∞ if drag stops the ball first).</summary>
        public static float FlightTime(float range, float speed, float elevation, float drag)
        {
            float vh = speed * MathF.Cos(elevation);
            if (vh <= 1e-4f) return float.PositiveInfinity;
            if (drag <= 1e-6f) return range / vh;
            float q = range * drag / vh;
            return q >= 1f ? float.PositiveInfinity : -MathF.Log(1f - q) / drag;
        }

        /// <summary>Seconds until an airborne ball, coming down, is at <paramref name="height"/> (closed form under gravity and
        /// linear drag, searched in 20 ms steps then interpolated); <c>-1</c> if it is already below and falling, or never
        /// gets there within <paramref name="horizon"/>.</summary>
        public static float TimeToHeight(Ball ball, float height, float g, float drag, float horizon)
        {
            if (ball.State != BallState.Airborne) return -1f;
            const float step = 0.02f;
            float prev = ball.Position.Z;
            bool rising = ball.Velocity.Z > 0f;
            for (float t = step; t <= horizon + 1e-4f; t += step)
            {
                float z = HeightAfter(ball, t, g, drag);
                if (rising && z < prev) rising = false; // past the apex
                if (!rising && prev >= height && z < height)
                {
                    float f = (prev - height) / MathF.Max(1e-5f, prev - z);
                    return t - step + f * step;
                }
                prev = z;
            }
            return -1f;
        }

        /// <summary>Where an airborne ball is after <paramref name="t"/> seconds (no bounce).</summary>
        public static Vector3 PositionAfter(Ball ball, float t, float g, float drag)
        {
            float e = drag > 1e-6f ? (1f - MathF.Exp(-drag * t)) / drag : t;
            return new Vector3(ball.Position.X + ball.Velocity.X * e, ball.Position.Y + ball.Velocity.Y * e, HeightAfter(ball, t, g, drag));
        }

        /// <summary>Where the ball first comes down to the ground (its current position if it is not in the air).</summary>
        public static Vector3 LandingPoint(Ball ball, float g, float drag)
        {
            float t = TimeToHeight(ball, 0f, g, drag, 10f);
            if (t < 0f) return new Vector3(ball.Position.X, ball.Position.Y, 0f);
            var p = PositionAfter(ball, t, g, drag);
            return new Vector3(p.X, p.Y, 0f);
        }

        private static float HeightAfter(Ball ball, float t, float g, float drag)
        {
            float vz = ball.Velocity.Z;
            if (drag <= 1e-6f) return ball.Position.Z + vz * t - 0.5f * g * t * t;
            float e = 1f - MathF.Exp(-drag * t);
            return ball.Position.Z + (vz + g / drag) / drag * e - g / drag * t;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
