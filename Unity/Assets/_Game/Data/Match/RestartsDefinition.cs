namespace Game.Data.Match
{
    /// <summary>Match clock and simple restarts (Data/Balance/restarts.json, A7a, baseline v1, X-57).</summary>
    public sealed class RestartsDefinition
    {
        /// <summary>Number of halves; the real duration (MatchSetup.DurationMinutes) is split evenly between them.</summary>
        public int Halves { get; internal set; }
        /// <summary>Minutes the accelerated clock shows per half (GAME_DESIGN §14: "relógio acelerado a 90 min").</summary>
        public float DisplayMinutesPerHalf { get; internal set; }

        /// <summary>Dead ball → the taker is at the spot with the ball (he runs there meanwhile; whoever is still on the
        /// way when it ends is placed there).</summary>
        public float SetupSeconds { get; internal set; }
        /// <summary>An AI taker plays the ball this long after he is ready.</summary>
        public float AiTakeSeconds { get; internal set; }
        /// <summary>A user taker who does nothing for this long has the restart taken for him (a set piece never stalls).</summary>
        public float HumanTimeoutSeconds { get; internal set; }
        /// <summary>Opponents keep this far from the ball until it is played (kick-off, goal kick, corner) (m).</summary>
        public float ExclusionRadius { get; internal set; }
        /// <summary>Same for a throw-in (m).</summary>
        public float ThrowInExclusionRadius { get; internal set; }
        /// <summary>A throw-in is taken this far inside the touchline (m).</summary>
        public float LineInset { get; internal set; }
        /// <summary>Goal-kick spot: this far from the goal line…</summary>
        public float GoalKickDepth { get; internal set; }
        /// <summary>…and this far to the side the ball went out (m).</summary>
        public float GoalKickLateral { get; internal set; }
        /// <summary>Corner spot inset from both lines (m).</summary>
        public float CornerInset { get; internal set; }
    }
}
