namespace Game.Data.Match
{
    /// <summary>Defense and control-switch coefficients (Data/Balance/defense.json, A5, baseline v1, X-53). The
    /// attribute-dependent values come from the Effect catalog: TackleReach (Desarme), ShieldStrength (Força).</summary>
    public sealed class DefenseDefinition
    {
        /// <summary>How much closer than the carrier a defender must be to the ball at full ShieldStrength (m): the
        /// carrier's body protects the ball (GAME_DESIGN §21 "proteção ... Força").</summary>
        public float ShieldMeters { get; internal set; }
        /// <summary>A player who just lost the ball cannot win it straight back for this long (no tackle ping-pong).</summary>
        public float ReTackleCooldownSeconds { get; internal set; }
        /// <summary>The dispossessed carrier stumbles this long.</summary>
        public float DispossessedStunSeconds { get; internal set; }
        /// <summary>Right after winning the ball (tackle or first touch) the new carrier cannot be tackled for this long,
        /// so possession does not ping-pong between bodies in a crowd.</summary>
        public float PossessionSecureSeconds { get; internal set; }

        /// <summary>Auto-switch only to a candidate this much (fraction) better than the current player (GAME_DESIGN §17: ~25%).</summary>
        public float SwitchHysteresis { get; internal set; }
        /// <summary>No auto-switch this long after a manual one (GAME_DESIGN §17: 1 s).</summary>
        public float PostManualLockSeconds { get; internal set; }
        /// <summary>The controlled player counts as beaten once this far behind the carrier (GAME_DESIGN §17: 2 m).</summary>
        public float BeatenDistance { get; internal set; }
        /// <summary>Time-to-intercept penalty (fraction) for a candidate on the wrong side of the ball ("ponderado por posição").</summary>
        public float BehindBallPenalty { get; internal set; }
        /// <summary>Minimum time between two automatic switches.</summary>
        public float AutoSwitchCooldownSeconds { get; internal set; }
        /// <summary>"Trava de intenção": no auto-switch while the stick points this close (cosine) to the ball direction.</summary>
        public float IntentionAlignment { get; internal set; }
    }
}
