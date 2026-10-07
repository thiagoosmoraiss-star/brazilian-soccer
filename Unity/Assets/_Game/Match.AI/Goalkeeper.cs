using System;
using System.Numerics;
using Game.Core.Math;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Match.AI
{
    /// <summary>What the keeper is doing (A6).</summary>
    public enum KeeperState
    {
        /// <summary>On the bisector of the shooting angle.</summary>
        Positioning = 0,
        /// <summary>A shot is on its way; the keeper has not reacted yet (GkReactionTime + penalties).</summary>
        Reacting = 1,
        /// <summary>Reacted: moving (or standing set) to where his hands meet the ball.</summary>
        Tracking = 2,
        /// <summary>Committed to a dive towards a point; covers distance at DiveSpeed, no teleport.</summary>
        Diving = 3,
        /// <summary>On the ground after a dive (GAME_DESIGN §23: "levanta em 0,6-1,0 s").</summary>
        Grounded = 4,
        /// <summary>Ball in his hands; distributes after a moment.</summary>
        Holding = 5,
    }

    /// <summary>One prediction of a loose ball's path against a keeper.</summary>
    public readonly struct KeeperScan
    {
        /// <summary>The path crosses the keeper's goal line inside the posts (or within the threat margin).</summary>
        public readonly bool Threat;
        /// <summary>Some point of the path is within reach in time; otherwise <see cref="Point"/> is the closest miss.</summary>
        public readonly bool Reachable;
        /// <summary>Ball position at the chosen intercept.</summary>
        public readonly Vector3 Point;
        /// <summary>Seconds from now until the ball is there.</summary>
        public readonly float Time;
        /// <summary>How far the body must move for the hands to meet it (m).</summary>
        public readonly float Required;

        public KeeperScan(bool threat, bool reachable, Vector3 point, float time, float required)
        {
            Threat = threat;
            Reachable = reachable;
            Point = point;
            Time = time;
            Required = required;
        }
    }

    /// <summary>Per-keeper state (one per team). Plain mutable holder; <see cref="Goalkeeper"/> and AiMatch update it.</summary>
    public sealed class Keeper
    {
        public readonly AiPlayer Player;
        /// <summary>Scratch ball the prediction integrates (allocated once: zero allocation per step).</summary>
        internal readonly Ball Scratch = new Ball();

        public KeeperState State;
        /// <summary>Reaction left (Reacting), get-up left (Grounded), dive left (Diving) or hold left (Holding).</summary>
        public float Timer;
        public Vector3 Target;
        /// <summary>The current or last save attempt needed a dive.</summary>
        public bool Dove;
        public float AngleErrorDegrees;
        internal float ErrorTimer;

        /// <summary>Reaction time applied to the last shot (s), for tests/tools.</summary>
        public float LastReactionSeconds;
        public int Saves;
        public int Catches;
        public int Parries;

        public Keeper(AiPlayer player)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void Reset()
        {
            State = KeeperState.Positioning;
            Timer = 0f;
            Dove = false;
            Target = Player.Body.Position;
        }
    }

    /// <summary>
    /// A6 goalkeeper (GAME_DESIGN §23; TECHNICAL_SPEC §7 "Goalkeeper | trajetória prevista → intenção + defesa"):
    /// positioning on the bisector of the shooting angle, advancing with the ball (and with the own defensive line, as
    /// a sweeper); reaction GkReactionTime (Reflexo) + penalties; reach limited by GkDiveReach and covered at a finite
    /// dive speed; catch or parry by GkCatchChance (Mãos), ball speed, dive, height and spin; parries rebound away
    /// from goal. The path is predicted with the very same <see cref="BallPhysics.Step"/> the ball uses, so the keeper
    /// never dives the wrong way without a deflection. Out of A6: 1×1 rush, crosses, special errors, tactical
    /// distribution. Zero allocation.
    /// </summary>
    public static class Goalkeeper
    {
        private const float DegToRad = MathF.PI / 180f;

        /// <summary>Where the keeper wants to stand: on the bisector of the angle ball → posts, at a depth that grows
        /// with the ball's distance (up to the sweeper depth set by the own defensive line), with his GkAngleError
        /// offset; never closer than MinDepth to the goal line, never wider than the posts (+LateralLimit).</summary>
        public static Vector3 PositionTarget(Keeper k, Vector3 ball, Pitch pitch, GoalkeeperDefinition g)
        {
            var team = k.Player.Team;
            var f = team.Frame;
            float bu = f.U(ball), bv = f.V(ball);
            float hw = pitch.HalfGoalWidth;

            // Angle bisector theorem: the bisector meets the goal line at the point dividing the posts in the ratio
            // of the ball's distances to them.
            float d1 = Length(bu, bv + hw), d2 = Length(bu, bv - hw);
            float gv = d1 + d2 > 1e-4f ? hw * (d1 - d2) / (d1 + d2) : 0f;
            float du = bu, dv = bv - gv;
            float len = Length(du, dv);
            if (len < 1e-3f || bu <= 0f) { du = 1f; dv = 0f; len = 1f; }
            du /= len;
            dv /= len;
            float err = k.AngleErrorDegrees * DegToRad;
            float c = MathF.Cos(err), s = MathF.Sin(err);
            float ru = du * c - dv * s, rv = du * s + dv * c;

            float lineU = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
                if (!team.Players[i].IsGoalkeeper) lineU = MathF.Min(lineU, f.U(team.Players[i].Body.Position));
            float full = MathUtil.Clamp(lineU * g.SweeperLineFraction, g.MaxDepth, g.SweeperMaxDepth);
            float ballDistance = Length(bu, bv);
            float t = MathUtil.Clamp01((ballDistance - g.NearBallDistance) / (g.FarBallDistance - g.NearBallDistance));
            float depth = MathUtil.Lerp(g.MinDepth, full, t);

            float u = MathF.Max(g.MinDepth, ru * depth);
            float v = MathUtil.Clamp(gv + rv * depth, -(hw + g.LateralLimit), hw + g.LateralLimit);
            return f.World(u, v);
        }

        /// <summary>Re-samples the positioning error (GkAngleError, Posicionamento GK) every ErrorResampleSeconds.</summary>
        public static void TickError(Keeper k, Balance balance, GoalkeeperDefinition g, Rng rng, float dt)
        {
            k.ErrorTimer -= dt;
            if (k.ErrorTimer > 0f) return;
            k.ErrorTimer = g.ErrorResampleSeconds;
            k.AngleErrorDegrees = balance.Eval(Effect.GkAngleError, k.Player.Setup.Attributes) * KickErrorRules.Triangular(rng);
        }

        /// <summary>True when a loose ball moves towards this keeper's goal line fast enough to get there within the
        /// prediction horizon (cheap filter before <see cref="Scan"/>).</summary>
        public static bool Incoming(Keeper k, Ball ball, GoalkeeperDefinition g)
        {
            if (ball.State != BallState.Rolling && ball.State != BallState.Airborne) return false;
            var f = k.Player.Team.Frame;
            float u = f.U(ball.Position);
            float vu = f.AttackingPositiveX ? ball.Velocity.X : -ball.Velocity.X; // speed along the team's attack
            if (vu >= -1e-3f) return false;
            return u / -vu <= g.PredictionHorizonSeconds;
        }

        /// <summary>Reaction for a shot that just appeared: GkReactionTime (Reflexo) plus the not-set penalty.</summary>
        public static float ReactionSeconds(Keeper k, Balance balance, GoalkeeperDefinition g)
        {
            var body = k.Player.Body;
            float speed = new Vector2(body.Velocity.X, body.Velocity.Y).Length();
            return balance.Eval(Effect.GkReactionTime, k.Player.Setup.Attributes) + (speed > g.UnsetSpeed ? g.UnsetPenaltySeconds : 0f);
        }

        /// <summary>Get-up time after a dive: proportional to the reaction time, within GAME_DESIGN's 0.6-1.0 s.</summary>
        public static float GetUpSeconds(Keeper k, Balance balance, GoalkeeperDefinition g) =>
            MathUtil.Clamp(g.GetUpPerReactionSecond * balance.Eval(Effect.GkReactionTime, k.Player.Setup.Attributes), g.GetUpMinSeconds, g.GetUpMaxSeconds);

        /// <summary>How far the body can move by diving (GkDiveReach is hands included).</summary>
        public static float DiveBudget(Keeper k, Balance balance, GoalkeeperDefinition g) =>
            MathF.Max(0f, balance.Eval(Effect.GkDiveReach, k.Player.Setup.Attributes) - g.HandReach);

        /// <summary>Distance a player covers running from rest for <paramref name="t"/> seconds (SprintSpeed, AccelTime).</summary>
        public static float RunCover(float t, float sprintSpeed, float accelTime)
        {
            if (t <= 0f) return 0f;
            if (accelTime <= 1e-3f || t >= accelTime) return sprintSpeed * (t - 0.5f * accelTime);
            return 0.5f * sprintSpeed / accelTime * t * t;
        }

        /// <summary>Distance the keeper's body can cover in <paramref name="t"/> seconds: running, or running then a full
        /// dive at the end (a step and a dive).</summary>
        public static float Cover(float t, float diveBudget, float diveSpeed, float sprintSpeed, float accelTime)
        {
            if (t <= 0f) return 0f;
            float diveTime = diveBudget / diveSpeed;
            float dive = t <= diveTime ? diveSpeed * t : diveBudget + RunCover(t - diveTime, sprintSpeed, accelTime);
            return MathF.Max(RunCover(t, sprintSpeed, accelTime), dive);
        }

        /// <summary>
        /// Predicts the loose ball's path (same physics as the real ball) and finds the earliest point the keeper can
        /// meet — running or diving after <paramref name="reactLeft"/> seconds — below his reach height, needing the least
        /// movement; if none, the closest miss. Also says whether the path threatens his goal.
        /// </summary>
        public static KeeperScan Scan(Keeper k, Ball ball, Pitch pitch, BallParameters cfg, Balance balance, GoalkeeperDefinition g,
            float reactLeft, float dt)
        {
            var body = k.Player.Body;
            var attrs = k.Player.Setup.Attributes;
            float sprint = balance.Eval(Effect.SprintSpeed, attrs);
            float accel = balance.Eval(Effect.AccelTime, attrs);
            float budget = DiveBudget(k, balance, g);
            float ownLineX = k.Player.Team.Frame.OwnGoal.X;

            var s = k.Scratch;
            s.CopyFrom(ball);
            int steps = Math.Max(1, (int)(g.PredictionHorizonSeconds / dt));
            bool threat = false, found = false;
            var point = ball.Position;
            float time = 0f, required = float.MaxValue, bestDeficit = float.MaxValue;
            for (int i = 1; i <= steps; i++)
            {
                var ev = BallPhysics.Step(s, pitch, cfg, dt);
                float t = i * dt;
                var p = s.Position;
                if (p.Z <= g.ReachHeight && (!found || required > 0f))
                {
                    float dx = p.X - body.Position.X, dy = p.Y - body.Position.Y;
                    float req = MathF.Max(0f, MathF.Sqrt(dx * dx + dy * dy) - g.HandReach);
                    float avail = t - reactLeft;
                    float cover = Cover(avail, budget, g.DiveSpeed, sprint, accel);
                    if (req <= cover && (req <= 0f || avail > 0f))
                    {
                        // Of the reachable points, the one needing the least movement (set if possible, no needless
                        // charge out at the ball); earliest on ties.
                        if (!found || req < required) { point = p; time = t; required = req; }
                        found = true;
                    }
                    else if (!found && req - cover < bestDeficit)
                    {
                        bestDeficit = req - cover;
                        point = p; time = t; required = req;
                    }
                }

                bool atOwnLine = MathF.Abs(p.X - ownLineX) < 1e-3f;
                if (ev == BallEvent.Goal) { threat = atOwnLine; break; }
                if (ev == BallEvent.Out)
                {
                    threat = atOwnLine && MathF.Abs(p.Y) <= pitch.HalfGoalWidth + g.ThreatMargin && p.Z <= pitch.GoalHeight + g.ThreatMargin;
                    break;
                }
                if (s.State == BallState.Dead || s.Velocity.LengthSquared() < 1e-4f) break;
            }
            return new KeeperScan(threat, found, point, time, required);
        }

        /// <summary>True if the ball, moving this step from its position by velocity × dt, passes within
        /// <paramref name="reach"/> of the keeper and below his reach height; <paramref name="contact"/> is that point.</summary>
        public static bool Touches(PlayerBody body, Ball ball, float reach, float reachHeight, float dt, out Vector3 contact)
        {
            var a = ball.Position;
            var d = ball.Velocity * dt;
            float lenSq = d.X * d.X + d.Y * d.Y;
            float t = lenSq > 1e-8f ? MathUtil.Clamp01(((body.Position.X - a.X) * d.X + (body.Position.Y - a.Y) * d.Y) / lenSq) : 0f;
            contact = a + d * t;
            float dx = contact.X - body.Position.X, dy = contact.Y - body.Position.Y;
            return dx * dx + dy * dy <= reach * reach && contact.Z <= reachHeight;
        }

        /// <summary>Catch chance: GkCatchChance (Mãos) × ball speed × dive × height × spin, capped.</summary>
        public static float CatchChance(Keeper k, float ballSpeed, float height, float spin, bool dove, Balance balance, GoalkeeperDefinition g)
        {
            float p = balance.Eval(Effect.GkCatchChance, k.Player.Setup.Attributes);
            p *= MathF.Max(g.CatchSpeedMultMin, 1f + (g.CatchReferenceSpeed - ballSpeed) * g.CatchSpeedSlope);
            if (dove) p *= g.DiveCatchFactor;
            if (height > g.HighBallHeight) p *= g.HighCatchFactor;
            p *= MathF.Max(0f, 1f - MathF.Abs(spin) * g.SpinCatchPenalty);
            return MathUtil.Clamp(p, 0f, g.MaxCatchChance);
        }

        /// <summary>A parry: the ball leaves away from the goal, angled towards the side it was travelling (or the side
        /// of the keeper it struck), losing most of its speed, with a little lift.</summary>
        public static Vector3 ParryVelocity(Keeper k, Ball ball, Vector3 contact, GoalkeeperDefinition g, Rng rng)
        {
            var f = k.Player.Team.Frame;
            var outward = f.Forward;
            var side = new Vector2(-outward.Y, outward.X);
            float lateral = ball.Velocity.X * side.X + ball.Velocity.Y * side.Y;
            if (MathF.Abs(lateral) < 0.5f)
                lateral = (contact.X - k.Player.Body.Position.X) * side.X + (contact.Y - k.Player.Body.Position.Y) * side.Y;
            float sign = lateral > 0f ? 1f : lateral < 0f ? -1f : (rng.NextFloat() < 0.5f ? -1f : 1f);
            float angle = MathUtil.Lerp(g.ParryMinAngleDegrees, g.ParryMaxAngleDegrees, rng.NextFloat()) * DegToRad;
            var dir = outward * MathF.Cos(angle) + side * (sign * MathF.Sin(angle));
            float speed = ball.Velocity.Length() * g.ParryRestitution;
            return new Vector3(dir.X * speed, dir.Y * speed, g.ParryLift);
        }

        /// <summary>Keeps the keeper's body in front of his goal line (GAME_DESIGN §23: "nunca andar para dentro do gol").</summary>
        public static void ClampInFront(Keeper k, GoalkeeperDefinition g)
        {
            var body = k.Player.Body;
            var f = k.Player.Team.Frame;
            float u = f.U(body.Position);
            if (u >= g.MinDepth) return;
            var fixedPos = f.World(g.MinDepth, f.V(body.Position));
            body.Position = new Vector3(fixedPos.X, fixedPos.Y, body.Position.Z);
            float vu = f.AttackingPositiveX ? body.Velocity.X : -body.Velocity.X;
            if (vu < 0f) body.Velocity = new Vector3(0f, body.Velocity.Y, body.Velocity.Z);
        }

        private static float Length(float a, float b) => MathF.Sqrt(a * a + b * b);
    }
}
