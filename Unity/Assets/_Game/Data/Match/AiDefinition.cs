namespace Game.Data.Match
{
    /// <summary>Block shape for one phase at a given tactic (Data/Balance/tactics.json, A4 baseline v1, X-52).
    /// Distances in meters, measured from the team's own goal line along its attack.</summary>
    public sealed class PhaseBlock
    {
        /// <summary>Distance from the defensive line to the most advanced line (GAME_DESIGN §24: compactação).</summary>
        public float Depth { get; internal set; }
        /// <summary>How far behind the ball the defensive line sits.</summary>
        public float LineBehindBall { get; internal set; }
        /// <summary>Highest the defensive line may push.</summary>
        public float MaxLine { get; internal set; }
        /// <summary>Lateral spread of the formation (1 = full pitch width).</summary>
        public float WidthScale { get; internal set; }
    }

    /// <summary>Tactic-dependent AI parameters (TECHNICAL_SPEC §9). A4 only has the default tactic; per-level values
    /// (mentalidade, linha, pressão) come with TacticsRuntime (A9).</summary>
    public sealed class TacticsDefinition
    {
        /// <summary>Indexed by <c>TeamPhase</c>.</summary>
        public PhaseBlock[] Phases { get; internal set; }
        /// <summary>Deepest the defensive line may drop.</summary>
        public float MinLine { get; internal set; }
        /// <summary>Fraction of the ball's lateral position the block follows (GAME_DESIGN §24: ~40-60%).</summary>
        public float LateralShift { get; internal set; }
        public int Pressers { get; internal set; }
        /// <summary>A presser only engages a carrier within this distance.</summary>
        public float PressTriggerDistance { get; internal set; }
    }

    /// <summary>Tactic-independent AI coefficients (Data/Balance/ai.json, A4 baseline v1, X-52; TECHNICAL_SPEC §7,
    /// GAME_DESIGN §24). Attribute-dependent values come from the Effect catalog (AiTargetError, AiCorrectionDelay,
    /// AiPassOptionsCount, LooseBallReaction, PressureErrorMult).</summary>
    public sealed class AiDefinition
    {
        public float TeamHz { get; internal set; }
        public float RoleHz { get; internal set; }
        public float IndividualHz { get; internal set; }

        /// <summary>GAME_DESIGN §24: transição ofensiva 2-4 s.</summary>
        public float TransitionAttackSeconds { get; internal set; }
        /// <summary>GAME_DESIGN §24: transição defensiva 2-3 s.</summary>
        public float TransitionDefenseSeconds { get; internal set; }
        /// <summary>In possession, the phase is Build while the ball is below this fraction of the pitch (own frame).</summary>
        public float BuildMaxBallFraction { get; internal set; }

        public float OffsideMargin { get; internal set; }
        public float GoalkeeperDistance { get; internal set; }
        public float GoalkeeperLateralShift { get; internal set; }
        public float SidelineMargin { get; internal set; }
        /// <summary>How often each player's positioning error is re-drawn.</summary>
        public float ErrorResampleSeconds { get; internal set; }
        /// <summary>A role target change smaller than this is applied at once; larger ones wait AiCorrectionDelay.</summary>
        public float CorrectionThreshold { get; internal set; }

        public int SupportPlayers { get; internal set; }
        public int SupportCandidates { get; internal set; }
        public float SupportRadius { get; internal set; }
        public float SupportOpennessCap { get; internal set; }
        public float SupportOpennessWeight { get; internal set; }
        public float SupportForwardWeight { get; internal set; }
        public float SupportShapeWeight { get; internal set; }
        public float SupportCommitSeconds { get; internal set; }
        public float MarkZoneRadius { get; internal set; }
        public float MarkGoalSideDistance { get; internal set; }
        /// <summary>A presser stops this far from the carrier (no tackling before A5).</summary>
        public float ContainDistance { get; internal set; }
        /// <summary>Minimum time an intention is kept before switching (anti-oscillation).</summary>
        public float MinCommitSeconds { get; internal set; }
        /// <summary>The current supporter/presser/chaser keeps the job unless another player is this much (fraction)
        /// better placed (GAME_DESIGN §17 uses ~25% for control switching).</summary>
        public float AssignmentHysteresis { get; internal set; }
        /// <summary>A player going for a moving ball runs to where he meets it, searched this far ahead…</summary>
        public float InterceptHorizonSeconds { get; internal set; }
        /// <summary>…in steps of this many seconds (A7a, X-57).</summary>
        public float InterceptStepSeconds { get; internal set; }

        public float DecisionIntervalSeconds { get; internal set; }
        public float MinDribbleSeconds { get; internal set; }
        public float DribbleStep { get; internal set; }
        /// <summary>Number of dribble headings considered, fanned around the attack direction.</summary>
        public int DribbleHeadings { get; internal set; }
        /// <summary>Angle between neighbouring dribble headings.</summary>
        public float DribbleSpreadDegrees { get; internal set; }
        public float DribbleClearance { get; internal set; }
        /// <summary>Teammates closer than this are not pass options (no ping-pong between players side by side).</summary>
        public float PassMinDistance { get; internal set; }
        public float PassMaxDistance { get; internal set; }
        public float PassLaneClearance { get; internal set; }
        public float PassDistanceRisk { get; internal set; }
        public float ReceiverPressureRadius { get; internal set; }
        public float ShotRange { get; internal set; }
        public float ShotPower { get; internal set; }
        /// <summary>A clear chance at least this good is taken at once instead of carrying the ball even closer.</summary>
        public float ShootChanceThreshold { get; internal set; }
        public float LateralValuePenalty { get; internal set; }
        /// <summary>Cap on the value of mere progress up the pitch; only a real shooting chance is worth more.</summary>
        public float ProgressWeight { get; internal set; }
        /// <summary>Discount on dribbling (the ball may still be lost before the next decision).</summary>
        public float DribbleRiskFactor { get; internal set; }
        public float DecisionNoise { get; internal set; }

        public float ArriveRadius { get; internal set; }
        public float RestartRadius { get; internal set; }
        public float SprintDistance { get; internal set; }
    }
}
