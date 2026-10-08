namespace Game.Data.Match
{
    /// <summary>Non-attribute-dependent movement coefficients (Data/Balance/movement.json, A2, baseline v1, X-49).
    /// Attribute-dependent values (sprint speed, accel time, turn loss, touch distance...) come from the
    /// <c>Effect</c> catalog instead (GAME_DESIGN §18/21, TECHNICAL_SPEC §8, already baseline v1 since Stage 0).</summary>
    public sealed class MovementDefinition
    {
        public float PlayerRadius { get; internal set; }
        public float PossessionCaptureRadius { get; internal set; }
        /// <summary>A ball above this height cannot be controlled with the feet/chest (headers come in A9).</summary>
        public float PossessionCaptureMaxHeight { get; internal set; }
        /// <summary>A ball nobody on the player's side kicked last (an interception) is only caught within the full capture
        /// radius up to this speed (m/s); faster, the radius shrinks in proportion: a hard pass is not plucked out of the
        /// air by whoever it passes near (A7a, X-57).</summary>
        public float InterceptReferenceSpeed { get; internal set; }
        /// <summary>The interception radius never shrinks below this fraction of the capture radius (a ball straight at a
        /// player is still his).</summary>
        public float InterceptMinRadiusFraction { get; internal set; }
        public float TurnNoLossMaxDegrees { get; internal set; }
        public float TurnMediumLossMaxDegrees { get; internal set; }
        /// <summary>Flat speed loss for a 45-90° turn (GAME_DESIGN §18: 15-30%); sharper turns use the
        /// attribute-scaled <c>Effect.TurnSpeedLoss</c>/<c>TurnSpeedLossWithBall</c> instead (40-60%).</summary>
        public float MediumTurnSpeedLossFlat { get; internal set; }
        /// <summary>Speed penalty while dribbling with short touches (GAME_DESIGN §18: -10%).</summary>
        public float WithBallSpeedPenaltyShortTouch { get; internal set; }
        /// <summary>Speed penalty while dribbling with long touches/sprint burst (GAME_DESIGN §18: -5%).</summary>
        public float WithBallSpeedPenaltyLongTouch { get; internal set; }
        /// <summary>How much farther ahead the ball sits during a sprint burst ("arrancada, toque longo").</summary>
        public float SprintBurstTouchMultiplier { get; internal set; }
        public float FeintPulseMaxSeconds { get; internal set; }
        /// <summary>Sprint keeps going this long after release if the stick is still pushed (GAME_DESIGN §17).</summary>
        public float SprintMemorySeconds { get; internal set; }
        public float InputBufferSeconds { get; internal set; }
        public float StopSpeedEpsilon { get; internal set; }
    }

    /// <summary>Fatigue coefficients (Data/Balance/fatigue.json, A2, baseline v1, X-49; GAME_DESIGN §18). The
    /// attribute-dependent part (<c>Effect.EnergyDrainMult</c>, Resistência) is already baseline v1 since Stage 0.</summary>
    public sealed class FatigueDefinition
    {
        /// <summary>Baseline rates are defined for this duration; drain is normalized by multiplying the
        /// per-minute rate by this / the configured duration, so total spend over a full match is the same
        /// regardless of the configured duration (4, 6 or 10 minutes).</summary>
        public float ReferenceDurationMinutes { get; internal set; }
        public float JogDrainPerMinuteAt6Min { get; internal set; }
        /// <summary>Sprinting drains 5-6x the jog rate (GAME_DESIGN §18).</summary>
        public float SprintCostMultiplier { get; internal set; }
        /// <summary>Energy fraction (0-1) below which speed/acceleration are penalized (GAME_DESIGN §18: &lt; 60%).</summary>
        public float LowEnergyThreshold { get; internal set; }
        /// <summary>Maximum speed/acceleration penalty at 0 energy, scaling down to 0 at the threshold (GAME_DESIGN §18: up to -12%).</summary>
        public float LowEnergySpeedPenalty { get; internal set; }
        public float HalftimeRecovery { get; internal set; }
    }
}
