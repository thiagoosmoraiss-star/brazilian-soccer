namespace Game.Data.Match
{
    /// <summary>The high ball (Data/Balance/aerial.json, A9a, X-73): "Alto" — lançamento and cruzamento alto (GAME_DESIGN
    /// §19), the header and its aerial duel (§20), and the AI's use of high balls. Attribute-dependent values come from
    /// the Effect catalog (CrossAngleError, PassAngleError, PassPowerError, AerialDuel, HeaderAccuracy, HeaderPower,
    /// GkCrossClaimRange, GkCrossDecisionQuality).</summary>
    public sealed class AerialDefinition
    {
        // ---- high passes ----

        /// <summary>Launch elevation of a lançamento (deg); it lands at the target's feet.</summary>
        public float LongElevationDegrees { get; internal set; }
        /// <summary>Launch elevation of a cross (deg); it arrives at <see cref="CrossArrivalHeight"/> at the target.</summary>
        public float CrossElevationDegrees { get; internal set; }
        public float CrossArrivalHeight { get; internal set; }
        /// <summary>A held Alto sends the ball this far (m) between empty and full bar.</summary>
        public float MinDistance { get; internal set; }
        public float MaxDistance { get; internal set; }
        /// <summary>The Semi cone looks this far for a high-ball target (m).</summary>
        public float MaxTargetDistance { get; internal set; }
        /// <summary>A tapped Alto with nobody in the cone goes this far into space (m).</summary>
        public float SpaceDistance { get; internal set; }
        public float MaxLaunchSpeed { get; internal set; }
        /// <summary>A lançamento's direction error is PassAngleError × this (a cross uses CrossAngleError).</summary>
        public float LongErrorMultiplier { get; internal set; }
        /// <summary>PassPowerError applied to the launch speed, times this (the range grows with the speed squared).</summary>
        public float PowerErrorScale { get; internal set; }
        /// <summary>Cross zone: within this many metres of the goal line and at least <see cref="CrossZoneMinLateral"/> m
        /// from the centre line; an Alto there is a cross.</summary>
        public float CrossZoneDepth { get; internal set; }
        public float CrossZoneMinLateral { get; internal set; }
        /// <summary>A cross with nobody in the cone aims at the centre of the goal this far out (m).</summary>
        public float CrossDefaultDepth { get; internal set; }
        /// <summary>A long throw-in never goes farther than this (m).</summary>
        public float ThrowInMaxDistance { get; internal set; }

        // ---- headers ----

        /// <summary>Highest ball a jumping player meets with his head (m); lower than the capture height is feet.</summary>
        public float HeaderReachHeight { get; internal set; }
        /// <summary>Horizontal distance (m) within which a player contests a ball in the air.</summary>
        public float HeaderRadius { get; internal set; }
        /// <summary>In a duel, being closer to the ball adds up to this much to the AerialDuel weight (0-1).</summary>
        public float HeaderClosenessWeight { get; internal set; }
        /// <summary>An attacker this close to the goal (m) heads at it.</summary>
        public float HeaderShotRange { get; internal set; }
        public float HeaderShotTargetHeight { get; internal set; }
        public float HeaderVerticalErrorFraction { get; internal set; }
        /// <summary>A headed pass leaves at HeaderPower × this.</summary>
        public float HeaderPassSpeedFactor { get; internal set; }
        public float HeaderPassElevationDegrees { get; internal set; }
        public float ClearanceElevationDegrees { get; internal set; }
        /// <summary>A defender's clearance goes up to this far (deg) off straight away from his goal.</summary>
        public float ClearanceSpreadDegrees { get; internal set; }

        // ---- AI ----

        /// <summary>Weight of a cross in the AI's on-ball decision (× CrossQuality × chance in the box).</summary>
        public float AiCrossValue { get; internal set; }
        /// <summary>The AI only plays a lançamento to a teammate at least this far away (m).</summary>
        public float AiLongBallMinDistance { get; internal set; }
        public float AiLongBallValue { get; internal set; }
        /// <summary>Besides the receiver, this many of the nearest players of each side go for a high ball's drop zone.</summary>
        public int AiDropZonePlayers { get; internal set; }
        public float AiCornerCrossChance { get; internal set; }
        public float AiGoalKickLongChance { get; internal set; }
    }
}
