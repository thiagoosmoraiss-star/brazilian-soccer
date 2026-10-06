using Game.Core.Results;
using Game.Data.Match;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Balance/movement.json and fatigue.json.</summary>
    public static class MovementReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidMovement = "INVALID_MOVEMENT";
        public const string InvalidFatigue = "INVALID_FATIGUE";

        public static Result<MovementDefinition> ReadMovement(string json)
        {
            var j = new StrictJson(GameDataLoader.MovementFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion,
                "playerRadius", "possessionCaptureRadius", "turnNoLossMaxDegrees", "turnMediumLossMaxDegrees",
                "mediumTurnSpeedLossFlat", "withBallSpeedPenaltyShortTouch", "withBallSpeedPenaltyLongTouch",
                "sprintBurstTouchMultiplier", "feintPulseMaxSeconds", "sprintMemorySeconds", "inputBufferSeconds",
                "stopSpeedEpsilon");
            if (root == null) return Result<MovementDefinition>.Fail(j.Errors);

            var m = new MovementDefinition
            {
                PlayerRadius = j.Float(root, "playerRadius", "root"),
                PossessionCaptureRadius = j.Float(root, "possessionCaptureRadius", "root"),
                TurnNoLossMaxDegrees = j.Float(root, "turnNoLossMaxDegrees", "root"),
                TurnMediumLossMaxDegrees = j.Float(root, "turnMediumLossMaxDegrees", "root"),
                MediumTurnSpeedLossFlat = j.Float(root, "mediumTurnSpeedLossFlat", "root"),
                WithBallSpeedPenaltyShortTouch = j.Float(root, "withBallSpeedPenaltyShortTouch", "root"),
                WithBallSpeedPenaltyLongTouch = j.Float(root, "withBallSpeedPenaltyLongTouch", "root"),
                SprintBurstTouchMultiplier = j.Float(root, "sprintBurstTouchMultiplier", "root"),
                FeintPulseMaxSeconds = j.Float(root, "feintPulseMaxSeconds", "root"),
                SprintMemorySeconds = j.Float(root, "sprintMemorySeconds", "root"),
                InputBufferSeconds = j.Float(root, "inputBufferSeconds", "root"),
                StopSpeedEpsilon = j.Float(root, "stopSpeedEpsilon", "root"),
            };

            if (!j.Ok) return Result<MovementDefinition>.Fail(j.Errors);
            bool ok = m.PlayerRadius > 0f && m.PossessionCaptureRadius > 0f
                      && m.TurnNoLossMaxDegrees > 0f && m.TurnMediumLossMaxDegrees > m.TurnNoLossMaxDegrees
                      && m.MediumTurnSpeedLossFlat >= 0f && m.MediumTurnSpeedLossFlat < 1f
                      && m.WithBallSpeedPenaltyShortTouch >= 0f && m.WithBallSpeedPenaltyShortTouch < 1f
                      && m.WithBallSpeedPenaltyLongTouch >= 0f && m.WithBallSpeedPenaltyLongTouch < 1f
                      && m.SprintBurstTouchMultiplier >= 1f && m.FeintPulseMaxSeconds > 0f
                      && m.SprintMemorySeconds >= 0f && m.InputBufferSeconds >= 0f && m.StopSpeedEpsilon > 0f;
            if (!ok) { j.Fail(InvalidMovement, "movement values invalid."); return Result<MovementDefinition>.Fail(j.Errors); }
            return Result<MovementDefinition>.Ok(m);
        }

        public static Result<FatigueDefinition> ReadFatigue(string json)
        {
            var j = new StrictJson(GameDataLoader.FatigueFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion,
                "referenceDurationMinutes", "jogDrainPerMinuteAt6Min", "sprintCostMultiplier", "lowEnergyThreshold",
                "lowEnergySpeedPenalty", "halftimeRecovery");
            if (root == null) return Result<FatigueDefinition>.Fail(j.Errors);

            var f = new FatigueDefinition
            {
                ReferenceDurationMinutes = j.Float(root, "referenceDurationMinutes", "root"),
                JogDrainPerMinuteAt6Min = j.Float(root, "jogDrainPerMinuteAt6Min", "root"),
                SprintCostMultiplier = j.Float(root, "sprintCostMultiplier", "root"),
                LowEnergyThreshold = j.Float(root, "lowEnergyThreshold", "root"),
                LowEnergySpeedPenalty = j.Float(root, "lowEnergySpeedPenalty", "root"),
                HalftimeRecovery = j.Float(root, "halftimeRecovery", "root"),
            };

            if (!j.Ok) return Result<FatigueDefinition>.Fail(j.Errors);
            bool ok = f.ReferenceDurationMinutes > 0f && f.JogDrainPerMinuteAt6Min > 0f && f.SprintCostMultiplier >= 1f
                      && f.LowEnergyThreshold > 0f && f.LowEnergyThreshold < 1f
                      && f.LowEnergySpeedPenalty >= 0f && f.LowEnergySpeedPenalty < 1f && f.HalftimeRecovery >= 0f;
            if (!ok) { j.Fail(InvalidFatigue, "fatigue values invalid."); return Result<FatigueDefinition>.Fail(j.Errors); }
            return Result<FatigueDefinition>.Ok(f);
        }
    }
}
