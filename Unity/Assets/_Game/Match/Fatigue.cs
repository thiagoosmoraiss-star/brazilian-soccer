using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>
    /// Energy spend/recovery (TECHNICAL_SPEC §7: "Fatigue | Gasto/recuperação normalizados"; GAME_DESIGN §18).
    /// Drain is normalized by the configured match duration so the total spent playing the same way for the
    /// whole match is equal at 4, 6 or 10 minutes.
    /// </summary>
    public static class Fatigue
    {
        public static void Drain(PlayerBody body, Balance balance, MatchPlayerSetup player, bool sprinting,
            FatigueDefinition fatigue, float configuredDurationMinutes, float dt)
        {
            float staminaMult = balance.Eval(Effect.EnergyDrainMult, player.Attributes);
            float ratePerMinute = fatigue.JogDrainPerMinuteAt6Min * (sprinting ? fatigue.SprintCostMultiplier : 1f)
                                   * staminaMult * (fatigue.ReferenceDurationMinutes / configuredDurationMinutes);
            float drainPerSecond = ratePerMinute / 60f;
            body.Energy = MathUtil.Clamp(body.Energy - drainPerSecond * dt, 0f, 100f);
        }

        public static void Recover(PlayerBody body, float amount) =>
            body.Energy = MathUtil.Clamp(body.Energy + amount, 0f, 100f);

        /// <summary>Speed/acceleration multiplier for the current energy (GAME_DESIGN §18: &lt; 60% loses up to
        /// -12%, scaling linearly to the full penalty at 0 energy).</summary>
        public static float SpeedMultiplier(PlayerBody body, FatigueDefinition fatigue)
        {
            float thresholdEnergy = fatigue.LowEnergyThreshold * 100f;
            if (body.Energy >= thresholdEnergy) return 1f;
            float severity = MathUtil.InverseLerp(thresholdEnergy, 0f, body.Energy);
            return 1f - fatigue.LowEnergySpeedPenalty * MathUtil.Clamp01(severity);
        }
    }
}
