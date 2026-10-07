using System;
using System.Numerics;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// Function layer (TECHNICAL_SPEC §7 "Camada da função", 10 Hz): each slot's base position transformed by the
    /// block — the defensive line sits behind the ball (within the phase's min/max), the formation's vertical extent is
    /// scaled to the phase depth (compactação), the block shifts toward the ball's side (~50%), attackers stop at the
    /// offside line. Low Posicionamento adds a positioning error (AiTargetError) and a delay before big corrections
    /// (AiCorrectionDelay). Zero allocation.
    /// </summary>
    public static class RoleLayer
    {
        /// <summary>Ideal (error-free) target for one player, in world space.</summary>
        public static Vector3 IdealTarget(AiPlayer p, float ballU, float ballV, float offsideU, float pitchLength, float pitchHalfWidth, AiDefinition ai)
        {
            var team = p.Team;
            var t = team.Tactics;
            float shiftV = t.LateralShift * ballV;
            if (p.IsGoalkeeper) return team.Frame.World(ai.GoalkeeperDistance, ai.GoalkeeperLateralShift * ballV);

            var block = t.Phases[(int)team.Phase];
            float line = MathUtil.Clamp(ballU - block.LineBehindBall, t.MinLine, block.MaxLine);
            float span = team.SlotMaxX - team.SlotMinX;
            float rel = span > 1e-4f ? (p.Slot.X - team.SlotMinX) / span : 0f;
            float u = line + rel * block.Depth;
            u = MathF.Min(u, offsideU - ai.OffsideMargin);
            u = MathUtil.Clamp(u, 1f, pitchLength - 1f);

            float maxV = pitchHalfWidth - ai.SidelineMargin;
            float v = (0.5f - p.Slot.Y) * 2f * pitchHalfWidth * block.WidthScale + shiftV;
            v = MathUtil.Clamp(v, -maxV, maxV);
            return team.Frame.World(u, v);
        }

        /// <summary>Highest u (own frame) an attacker may reach: the second-last opponent, never behind the ball or in
        /// the own half.</summary>
        public static float OffsideU(AiTeam team, AiTeam opponents, float ballU, float pitchLength)
        {
            float first = float.MinValue, second = float.MinValue;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                float u = team.Frame.U(opponents.Players[i].Body.Position);
                if (u > first) { second = first; first = u; }
                else if (u > second) second = u;
            }
            return MathF.Max(MathF.Max(second, ballU), pitchLength * 0.5f);
        }

        /// <summary>Applies positioning error and correction delay; <paramref name="tick"/> is the layer period.</summary>
        public static void Commit(AiPlayer p, Vector3 ideal, Balance balance, AiDefinition ai, float tick)
        {
            p.IdealTarget = ideal;

            p.ErrorTimer -= tick;
            if (p.ErrorTimer <= 0f)
            {
                float magnitude = balance.Eval(Effect.AiTargetError, p.Setup.Attributes) * p.Team.Rng.NextFloat();
                float angle = p.Team.Rng.NextFloat() * 2f * MathF.PI;
                p.ErrorOffset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * magnitude;
                p.ErrorTimer = ai.ErrorResampleSeconds;
            }
            var target = ideal + new Vector3(p.ErrorOffset.X, p.ErrorOffset.Y, 0f);

            if (p.CorrectionTimer > 0f)
            {
                p.PendingTarget = target;
                p.CorrectionTimer -= tick;
                if (p.CorrectionTimer <= 0f) p.RoleTarget = p.PendingTarget;
                return;
            }
            if (Vector3.DistanceSquared(target, p.RoleTarget) > ai.CorrectionThreshold * ai.CorrectionThreshold)
            {
                p.PendingTarget = target;
                p.CorrectionTimer = balance.Eval(Effect.AiCorrectionDelay, p.Setup.Attributes);
                return;
            }
            p.RoleTarget = target;
        }

        /// <summary>Kick-off position: the slot squeezed into the own half (placeholder until restarts in A7).</summary>
        public static Vector3 KickoffPosition(AiPlayer p, float pitchLength, float pitchHalfWidth, AiDefinition ai)
        {
            var team = p.Team;
            if (p.IsGoalkeeper) return team.Frame.World(ai.GoalkeeperDistance, 0f);
            float u = p.Slot.X * (pitchLength * 0.5f - 1f);
            float v = (0.5f - p.Slot.Y) * 2f * pitchHalfWidth * team.Tactics.Phases[(int)Game.Data.Definitions.TeamPhase.Build].WidthScale;
            return team.Frame.World(u, v);
        }
    }
}
