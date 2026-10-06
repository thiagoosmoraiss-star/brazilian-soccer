namespace Game.Data.Match
{
    /// <summary>Field geometry (Data/Balance/ball.json, A1, baseline v1, X-47): GAME_DESIGN §2 "105×68 m inicial".</summary>
    public sealed class PitchParameters
    {
        public float Length { get; internal set; }
        public float Width { get; internal set; }
        public float GoalWidth { get; internal set; }
        public float GoalHeight { get; internal set; }
        public float PostRadius { get; internal set; }
        /// <summary>Penalty area depth from the goal line (A3: "Finalização (área)" vs "Chute de longe (fora)").</summary>
        public float PenaltyAreaDepth { get; internal set; }
        public float PenaltyAreaWidth { get; internal set; }
    }

    /// <summary>Ball physics coefficients (Data/Balance/ball.json, A1, baseline v1, X-47; TECHNICAL_SPEC §6,
    /// GAME_DESIGN §25). Recalibrate here; no code change needed.</summary>
    public sealed class BallParameters
    {
        public float Radius { get; internal set; }
        public float Gravity { get; internal set; }
        /// <summary>Constant deceleration while <c>Rolling</c> (m/s²).</summary>
        public float RollingFrictionDeceleration { get; internal set; }
        /// <summary>Below this speed, a rolling ball is treated as stopped.</summary>
        public float MinRollingSpeed { get; internal set; }
        /// <summary>Linear drag applied to the full velocity while <c>Airborne</c> ("arrasto leve").</summary>
        public float AirDragCoefficient { get; internal set; }
        /// <summary>Vertical speed kept after a bounce, GAME_DESIGN §25: 0.45-0.60 (loses 40-55%).</summary>
        public float BounceVerticalRestitution { get; internal set; }
        /// <summary>Horizontal speed kept after a bounce, GAME_DESIGN §25: ~0.85 (loses ~15%).</summary>
        public float BounceHorizontalRetention { get; internal set; }
        /// <summary>Below this vertical speed right after a bounce, the ball becomes <c>Rolling</c> instead of bouncing again.</summary>
        public float MinBounceSpeedToStayAirborne { get; internal set; }
        /// <summary>Speed kept after hitting a post/crossbar ("reflexão com perda").</summary>
        public float PostRestitution { get; internal set; }
        /// <summary>Speed kept right after a <c>NetHit</c> ("amortecimento forte").</summary>
        public float NetDampingFactor { get; internal set; }
        /// <summary>Lateral acceleration per unit of spin while <c>Airborne</c> (m/s² per spin unit).</summary>
        public float SpinLateralAccelCoefficient { get; internal set; }
        /// <summary>Exponential decay rate of spin per second ("spin decai").</summary>
        public float SpinDecayPerSecond { get; internal set; }
        /// <summary>A step covering more than this distance is split into sub-steps ("sub-passos só em chute forte").</summary>
        public float SubstepDistance { get; internal set; }
        public int MaxSubsteps { get; internal set; }
    }

    /// <summary>Data/Balance/ball.json root: field geometry + ball physics (A1, X-47).</summary>
    public sealed class BallDefinition
    {
        public PitchParameters Pitch { get; internal set; }
        public BallParameters Ball { get; internal set; }
    }
}
