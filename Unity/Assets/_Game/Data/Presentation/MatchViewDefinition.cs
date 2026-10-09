namespace Game.Data.Presentation
{
    /// <summary>Presentation tunables of the match scene (Data/Presentation/match_view.json, A7b, X-59): broadcast camera,
    /// radar, placeholder animation, HUD. Not balance — nothing here changes the simulation.</summary>
    public sealed class MatchViewDefinition
    {
        /// <summary>Camera elevation (GAME_DESIGN §16: lateral elevada ~30-45°).</summary>
        public float PitchDegrees { get; internal set; }
        /// <summary>Camera distance with the ball in midfield / near a goal or at a set piece (zoom dinâmico).</summary>
        public float FarDistance { get; internal set; }
        public float NearDistance { get; internal set; }
        /// <summary>Ball distance to the nearest goal (m) at or below which the camera is fully zoomed in…</summary>
        public float ZoomInGoalDistance { get; internal set; }
        /// <summary>…and at or above which it is fully zoomed out.</summary>
        public float ZoomOutGoalDistance { get; internal set; }
        /// <summary>Extra elevation with the ball on the far touchline ("ajuste de inclinação na lateral distante").</summary>
        public float FarSideExtraPitchDegrees { get; internal set; }
        /// <summary>Time constant of the follow damping (amortecimento) and of the zoom (s).</summary>
        public float FollowSeconds { get; internal set; }
        public float ZoomSeconds { get; internal set; }
        /// <summary>The camera looks this far ahead of the ball along its velocity (s), at most <see cref="MaxLookahead"/> m.</summary>
        public float LookaheadSeconds { get; internal set; }
        public float MaxLookahead { get; internal set; }
        public float FieldOfView { get; internal set; }

        /// <summary>Radar width as a fraction of the screen width; its height keeps the pitch's proportions.</summary>
        public float RadarWidthFraction { get; internal set; }
        public float RadarOpacity { get; internal set; }
        public float RadarDotSize { get; internal set; }

        /// <summary>Placeholder running animation: one leg cycle per this many meters run.</summary>
        public float StrideMeters { get; internal set; }
        public float LegSwingDegrees { get; internal set; }
        public float SprintLegSwingDegrees { get; internal set; }
        public float ArmSwingFraction { get; internal set; }
        /// <summary>Below this speed (m/s) the player stands.</summary>
        public float MoveSpeedThreshold { get; internal set; }
        public float KickSeconds { get; internal set; }
        public float KickSwingDegrees { get; internal set; }

        /// <summary>How long an event banner (goal, half-time, restart) stays up.</summary>
        public float BannerSeconds { get; internal set; }
    }
}
