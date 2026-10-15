using System;
using System.Numerics;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Match.AI
{
    public enum OnBallChoice
    {
        Dribble = 0,
        Pass = 1,
        Shot = 2,
        /// <summary>Alto (A9a): a cross from the cross zone, or a lançamento to <see cref="OnBallDecisionResult.Receiver"/>.</summary>
        High = 3,
        /// <summary>A through ball into the path of the teammate running in behind (A9b ruptura).</summary>
        Through = 4,
    }

    public readonly struct OnBallDecisionResult
    {
        public readonly OnBallChoice Choice;
        /// <summary>Team-local index of the pass receiver (Pass only).</summary>
        public readonly int Receiver;
        public readonly Vector3 DribbleTarget;

        public OnBallDecisionResult(OnBallChoice choice, int receiver, Vector3 dribbleTarget)
        {
            Choice = choice;
            Receiver = receiver;
            DribbleTarget = dribbleTarget;
        }
    }

    /// <summary>
    /// "Decisão com bola: nota = chance de sucesso × valor da situação; Visão define nº de opções; Compostura reduz
    /// ruído" (TECHNICAL_SPEC §7). Options: dribble forward (three headings), pass to the nearest teammates Visão
    /// lets the player see (AiPassOptionsCount), shoot when in range. Situation value grows with how far up the pitch
    /// the point is. Zero allocation.
    /// </summary>
    public static class OnBallDecision
    {
        private const int MaxSeen = 11;

        public static OnBallDecisionResult Decide(AiPlayer holder, AiTeam opponents, Pitch pitch, Balance balance, AiDefinition ai,
            AerialDefinition aerial = null, DuelsDefinition duels = null)
        {
            var team = holder.Team;
            var rng = team.Rng;
            var pos = holder.Body.Position;
            float nearestOpp = NearestOpponentDistance(pos, opponents);
            float noise = ai.DecisionNoise;
            if (nearestOpp < ai.ReceiverPressureRadius) noise *= balance.Eval(Effect.PressureErrorMult, holder.Setup.Attributes);

            // Dribble: headings fanned around the attack direction — within shooting range around the line to the goal,
            // so near the byline the carrier cuts inside instead of running along it — the one with the most room valued
            // at where it leads. A heading the pitch edge stops dead is no option.
            float bestValue = float.MinValue;
            var best = new OnBallDecisionResult(OnBallChoice.Dribble, -1, pos);
            var goalCentre = team.Frame.TargetGoal;
            var toGoal = new Vector2(goalCentre.X - pos.X, goalCentre.Y - pos.Y);
            bool inRange = toGoal.LengthSquared() < ai.ShotRange * ai.ShotRange && toGoal.LengthSquared() > 1e-4f;
            var fwd = inRange ? Vector2.Normalize(toGoal) : team.Frame.Forward;
            for (int h = 0; h < ai.DribbleHeadings; h++)
            {
                float angle = (h - (ai.DribbleHeadings - 1) * 0.5f) * ai.DribbleSpreadDegrees * MathF.PI / 180f;
                var dir = new Vector2(fwd.X * MathF.Cos(angle) - fwd.Y * MathF.Sin(angle), fwd.X * MathF.Sin(angle) + fwd.Y * MathF.Cos(angle));
                var target = ClampToPitch(pos + new Vector3(dir.X, dir.Y, 0f) * ai.DribbleStep, pitch, ai.SidelineMargin);
                if (Vector3.DistanceSquared(target, pos) < ai.DribbleStep * ai.DribbleStep * 0.25f) continue;
                // Room at the end of the run, and nobody standing on the way (a defender in reach takes the ball, A5).
                float success = MathUtil.Clamp01(NearestOpponentDistance(target, opponents) / ai.DribbleClearance);
                success = MathF.Min(success, MathUtil.Clamp01(1f - LaneRisk(pos, target, opponents, ai.DribbleClearance)));
                float value = success * ai.DribbleRiskFactor * Threat(target, team, pitch, ai) + noise * KickErrorRules.Triangular(rng);
                if (value > bestValue) { bestValue = value; best = new OnBallDecisionResult(OnBallChoice.Dribble, -1, target); }
            }

            // Passes: Visão limits how many teammates (nearest first) are considered.
            int seen = Math.Min(MaxSeen - 1, (int)MathF.Round(balance.Eval(Effect.AiPassOptionsCount, holder.Setup.Attributes)));
            float lastDistance = 0f;
            int lastIndex = -1;
            for (int n = 0; n < seen; n++)
            {
                int candidate = NextNearestTeammate(holder, lastDistance, lastIndex, out float d);
                if (candidate < 0) break;
                lastDistance = d;
                lastIndex = candidate;
                if (d > ai.PassMaxDistance) break;
                if (d < ai.PassMinDistance) continue;
                var receiver = team.Players[candidate].Body.Position;
                if (team.Frame.U(receiver) > team.OffsideU) continue; // he would be offside (A9b: runners end up there)
                float success = 1f - ai.PassDistanceRisk * d / ai.PassMaxDistance;
                success -= LaneRisk(pos, receiver, opponents, ai.PassLaneClearance);
                float receiverPressure = NearestOpponentDistance(receiver, opponents);
                if (receiverPressure < ai.ReceiverPressureRadius) success -= 1f - receiverPressure / ai.ReceiverPressureRadius;
                float value = MathUtil.Clamp01(success) * Threat(receiver, team, pitch, ai) + noise * KickErrorRules.Triangular(rng);
                if (value > bestValue) { bestValue = value; best = new OnBallDecisionResult(OnBallChoice.Pass, candidate, receiver); }
            }

            if (duels != null && !holder.IsGoalkeeper) RunnerOption(holder, opponents, pitch, ai, duels, noise, ref bestValue, ref best);
            if (aerial != null && !holder.IsGoalkeeper) HighOptions(holder, opponents, pitch, balance, ai, aerial, noise, ref bestValue, ref best);

            // Shot: chance falls with distance and with how little of the goal is visible, and with bodies in the way.
            float chance = ShotChance(pos, team, pitch, ai);
            if (chance > 0f)
            {
                var goal = team.Frame.TargetGoal;
                float value = chance * BlockSurvival(pos, goal, opponents, balance, ai.PassLaneClearance) + noise * KickErrorRules.Triangular(rng);
                if (value >= ai.ShootChanceThreshold || value > bestValue) best = new OnBallDecisionResult(OnBallChoice.Shot, -1, goal);
            }
            return best;
        }

        /// <summary>The teammate running in behind (A9b), still onside: a through ball into his path, valued at where the
        /// run leads (its threat), times the chance it gets through the lane.</summary>
        private static void RunnerOption(AiPlayer holder, AiTeam opponents, Pitch pitch, AiDefinition ai, DuelsDefinition d,
            float noise, ref float bestValue, ref OnBallDecisionResult best)
        {
            var team = holder.Team;
            if (team.Runner < 0 || team.Runner == holder.Local) return;
            var r = team.Players[team.Runner];
            if (r.RunRemaining <= 0f || team.Frame.U(r.Body.Position) > team.OffsideU - d.RunOnsideMargin) return; // gone offside: too late
            var pos = holder.Body.Position;
            var target = r.RunTarget;
            float dist = Vector3.Distance(pos, target);
            if (dist > ai.PassMaxDistance + d.RunDepthBehindLine) return;
            float success = 1f - ai.PassDistanceRisk * dist / (ai.PassMaxDistance + d.RunDepthBehindLine)
                            - LaneRisk(pos, target, opponents, ai.PassLaneClearance);
            float value = d.RunThroughValue * MathUtil.Clamp01(success) * Threat(target, team, pitch, ai)
                          + noise * KickErrorRules.Triangular(team.Rng);
            if (value > bestValue) { bestValue = value; best = new OnBallDecisionResult(OnBallChoice.Through, team.Runner, target); }
        }

        /// <summary>Alto options (A9a): from the cross zone, a cross valued by Cruzamento and how many teammates wait in the
        /// box; elsewhere, a lançamento to an open teammate far up the pitch. Receiver -1 = the default cross point.</summary>
        private static void HighOptions(AiPlayer holder, AiTeam opponents, Pitch pitch, Balance balance, AiDefinition ai, AerialDefinition a,
            float noise, ref float bestValue, ref OnBallDecisionResult best)
        {
            var team = holder.Team;
            var rng = team.Rng;
            var pos = holder.Body.Position;
            if (HighBallSystem.InCrossZone(pos, team.Frame.AttackingPositiveX, pitch, a))
            {
                int inBox = 0, target = -1;
                float bestOpen = -1f;
                for (int i = 0; i < team.Players.Length; i++)
                {
                    var q = team.Players[i];
                    if (q == holder || q.IsGoalkeeper || q.Body.OffPitch) continue;
                    var qp = q.Body.Position;
                    if (!pitch.InPenaltyArea(qp.X, qp.Y, team.Frame.AttackingPositiveX)) continue;
                    inBox++;
                    float open = NearestOpponentDistance(qp, opponents);
                    if (open > bestOpen) { bestOpen = open; target = i; }
                }
                // Nobody in the box: only a pressed winger puts it in anyway (a quarter of the value).
                bool pressed = NearestOpponentDistance(pos, opponents) < ai.ReceiverPressureRadius;
                if (inBox > 0 || pressed)
                {
                    float company = inBox > 0 ? MathF.Min(1f, inBox * 0.5f) : 0.25f;
                    float value = a.AiCrossValue * balance.Eval(Effect.CrossQuality, holder.Setup.Attributes) * company
                                  + noise * KickErrorRules.Triangular(rng);
                    if (value > bestValue) { bestValue = value; best = new OnBallDecisionResult(OnBallChoice.High, target, pos); }
                }
                return;
            }
            float u0 = team.Frame.U(pos);
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (q == holder || q.IsGoalkeeper || q.Body.OffPitch) continue;
                var qp = q.Body.Position;
                float d = Vector3.Distance(qp, pos);
                if (d < a.AiLongBallMinDistance || d > a.MaxTargetDistance || team.Frame.U(qp) <= u0 || team.Frame.U(qp) > team.OffsideU) continue;
                float open = MathUtil.Clamp01(NearestOpponentDistance(qp, opponents) / (2f * ai.ReceiverPressureRadius));
                float value = a.AiLongBallValue * open * Threat(qp, team, pitch, ai) + noise * KickErrorRules.Triangular(rng);
                if (value > bestValue) { bestValue = value; best = new OnBallDecisionResult(OnBallChoice.High, i, qp); }
            }
        }

        /// <summary>Clear-shot quality 0-1 from <paramref name="point"/>: falls with distance (0 beyond the shot range)
        /// and with how little of the goal is visible.</summary>
        public static float ShotChance(Vector3 point, AiTeam team, Pitch pitch, AiDefinition ai)
        {
            float d = Vector3.Distance(point, team.Frame.TargetGoal);
            if (d >= ai.ShotRange) return 0f;
            float angleFactor = MathUtil.Clamp01(VisibleGoalAngle(point, team, pitch) / ReferenceGoalAngle(pitch));
            return (1f - d / ai.ShotRange) * angleFactor;
        }

        /// <summary>Situation value 0-1 ("valor da situação"): the shooting chance from there, or — for build-up — a
        /// capped value of progress up the pitch (less out wide), whichever is higher.</summary>
        public static float Threat(Vector3 point, AiTeam team, Pitch pitch, AiDefinition ai)
        {
            float progress = MathUtil.Clamp01(team.Frame.U(point) / pitch.Length);
            float wide = MathUtil.Clamp01(MathF.Abs(point.Y) / pitch.HalfWidth);
            float build = ai.ProgressWeight * progress * progress * (1f - ai.LateralValuePenalty * wide);
            return MathF.Max(build, ShotChance(point, team, pitch, ai));
        }

        public static float NearestOpponentDistance(Vector3 point, AiTeam opponents)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                var o = opponents.Players[i].Body.Position;
                float dx = o.X - point.X, dy = o.Y - point.Y;
                best = MathF.Min(best, dx * dx + dy * dy);
            }
            return MathF.Sqrt(best);
        }

        /// <summary>Sum over opponents of how far inside <paramref name="clearance"/> they are from the segment.</summary>
        public static float LaneRisk(Vector3 from, Vector3 to, AiTeam opponents, float clearance)
        {
            var a = new Vector2(from.X, from.Y);
            var ab = new Vector2(to.X, to.Y) - a;
            float len2 = ab.LengthSquared();
            float risk = 0f;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                var o = opponents.Players[i].Body.Position;
                var ao = new Vector2(o.X, o.Y) - a;
                float t = len2 > 1e-6f ? MathUtil.Clamp01(Vector2.Dot(ao, ab) / len2) : 0f;
                float d = Vector2.Distance(a + ab * t, new Vector2(o.X, o.Y));
                if (d < clearance) risk += 1f - d / clearance;
            }
            return risk;
        }

        /// <summary>Probability a shot gets past the bodies in its lane: each opponent close to the line blocks it with his
        /// <c>ShotBlockChance</c> (Desarme), scaled by how close to the line he stands.</summary>
        public static float BlockSurvival(Vector3 from, Vector3 to, AiTeam opponents, Balance balance, float clearance)
        {
            var a = new Vector2(from.X, from.Y);
            var ab = new Vector2(to.X, to.Y) - a;
            float len2 = ab.LengthSquared();
            float survive = 1f;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                var o = opponents.Players[i];
                var op = new Vector2(o.Body.Position.X, o.Body.Position.Y);
                float t = len2 > 1e-6f ? MathUtil.Clamp01(Vector2.Dot(op - a, ab) / len2) : 0f;
                float d = Vector2.Distance(a + ab * t, op);
                if (d >= clearance) continue;
                survive *= 1f - balance.Eval(Effect.ShotBlockChance, o.Setup.Attributes) * (1f - d / clearance);
            }
            return survive;
        }

        /// <summary>Next teammate (excluding the holder) by distance after (<paramref name="afterDistance"/>, <paramref name="afterIndex"/>).</summary>
        private static int NextNearestTeammate(AiPlayer holder, float afterDistance, int afterIndex, out float distance)
        {
            var team = holder.Team;
            int best = -1;
            distance = float.PositiveInfinity;
            for (int i = 0; i < team.Players.Length; i++)
            {
                if (i == holder.Local || team.Players[i].Body.OffPitch) continue;
                float d = Vector3.Distance(team.Players[i].Body.Position, holder.Body.Position);
                bool after = d > afterDistance || (d == afterDistance && i > afterIndex);
                if (!after) continue;
                if (d < distance || (d == distance && i < best)) { distance = d; best = i; }
            }
            return best;
        }

        private static float VisibleGoalAngle(Vector3 pos, AiTeam team, Pitch pitch)
        {
            var left = team.Frame.World(pitch.Length, pitch.HalfGoalWidth);
            var right = team.Frame.World(pitch.Length, -pitch.HalfGoalWidth);
            var a = Vector2.Normalize(new Vector2(left.X - pos.X, left.Y - pos.Y));
            var b = Vector2.Normalize(new Vector2(right.X - pos.X, right.Y - pos.Y));
            return MathF.Acos(MathUtil.Clamp(Vector2.Dot(a, b), -1f, 1f));
        }

        /// <summary>Goal angle seen from the penalty spot (a "clear chance" reference).</summary>
        private static float ReferenceGoalAngle(Pitch pitch) => 2f * MathF.Atan(pitch.HalfGoalWidth / ReferenceShotDistance);

        private const float ReferenceShotDistance = 11f; // the penalty spot: a real-world fact, not a balance number

        private static Vector3 ClampToPitch(Vector3 p, Pitch pitch, float margin) =>
            new Vector3(MathUtil.Clamp(p.X, -pitch.HalfLength + margin, pitch.HalfLength - margin),
                MathUtil.Clamp(p.Y, -pitch.HalfWidth + margin, pitch.HalfWidth - margin), 0f);
    }
}
