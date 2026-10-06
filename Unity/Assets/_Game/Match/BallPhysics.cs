using System;
using System.Numerics;
using Game.Core.Diagnostics;
using Game.Core.Math;
using Game.Data.Match;
using Game.Match.Geometry;

namespace Game.Match
{
    /// <summary>
    /// Integrates <see cref="Ball"/> one fixed step at a time (TECHNICAL_SPEC §6, GAME_DESIGN §25): rolling with
    /// constant friction, airborne with gravity + light drag + decaying spin, bounce, post/crossbar reflection,
    /// goal and out-of-bounds. Zero allocation; the same code integrates for real and predicts (<see cref="Predict"/>).
    /// </summary>
    public static class BallPhysics
    {
        /// <summary>Advances the ball by <paramref name="dt"/> seconds, splitting into sub-steps for a fast ball
        /// ("sub-passos só em chute forte") so it cannot tunnel through a post or a boundary.</summary>
        public static BallEvent Step(Ball ball, Pitch pitch, BallParameters cfg, float dt, IDebugDraw draw = null)
        {
            if (ball.State == BallState.Dead || ball.State == BallState.Controlled) return BallEvent.None;

            float speed = ball.Velocity.Length();
            int substeps = 1;
            float moveThisFrame = speed * dt;
            if (moveThisFrame > cfg.SubstepDistance)
                substeps = Math.Min(cfg.MaxSubsteps, (int)MathF.Ceiling(moveThisFrame / cfg.SubstepDistance));
            float subDt = dt / substeps;

            // Stops at the first event rather than re-checking collisions within the same frame: right after a
            // bounce or a bar deflection the ball sits exactly at the contact distance, and a further sub-step
            // from there would immediately re-detect that same contact (its distance is still <= threshold) even
            // though the ball is now moving away — reflecting an already-receding ball sends it back the wrong way.
            BallEvent result = BallEvent.None;
            for (int i = 0; i < substeps; i++)
            {
                result = StepOnce(ball, pitch, cfg, subDt, draw);
                if (result != BallEvent.None || ball.State == BallState.Dead) break;
            }
            return result;
        }

        /// <summary>Predicted path from the ball's current state, sampled every <paramref name="sampleDt"/> up to
        /// <paramref name="horizonSeconds"/> or until it dies, drawn live (not collected: zero allocation). Runs the
        /// exact same <see cref="Step"/> on a clone, so prediction never disagrees with what actually happens.</summary>
        public static void Predict(Ball ball, Pitch pitch, BallParameters cfg, float horizonSeconds, float sampleDt, IDebugDraw draw)
        {
            if (draw == null) return;
            var clone = ball.Clone();
            float elapsed = 0f;
            Vector3 previous = clone.Position;
            while (elapsed < horizonSeconds && clone.State != BallState.Dead)
            {
                Step(clone, pitch, cfg, sampleDt, null);
                draw.Line(previous, clone.Position, DebugColor.Yellow);
                previous = clone.Position;
                elapsed += sampleDt;
            }
        }

        private static BallEvent StepOnce(Ball ball, Pitch pitch, BallParameters cfg, float dt, IDebugDraw draw)
        {
            Vector3 oldPos = ball.Position;
            Vector3 velocity = ball.Velocity;
            float spin = ball.Spin;
            bool wasAirborne = ball.State == BallState.Airborne;

            if (wasAirborne)
            {
                velocity.Z -= cfg.Gravity * dt;
                velocity -= velocity * cfg.AirDragCoefficient * dt;
                if (spin != 0f)
                {
                    Vector3 horizontal = new Vector3(velocity.X, velocity.Y, 0f);
                    if (horizontal.LengthSquared() > 1e-6f)
                    {
                        Vector3 lateral = new Vector3(-horizontal.Y, horizontal.X, 0f) / horizontal.Length();
                        velocity += lateral * (spin * cfg.SpinLateralAccelCoefficient * dt);
                    }
                    spin -= spin * cfg.SpinDecayPerSecond * dt;
                }
            }
            else
            {
                float speed = new Vector3(velocity.X, velocity.Y, 0f).Length();
                if (speed > 0f)
                {
                    float newSpeed = MathF.Max(0f, speed - cfg.RollingFrictionDeceleration * dt);
                    float scale = newSpeed / speed;
                    velocity.X *= scale;
                    velocity.Y *= scale;
                }
                velocity.Z = 0f;
            }

            Vector3 newPos = oldPos + velocity * dt;
            if (!wasAirborne) newPos.Z = 0f;

            if (TryBarCollision(pitch, oldPos, newPos, cfg, velocity, out Vector3 barPos, out Vector3 barVel))
            {
                ball.Position = barPos;
                ball.Velocity = barVel;
                ball.Spin = spin;
                draw?.Circle(barPos, cfg.Radius, DebugColor.Red);
                return BallEvent.PostHit;
            }

            bool crossedX = TryCrossGoalLineX(pitch, oldPos, newPos, out float tx);
            bool crossedY = TryCrossSidelineY(pitch, oldPos, newPos, out float ty);
            if (crossedX && (!crossedY || tx <= ty))
            {
                Vector3 p = Vector3.Lerp(oldPos, newPos, tx);
                bool isGoal = pitch.WithinGoalMouth(p.Y, p.Z);
                ball.Position = p;
                ball.Velocity = isGoal ? velocity * cfg.NetDampingFactor : Vector3.Zero;
                ball.Spin = 0f;
                ball.State = BallState.Dead;
                draw?.Circle(p, cfg.Radius, isGoal ? DebugColor.Green : DebugColor.White);
                return isGoal ? BallEvent.Goal : BallEvent.Out;
            }
            if (crossedY)
            {
                Vector3 p = Vector3.Lerp(oldPos, newPos, ty);
                ball.Position = p;
                ball.Velocity = Vector3.Zero;
                ball.Spin = 0f;
                ball.State = BallState.Dead;
                draw?.Circle(p, cfg.Radius, DebugColor.White);
                return BallEvent.Out;
            }

            if (wasAirborne && oldPos.Z > 0f && newPos.Z <= 0f)
            {
                float s = oldPos.Z / (oldPos.Z - newPos.Z);
                Vector3 bouncePos = Vector3.Lerp(oldPos, newPos, s);
                bouncePos.Z = 0f;
                Vector3 bounceVel = velocity;
                bounceVel.Z = -bounceVel.Z * cfg.BounceVerticalRestitution;
                bounceVel.X *= cfg.BounceHorizontalRetention;
                bounceVel.Y *= cfg.BounceHorizontalRetention;
                ball.Position = bouncePos;
                ball.Velocity = bounceVel;
                ball.Spin = spin;
                ball.State = MathF.Abs(bounceVel.Z) < cfg.MinBounceSpeedToStayAirborne ? BallState.Rolling : BallState.Airborne;
                return BallEvent.None;
            }

            ball.Position = newPos;
            ball.Velocity = velocity;
            ball.Spin = spin;
            if (!wasAirborne && new Vector3(velocity.X, velocity.Y, 0f).Length() < cfg.MinRollingSpeed) ball.Velocity = Vector3.Zero;
            return BallEvent.None;
        }

        private static bool TryBarCollision(Pitch pitch, Vector3 oldPos, Vector3 newPos, BallParameters cfg, Vector3 velocity,
            out Vector3 contactPos, out Vector3 reflectedVelocity)
        {
            float threshold = cfg.Radius + pitch.PostRadius;
            float thresholdSqr = threshold * threshold;
            float best = float.MaxValue;
            Segment3 bestBar = default;
            bool hit = false;

            CheckBar(pitch.HomeLeftPost, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);
            CheckBar(pitch.HomeRightPost, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);
            CheckBar(pitch.HomeCrossbar, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);
            CheckBar(pitch.AwayLeftPost, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);
            CheckBar(pitch.AwayRightPost, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);
            CheckBar(pitch.AwayCrossbar, oldPos, newPos, thresholdSqr, ref best, ref bestBar, ref hit);

            if (!hit)
            {
                contactPos = default;
                reflectedVelocity = default;
                return false;
            }

            // The segment-vs-segment check above only detects THAT the ball's path comes within range of a bar
            // (needed to avoid tunneling); a ball aimed exactly at the bar's axis makes its closest-point pair
            // degenerate (near-zero separation, no stable direction). The contact normal instead comes from the
            // ball's actual center (its position before this step) against that one bar: point-vs-segment is
            // always well-defined, and physically correct for a sphere hitting a cylinder.
            GeometryMath.ClosestPointSegmentSegment(oldPos, oldPos, bestBar.A, bestBar.B, out _, out Vector3 barPoint);
            Vector3 n = oldPos - barPoint;
            float len = n.Length();
            n = len > 1e-6f ? n / len : -Vector3.Normalize(velocity);

            // Sitting exactly at the contact distance right after a bounce is itself still "within threshold":
            // only a ball actually closing on the bar (velocity opposing the outward normal) is a real collision,
            // or the very next step would immediately re-detect contact on a ball already moving away and bounce
            // it right back.
            if (Vector3.Dot(velocity, n) >= 0f)
            {
                contactPos = default;
                reflectedVelocity = default;
                return false;
            }

            contactPos = barPoint + n * (cfg.Radius + pitch.PostRadius);
            Vector3 reflected = velocity - 2f * Vector3.Dot(velocity, n) * n;
            reflectedVelocity = reflected * cfg.PostRestitution;
            return true;
        }

        private static void CheckBar(Segment3 bar, Vector3 oldPos, Vector3 newPos, float thresholdSqr,
            ref float best, ref Segment3 bestBar, ref bool hit)
        {
            float distSqr = GeometryMath.ClosestPointSegmentSegment(oldPos, newPos, bar.A, bar.B, out _, out _);
            if (distSqr <= thresholdSqr && distSqr < best)
            {
                best = distSqr;
                bestBar = bar;
                hit = true;
            }
        }

        private static bool TryCrossGoalLineX(Pitch pitch, Vector3 oldPos, Vector3 newPos, out float t)
        {
            if (TryCrossPlaneX(pitch.HomeGoalLineX, oldPos, newPos, out t)) return true;
            if (TryCrossPlaneX(pitch.AwayGoalLineX, oldPos, newPos, out t)) return true;
            t = 0f;
            return false;
        }

        private static bool TryCrossPlaneX(float planeX, Vector3 oldPos, Vector3 newPos, out float t)
        {
            float a = oldPos.X - planeX;
            float b = newPos.X - planeX;
            if (a == 0f || (a > 0f) != (b > 0f))
            {
                t = MathUtil.Clamp01(a / (oldPos.X - newPos.X + (oldPos.X == newPos.X ? 1e-9f : 0f)));
                return true;
            }
            t = 0f;
            return false;
        }

        private static bool TryCrossSidelineY(Pitch pitch, Vector3 oldPos, Vector3 newPos, out float t)
        {
            if (TryCrossPlaneY(pitch.HalfWidth, oldPos, newPos, out t)) return true;
            if (TryCrossPlaneY(-pitch.HalfWidth, oldPos, newPos, out t)) return true;
            t = 0f;
            return false;
        }

        private static bool TryCrossPlaneY(float planeY, Vector3 oldPos, Vector3 newPos, out float t)
        {
            float a = oldPos.Y - planeY;
            float b = newPos.Y - planeY;
            if (a == 0f || (a > 0f) != (b > 0f))
            {
                t = MathUtil.Clamp01(a / (oldPos.Y - newPos.Y + (oldPos.Y == newPos.Y ? 1e-9f : 0f)));
                return true;
            }
            t = 0f;
            return false;
        }
    }
}
