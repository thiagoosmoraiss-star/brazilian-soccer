using System;
using System.Numerics;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// Individual layer (TECHNICAL_SPEC §7 "Camada individual"), A4 slice: apoio (the closest teammates offer an open
    /// angle around the carrier), marcação por zona (an opponent inside the zone around the role target is marked
    /// goal-side; leaving the zone hands him over), a presser who contains without tackling (DefenseSystem is A5), one
    /// chaser per team for a loose ball (after the LooseBallReaction delay). Everyone else holds the shape. Assignments
    /// use hysteresis and a minimum commitment so intentions do not flicker. Zero allocation.
    /// </summary>
    public static class IndividualLayer
    {
        /// <summary>Team-rate refresh of who supports / presses / chases.</summary>
        public static void Assign(AiTeam team, AiTeam opponents, Ball ball, Balance balance, AiDefinition ai)
        {
            for (int i = 0; i < team.Players.Length; i++) { team.Supporter[i] = false; team.Presser[i] = false; }

            bool controlled = ball.State == BallState.Controlled && ball.Owner >= 0;
            if (team.HasPossession) PickNearest(team, ball.Position, ai.SupportPlayers, team.Supporter, controlled ? ball.Owner : -1, float.PositiveInfinity, ai, balance, false);
            else if (controlled) PickNearest(team, ball.Position, team.Tactics.Pressers, team.Presser, -1, team.Tactics.PressTriggerDistance, ai, balance, true);

            if (controlled || ball.State == BallState.Dead) { team.Chaser = -1; return; }

            // Loose ball: the player who would get there first (sprint + reaction), sticky by hysteresis.
            int best = -1;
            float bestTime = float.PositiveInfinity;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                if (p.IsGoalkeeper) continue;
                float time = Vector3.Distance(p.Body.Position, ball.Position) / balance.Eval(Effect.SprintSpeed, p.Setup.Attributes)
                             + balance.Eval(Effect.LooseBallReaction, p.Setup.Attributes);
                if (i == team.Chaser) time *= 1f - ai.AssignmentHysteresis;
                if (time < bestTime) { bestTime = time; best = i; }
            }
            team.Chaser = best;
        }

        /// <summary>Marks the <paramref name="count"/> outfield players nearest to <paramref name="point"/>; current
        /// holders of the job get the hysteresis discount.</summary>
        private static void PickNearest(AiTeam team, Vector3 point, int count, bool[] flags, int excludeGlobal, float maxDistance,
            AiDefinition ai, Balance balance, bool previousIsPress)
        {
            for (int n = 0; n < count; n++)
            {
                int best = -1;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < team.Players.Length; i++)
                {
                    var p = team.Players[i];
                    if (p.IsGoalkeeper || flags[i] || p.Global == excludeGlobal) continue;
                    float d = Vector3.Distance(p.Body.Position, point);
                    if (d > maxDistance) continue;
                    bool hadJob = previousIsPress ? p.Intention == AiIntention.Press : p.Intention == AiIntention.Support;
                    float score = hadJob ? d * (1f - ai.AssignmentHysteresis) : d;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) return;
                flags[best] = true;
            }
        }

        /// <summary>What <paramref name="p"/> should be doing now (before commitment rules).</summary>
        public static AiIntention Desired(AiPlayer p, AiTeam opponents, Ball ball, AiDefinition ai)
        {
            var team = p.Team;
            bool controlled = ball.State == BallState.Controlled && ball.Owner >= 0;
            if (controlled && ball.Owner == p.Global) return AiIntention.OnBall;
            if (p.IsGoalkeeper) return AiIntention.HoldShape;
            if (!controlled && ball.State != BallState.Dead && team.Chaser == p.Local) return AiIntention.ChaseBall;
            if (team.HasPossession) return team.Supporter[p.Local] ? AiIntention.Support : AiIntention.HoldShape;
            if (controlled && team.Presser[p.Local]) return AiIntention.Press;
            return MarkTarget(p, opponents, ball, ai, out _) >= 0 ? AiIntention.Mark : AiIntention.HoldShape;
        }

        public static bool IsValid(AiPlayer p, AiIntention intention, Ball ball)
        {
            bool controlled = ball.State == BallState.Controlled && ball.Owner >= 0;
            switch (intention)
            {
                case AiIntention.Support: return p.Team.HasPossession && ball.Owner != p.Global;
                case AiIntention.Mark: return !p.Team.HasPossession;
                case AiIntention.Press: return controlled && !p.Team.Owns(ball.Owner);
                case AiIntention.ChaseBall: return !controlled && ball.State != BallState.Dead;
                case AiIntention.OnBall: return controlled && ball.Owner == p.Global;
                default: return true;
            }
        }

        /// <summary>Most dangerous opponent (closest to our goal) inside the zone around the role target, except the
        /// carrier. A player already marking keeps a zone larger by the assignment hysteresis, so an opponent on the
        /// edge does not flip him between marking and holding.</summary>
        public static int MarkTarget(AiPlayer p, AiTeam opponents, Ball ball, AiDefinition ai, out Vector3 opponentPosition)
        {
            float zone = ai.MarkZoneRadius * (p.Intention == AiIntention.Mark ? 1f + ai.AssignmentHysteresis : 1f);
            int best = -1;
            float bestU = float.PositiveInfinity;
            opponentPosition = Vector3.Zero;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                var o = opponents.Players[i];
                if (o.Global == ball.Owner || o.IsGoalkeeper) continue;
                if (Vector3.DistanceSquared(o.Body.Position, p.RoleTarget) > zone * zone) continue;
                float u = p.Team.Frame.U(o.Body.Position);
                if (u < bestU) { bestU = u; best = i; opponentPosition = o.Body.Position; }
            }
            return best;
        }

        /// <summary>Where the intention wants the player to stand.</summary>
        public static Vector3 Target(AiPlayer p, AiTeam opponents, Ball ball, Pitch pitch, AiDefinition ai, float tick)
        {
            switch (p.Intention)
            {
                case AiIntention.Support:
                    p.SupportTimer -= tick;
                    if (p.SupportTimer <= 0f)
                    {
                        p.IntentTarget = SupportPoint(p, opponents, ball, pitch, ai);
                        p.SupportTimer = ai.SupportCommitSeconds;
                    }
                    return p.IntentTarget;
                case AiIntention.Mark:
                    if (MarkTarget(p, opponents, ball, ai, out var opp) < 0) return p.RoleTarget;
                    return GoalSide(opp, p.Team.Frame.OwnGoal, ai.MarkGoalSideDistance);
                case AiIntention.Press:
                    return GoalSide(ball.Position, p.Team.Frame.OwnGoal, ai.ContainDistance);
                case AiIntention.ChaseBall:
                    return new Vector3(ball.Position.X, ball.Position.Y, 0f);
                case AiIntention.OnBall:
                    return p.DribbleTarget;
                default:
                    return p.RoleTarget;
            }
        }

        private static Vector3 GoalSide(Vector3 from, Vector3 ownGoal, float distance)
        {
            var d = new Vector3(ownGoal.X - from.X, ownGoal.Y - from.Y, 0f);
            float len = d.Length();
            var p = len > 1e-3f ? from + d / len * distance : from;
            return new Vector3(p.X, p.Y, 0f);
        }

        /// <summary>Best of the candidate points on a ring around the carrier: open, forward, near the shape, not on
        /// top of the other supporter, never offside.</summary>
        public static Vector3 SupportPoint(AiPlayer p, AiTeam opponents, Ball ball, Pitch pitch, AiDefinition ai)
        {
            var team = p.Team;
            var carrier = ball.Position;
            float carrierU = team.Frame.U(carrier);
            var best = p.RoleTarget;
            float bestScore = float.NegativeInfinity;
            for (int k = 0; k < ai.SupportCandidates; k++)
            {
                float angle = k * 2f * MathF.PI / ai.SupportCandidates;
                var point = new Vector3(carrier.X + MathF.Cos(angle) * ai.SupportRadius, carrier.Y + MathF.Sin(angle) * ai.SupportRadius, 0f);
                point = new Vector3(MathUtil.Clamp(point.X, -pitch.HalfLength + ai.SidelineMargin, pitch.HalfLength - ai.SidelineMargin),
                    MathUtil.Clamp(point.Y, -pitch.HalfWidth + ai.SidelineMargin, pitch.HalfWidth - ai.SidelineMargin), 0f);
                float u = team.Frame.U(point);
                if (u > team.OffsideU - ai.OffsideMargin) continue;
                if (CrowdedBySupporter(p, point, ai.SupportRadius * 0.5f)) continue;

                float openness = MathF.Min(ai.SupportOpennessCap, OnBallDecision.NearestOpponentDistance(point, opponents)) / ai.SupportOpennessCap;
                float forward = (u - carrierU) / ai.SupportRadius;
                float shape = Vector3.Distance(point, p.RoleTarget);
                float score = ai.SupportOpennessWeight * openness + ai.SupportForwardWeight * forward - ai.SupportShapeWeight * shape;
                if (score > bestScore) { bestScore = score; best = point; }
            }
            return best;
        }

        private static bool CrowdedBySupporter(AiPlayer p, Vector3 point, float minSpacing)
        {
            var team = p.Team;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (q == p || q.Intention != AiIntention.Support) continue;
                if (Vector3.DistanceSquared(q.IntentTarget, point) < minSpacing * minSpacing) return true;
            }
            return false;
        }
    }
}
