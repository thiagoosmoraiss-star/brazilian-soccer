namespace Game.Data.Match
{
    /// <summary>Referee and full set pieces (Data/Balance/referee.json, A8a, X-64/X-65): fouls on contact before the
    /// ball, cards (the per-foul chances are the shared <c>match_rules.json</c> discipline), offside at the pass, free kicks
    /// with an automatic wall and penalties.</summary>
    public sealed class RefereeDefinition
    {
        // ---- fouls (GAME_DESIGN §22: "falta/cartão por atraso, ângulo, velocidade, oportunidade clara de gol e rigor do árbitro") ----

        /// <summary>The referee's strictness ("rigor do árbitro"): multiplies every foul chance.</summary>
        public float Strictness { get; internal set; }
        /// <summary>Calibrates the FoulChance effect (per challenge) to the played-match rate of X-65.</summary>
        public float FoulScale { get; internal set; }
        /// <summary>A defender this close to the carrier (centre to centre, m) is in body contact with him: if he is
        /// still closing in without having won the ball, it is a challenge.</summary>
        public float ContactDistance { get; internal set; }
        /// <summary>Only a defender closing on the carrier at least this fast (m/s) challenges; a jockeying one does not.</summary>
        public float MinClosingSpeed { get; internal set; }
        /// <summary>Closing speed above the minimum (m/s) that adds the whole <see cref="SpeedMultiplier"/>.</summary>
        public float SpeedReference { get; internal set; }
        public float SpeedMultiplier { get; internal set; }
        /// <summary>A challenge from more than this angle off the carrier's facing comes from behind.</summary>
        public float BehindAngleDegrees { get; internal set; }
        public float BehindMultiplier { get; internal set; }
        /// <summary>After a challenge (foul or not) the defender cannot challenge again for this long (s).</summary>
        public float ChallengeCooldownSeconds { get; internal set; }
        /// <summary>A side is never reduced below this many players by dismissals (Law 3: at least 7).</summary>
        public int MinPlayersOnPitch { get; internal set; }

        // ---- cards ----

        /// <summary>A foul by the last defender within this distance of his goal denies an obvious goal-scoring
        /// opportunity: straight red outside the area, penalty and yellow inside (m).</summary>
        public float DenialDistance { get; internal set; }

        // ---- offside ----

        /// <summary>How far ahead of the second-last defender (and the ball) an attacker may be and still be level (m).</summary>
        public float OffsideTolerance { get; internal set; }

        // ---- free kicks ----

        public float FreeKickSetupSeconds { get; internal set; }
        /// <summary>A direct free kick this close to the goal (m) is a shot for the AI; farther, a pass.</summary>
        public float DirectShotDistance { get; internal set; }
        /// <summary>…and only within this angle of the goal's centre line (degrees).</summary>
        public float DirectShotMaxAngleDegrees { get; internal set; }
        public float FreeKickShotPower { get; internal set; }
        /// <summary>The wall stands at the exclusion radius plus this margin (m), its players this far apart.</summary>
        public float WallDistanceMargin { get; internal set; }
        public float WallSpacing { get; internal set; }
        /// <summary>Wall size by distance: <see cref="WallMaxDistance"/>[i] (ascending) → <see cref="WallPlayers"/>[i].</summary>
        public float[] WallMaxDistance { get; internal set; }
        public int[] WallPlayers { get; internal set; }

        // ---- penalties ----

        public float PenaltySetupSeconds { get; internal set; }
        /// <summary>Penalty spot distance from the goal line (m).</summary>
        public float PenaltySpotDistance { get; internal set; }
        public float PenaltyShotPower { get; internal set; }
        /// <summary>The AI keeper stays in the middle with this chance; otherwise he picks a side (GAME_DESIGN §28).</summary>
        public float KeeperStayChance { get; internal set; }
        /// <summary>A diving keeper aims this far from the goal's centre (m).</summary>
        public float KeeperDiveLateral { get; internal set; }
        /// <summary>Players wait this far outside the penalty area (m).</summary>
        public float AreaClearance { get; internal set; }

        /// <summary>Wall size for a free kick <paramref name="distance"/> m from the goal (0 beyond the table).</summary>
        public int WallSize(float distance)
        {
            for (int i = 0; i < WallMaxDistance.Length; i++)
                if (distance <= WallMaxDistance[i]) return WallPlayers[i];
            return 0;
        }
    }
}
