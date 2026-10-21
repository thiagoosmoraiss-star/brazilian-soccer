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
        /// <summary>Per-level changes of the default block by mentality / line / pressure (A9c, TECHNICAL_SPEC §9).</summary>
        public TacticModifiers Modifiers { get; internal set; }
    }

    /// <summary>How mentality (1-5), defensive line (1-3) and pressure (1-3) change the default block (tactics.json
    /// "modifiers", A9c, X-78; TECHNICAL_SPEC §9). Arrays are indexed by level - 1; the default tactic (3, 2, 2) is
    /// neutral (offset 0, scale 1).</summary>
    public sealed class TacticModifiers
    {
        public const int MentalityLevels = 5;
        public const int LineLevels = 3;
        public const int PressureLevels = 3;

        /// <summary>Mentality: the whole block higher (+) or lower (-), meters ("altura do bloco").</summary>
        public float[] MentalityLineOffset { get; internal set; }
        /// <summary>Mentality: depth of the in-possession phases ("jogadores além da linha da bola").</summary>
        public float[] MentalityAttackDepthScale { get; internal set; }
        /// <summary>Mentality: depth of the out-of-possession phases (a low block is a compact one).</summary>
        public float[] MentalityDefendDepthScale { get; internal set; }
        /// <summary>Mentality: weight of progress up the pitch in the on-ball decision ("risco").</summary>
        public float[] MentalityProgressWeightScale { get; internal set; }
        /// <summary>Mentality: the clear-chance threshold to shoot.</summary>
        public float[] MentalityShootThresholdScale { get; internal set; }
        /// <summary>Mentality: time between runs in behind (A9b ruptura).</summary>
        public float[] MentalityRunCooldownScale { get; internal set; }
        /// <summary>Mentality (D-37): added to a lançamento's value when the carrier is pressed in his own third ("saída longa
        /// sob pressão"); 0 = no preference.</summary>
        public float[] MentalityPressedOutletValue { get; internal set; }
        /// <summary>Mentality (D-37): box attackers added to ai.json's ("jogadores comprometidos à frente").</summary>
        public int[] MentalityBoxAttackersOffset { get; internal set; }

        /// <summary>Line: added to how far behind the ball the line sits when defending (+ = deeper).</summary>
        public float[] LineBehindBallOffset { get; internal set; }
        /// <summary>Line: added to the highest the line may push ("subida para impedimento").</summary>
        public float[] LineMaxOffset { get; internal set; }
        /// <summary>Line: added to the deepest the line may drop.</summary>
        public float[] LineMinOffset { get; internal set; }

        /// <summary>Pressure: pressers added to the default ("nº de pressionadores").</summary>
        public int[] PressersOffset { get; internal set; }
        /// <summary>Pressure: the distance at which a presser engages ("distância de disparo").</summary>
        public float[] PressTriggerScale { get; internal set; }
        /// <summary>Pressure: duration of the defensive transition (counter-press).</summary>
        public float[] TransitionDefenseScale { get; internal set; }

        /// <summary>A tactic changed during the match is eased in over this long ("suavização de 1-2 s").</summary>
        public float ChangeSmoothSeconds { get; internal set; }
        /// <summary>Formation change: cost of putting a player in a slot of another role (vs normalized distance).</summary>
        public float FormationRolePenalty { get; internal set; }
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
        /// <summary>Atacar a área (F1b, X-84 (a)): with the ball past <see cref="BoxAttackFraction"/> of the pitch, this many
        /// of the side's most advanced players (by formation slot) go into the box instead of supporting the carrier.</summary>
        public int BoxAttackers { get; internal set; }
        public float BoxAttackFraction { get; internal set; }
        /// <summary>They stand this far from the goal line (never offside)...</summary>
        public float BoxAttackDepth { get; internal set; }
        /// <summary>...and at most this far from the middle of the pitch, on the side of their slot.</summary>
        public float BoxAttackHalfWidth { get; internal set; }

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
        /// <summary>Value of still having the ball (F1b, X-84 (a)), added to the situation value of every option that keeps
        /// it (pass, dribble, through ball, lançamento): losing the ball costs it, so a safe pass beats a run into the presser.</summary>
        public float PossessionValue { get; internal set; }

        public float ArriveRadius { get; internal set; }
        public float RestartRadius { get; internal set; }
        public float SprintDistance { get; internal set; }
    }
}
