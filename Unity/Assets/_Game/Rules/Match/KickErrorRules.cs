using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Rules.Match
{
    /// <summary>Circumstances of one pass or shot that scale its error (GAME_DESIGN §19/§20).</summary>
    public readonly struct KickContext
    {
        /// <summary>Distance to the closest opponent (<see cref="float.PositiveInfinity"/> when there is none).</summary>
        public readonly float NearestOpponentDistance;
        /// <summary>Unsigned angle between the body's facing and the kick direction (0-180°).</summary>
        public readonly float BodyAngleDegrees;
        public readonly bool WeakFoot;
        /// <summary>"De primeira": kicked on the step the ball arrived (buffered input).</summary>
        public readonly bool FirstTime;
        /// <summary>0-100.</summary>
        public readonly float Energy;
        /// <summary>Intended kick distance (m).</summary>
        public readonly float Distance;

        public KickContext(float nearestOpponentDistance, float bodyAngleDegrees, bool weakFoot, bool firstTime, float energy, float distance)
        {
            NearestOpponentDistance = nearestOpponentDistance;
            BodyAngleDegrees = bodyAngleDegrees;
            WeakFoot = weakFoot;
            FirstTime = firstTime;
            Energy = energy;
            Distance = distance;
        }
    }

    /// <summary>
    /// GAME_DESIGN §19/§20: "Erro = produto de fatores". Pure functions; every attribute effect goes through
    /// <see cref="Balance.Eval"/> and every other coefficient comes from Data/Balance/kicking.json (X-50).
    /// </summary>
    public static class KickErrorRules
    {
        private const float RadToDeg = 180f / MathF.PI;

        /// <summary>Signed angle from <paramref name="facing"/> to <paramref name="direction"/> in degrees
        /// (positive = to the left / counterclockwise, x length, y width).</summary>
        public static float SignedAngleDegrees(Vector2 facing, Vector2 direction)
        {
            float cross = facing.X * direction.Y - facing.Y * direction.X;
            float dot = facing.X * direction.X + facing.Y * direction.Y;
            return MathF.Atan2(cross, dot) * RadToDeg;
        }

        /// <summary>X-50: a kick aimed beyond <see cref="KickingCommon.WeakFootAngleDegrees"/> toward the
        /// preferred-foot side of the body is struck with the weak foot (a right-footer kicking hard to his right
        /// opens up with the left).</summary>
        public static bool UsesWeakFoot(Vector2 facing, Vector2 direction, bool leftFooted, KickingCommon common)
        {
            float signed = SignedAngleDegrees(facing, direction);
            return leftFooted ? signed > common.WeakFootAngleDegrees : signed < -common.WeakFootAngleDegrees;
        }

        /// <summary>0 within the free angle, rising linearly to <paramref name="maxPenalty"/> for a kick straight backwards.</summary>
        public static float OrientationPenalty(float bodyAngleDegrees, float freeAngleDegrees, float maxPenalty)
        {
            float t = MathUtil.InverseLerp(freeAngleDegrees, 180f, MathF.Abs(bodyAngleDegrees));
            return maxPenalty * MathUtil.Clamp01(t);
        }

        /// <summary>Weak foot 5 = 0, weak foot 1 = <paramref name="maxPenalty"/>, linear between.</summary>
        public static float WeakFootErrorPenalty(int weakFoot, float maxPenalty) =>
            maxPenalty * (MatchPlayerSetup.MaxWeakFoot - weakFoot) / (float)(MatchPlayerSetup.MaxWeakFoot - MatchPlayerSetup.MinWeakFoot);

        /// <summary>GAME_DESIGN §18: below the threshold, up to <paramref name="maxPenalty"/> at 0 energy.</summary>
        public static float LowEnergyPenalty(float energy, float threshold, float maxPenalty)
        {
            if (energy >= threshold) return 0f;
            return maxPenalty * MathUtil.Clamp01((threshold - energy) / threshold);
        }

        /// <summary>Product of every pass error factor (1 = clean conditions).</summary>
        public static float PassErrorMultiplier(Balance balance, MatchPlayerSetup player, KickingDefinition k, in KickContext ctx)
        {
            var p = k.Pass;
            float m = 1f;
            if (ctx.NearestOpponentDistance < p.PressureRadius) m *= balance.Eval(Effect.PressureErrorMult, player.Attributes);
            m *= 1f + OrientationPenalty(ctx.BodyAngleDegrees, k.Common.OrientationFreeAngleDegrees, p.OrientationMaxErrorPenalty);
            if (ctx.WeakFoot) m *= 1f + WeakFootErrorPenalty(player.WeakFoot, p.WeakFootMaxErrorPenalty);
            if (ctx.FirstTime) m *= 1f + p.FirstTimeErrorPenalty;
            m *= 1f + LowEnergyPenalty(ctx.Energy, k.Common.LowEnergyThreshold, p.LowEnergyMaxErrorPenalty);
            m *= 1f + p.DistanceErrorPerMeter * MathF.Max(0f, ctx.Distance - p.DistanceReference);
            return m;
        }

        /// <summary>Product of every shot error factor (1 = clean conditions).</summary>
        public static float ShotErrorMultiplier(Balance balance, MatchPlayerSetup player, KickingDefinition k, in KickContext ctx)
        {
            var s = k.Shot;
            float m = 1f;
            if (ctx.NearestOpponentDistance < s.PressureRadius)
                m *= 1f + (balance.Eval(Effect.PressureErrorMult, player.Attributes) - 1f) * s.PressureErrorScale;
            m *= 1f + OrientationPenalty(ctx.BodyAngleDegrees, k.Common.OrientationFreeAngleDegrees, s.OrientationMaxErrorPenalty);
            if (ctx.WeakFoot) m *= 1f + WeakFootErrorPenalty(player.WeakFoot, s.WeakFootMaxErrorPenalty);
            if (ctx.FirstTime) m *= 1f + s.FirstTimeErrorPenalty;
            m *= 1f + LowEnergyPenalty(ctx.Energy, k.Common.LowEnergyThreshold, s.LowEnergyMaxErrorPenalty);
            m *= 1f + s.DistanceErrorPerMeter * MathF.Max(0f, ctx.Distance - s.DistanceReference);
            return m;
        }

        /// <summary>Shot speed multiplier: pressure -10%, weak foot -10% (weak foot 4) to -25% (weak foot 1).</summary>
        public static float ShotPowerMultiplier(MatchPlayerSetup player, KickingDefinition k, in KickContext ctx)
        {
            var s = k.Shot;
            float m = 1f;
            if (ctx.NearestOpponentDistance < s.PressureRadius) m *= 1f - s.PressurePowerLoss;
            if (ctx.WeakFoot && player.WeakFoot < MatchPlayerSetup.MaxWeakFoot)
            {
                float t = (MatchPlayerSetup.MaxWeakFoot - 1 - player.WeakFoot) / (float)(MatchPlayerSetup.MaxWeakFoot - 1 - MatchPlayerSetup.MinWeakFoot);
                m *= 1f - MathUtil.Lerp(s.WeakFootPowerLossMin, s.WeakFootPowerLossMax, MathUtil.Clamp01(t));
            }
            return m;
        }

        /// <summary>Bell-shaped sample in (-1, 1): most kicks land near the intent, few at the edge of the error band.</summary>
        public static float Triangular(Rng rng) => rng.NextFloat() + rng.NextFloat() - 1f;
    }
}
