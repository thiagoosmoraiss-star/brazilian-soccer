namespace Game.Data.Match
{
    /// <summary>Goalkeeper coefficients (Data/Balance/goalkeeper.json, A6, baseline v1, X-54). The attribute-dependent
    /// values come from the Effect catalog: GkReactionTime and GkDiveReach (Reflexo), GkAngleError (Posicionamento GK),
    /// GkCatchChance (Mãos).</summary>
    public sealed class GoalkeeperDefinition
    {
        // ---- positioning (GAME_DESIGN §23: "bissetriz do ângulo; avança/recua com a bola; líbero conforme a linha") ----
        /// <summary>Closest the keeper ever stands to his goal line (m): never walks into the goal.</summary>
        public float MinDepth { get; internal set; }
        /// <summary>Depth off the line (m) with the ball far away, before the sweeper adjustment.</summary>
        public float MaxDepth { get; internal set; }
        /// <summary>Ball distance to the goal (m) at or below which the keeper stays at <see cref="MinDepth"/>.</summary>
        public float NearBallDistance { get; internal set; }
        /// <summary>Ball distance (m) at or above which the keeper is at full depth.</summary>
        public float FarBallDistance { get; internal set; }
        /// <summary>Líbero: full depth may grow to this fraction of the own defensive line's distance from goal…</summary>
        public float SweeperLineFraction { get; internal set; }
        /// <summary>…up to this many meters.</summary>
        public float SweeperMaxDepth { get; internal set; }
        /// <summary>The keeper's target stays within the posts widened by this many meters each side.</summary>
        public float LateralLimit { get; internal set; }
        /// <summary>How often the positioning angle error (GkAngleError) is re-sampled.</summary>
        public float ErrorResampleSeconds { get; internal set; }
        public float ArriveRadius { get; internal set; }
        /// <summary>The keeper sprints to his spot when further than this (m).</summary>
        public float SprintDistance { get; internal set; }

        // ---- reaction (GAME_DESIGN §23: "reação 0,15-0,30 s (Reflexo) + penalidades") ----
        /// <summary>How far ahead a loose ball's path is predicted to decide whether it threatens the goal.</summary>
        public float PredictionHorizonSeconds { get; internal set; }
        /// <summary>A ball predicted to cross the goal line this close outside the posts still makes the keeper react.</summary>
        public float ThreatMargin { get; internal set; }
        /// <summary>A keeper moving faster than this (m/s) when the shot leaves is not set…</summary>
        public float UnsetSpeed { get; internal set; }
        /// <summary>…and reacts this much later.</summary>
        public float UnsetPenaltySeconds { get; internal set; }

        // ---- save (GAME_DESIGN §23: "alcance limitado, sem teletransporte; encaixa/espalma/rebate (Mãos, força e efeito)") ----
        /// <summary>Hands' horizontal reach from the body (m). GkDiveReach is the total: the dive moves the body the rest.</summary>
        public float HandReach { get; internal set; }
        /// <summary>Highest ball the keeper can touch (m).</summary>
        public float ReachHeight { get; internal set; }
        /// <summary>Body speed during a dive (m/s): a dive covers distance, it does not teleport.</summary>
        public float DiveSpeed { get; internal set; }
        /// <summary>Time on the ground after a dive (GAME_DESIGN §23: "levanta em 0,6-1,0 s"), shorter with better Reflexo.</summary>
        public float GetUpMinSeconds { get; internal set; }
        public float GetUpMaxSeconds { get; internal set; }
        /// <summary>Get-up time = this × GkReactionTime, clamped to [min, max] (0.15 s → 0.6 s, 0.25 s → 1.0 s).</summary>
        public float GetUpPerReactionSecond { get; internal set; }
        /// <summary>Catch chance multiplier = 1 + (reference − ball speed) × slope, at least <see cref="CatchSpeedMultMin"/>.</summary>
        public float CatchReferenceSpeed { get; internal set; }
        public float CatchSpeedSlope { get; internal set; }
        public float CatchSpeedMultMin { get; internal set; }
        /// <summary>Catch chance multiplier when the save needed a dive.</summary>
        public float DiveCatchFactor { get; internal set; }
        /// <summary>Above this height (m) a catch is harder…</summary>
        public float HighBallHeight { get; internal set; }
        /// <summary>…by this multiplier.</summary>
        public float HighCatchFactor { get; internal set; }
        /// <summary>Catch chance lost per unit of ball spin ("efeito").</summary>
        public float SpinCatchPenalty { get; internal set; }
        public float MaxCatchChance { get; internal set; }
        /// <summary>A parried ball keeps this fraction of its speed…</summary>
        public float ParryRestitution { get; internal set; }
        /// <summary>…deflected away from the goal at an angle (to the side the ball was going) in this range from the
        /// outward normal of the goal line.</summary>
        public float ParryMinAngleDegrees { get; internal set; }
        public float ParryMaxAngleDegrees { get; internal set; }
        /// <summary>Upward speed given to a parried ball (m/s).</summary>
        public float ParryLift { get; internal set; }
        /// <summary>A caught ball is held this long before the keeper distributes it (placeholder until restarts, A7).</summary>
        public float HoldSeconds { get; internal set; }
    }
}
