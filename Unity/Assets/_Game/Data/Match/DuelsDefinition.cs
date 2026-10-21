namespace Game.Data.Match
{
    /// <summary>Defence and attack duels (Data/Balance/duels.json, A9b, baseline v1): carrinho, the second defender
    /// ("Pressionar"), the basic body duel, cover, runs in behind (ruptura) and the keeper's 1×1 rush (GAME_DESIGN §21-§23,
    /// TECHNICAL_SPEC §7). Attribute-dependent values come from the Effect catalog: TackleReach (Desarme), BodyDuelWin
    /// (Força), RunTriggerThreshold (Visão), GkRushDecisionQuality (Posicionamento GK), GkDiveReach (Reflexo).</summary>
    public sealed class DuelsDefinition
    {
        // ---- carrinho (slide tackle) ----

        /// <summary>Ground the slide covers (m; GAME_DESIGN §22: "carrinho (2-3 m)").</summary>
        public float SlideDistance { get; internal set; }
        /// <summary>Launch speed of the slide: the player's speed, kept within these bounds (m/s).</summary>
        public float SlideMinSpeed { get; internal set; }
        public float SlideMaxSpeed { get; internal set; }
        /// <summary>The outstretched leg reaches TackleReach × this from the body.</summary>
        public float SlideReachFactor { get; internal set; }
        /// <summary>Only a ball up to this height is met by a slide (m).</summary>
        public float SlideBallMaxHeight { get; internal set; }
        /// <summary>A won slide pokes the ball on along the slide at this speed (m/s).</summary>
        public float SlidePokeSpeed { get; internal set; }
        /// <summary>A missed slide leaves the player on the ground this long (GAME_DESIGN §22: "erro = 1 s no chão").</summary>
        public float SlideGroundSeconds { get; internal set; }
        /// <summary>After a won slide he gets up in this long.</summary>
        public float SlideRecoverSeconds { get; internal set; }
        /// <summary>Body contact with an opponent closer than this (m), before the ball is touched, is a foul.</summary>
        public float SlideContactDistance { get; internal set; }
        /// <summary>The user's slide is aimed at the ball when it is within range and this close (deg) to the stick.</summary>
        public float SlideAssistAngleDegrees { get; internal set; }
        /// <summary>A foul by a slide from behind is booked with at least this chance ("por trás cartão provável").</summary>
        public float SlideBehindYellowChance { get; internal set; }
        /// <summary>The AI presser in range slides with this hazard (per second).</summary>
        public float AiSlideChancePerSecond { get; internal set; }
        /// <summary>The AI only slides from within this angle (deg) of the carrier's front (never from behind).</summary>
        public float AiSlideMaxApproachDegrees { get; internal set; }

        // ---- second defender ----

        /// <summary>"Pressionar": the nearest teammate within this distance (m) of the carrier goes to win the ball.</summary>
        public float SecondDefenderMaxDistance { get; internal set; }

        // ---- body duel ----

        /// <summary>Loose-ball contest: weight = BodyDuelWin × (1 + this × closeness).</summary>
        public float BodyClosenessWeight { get; internal set; }
        /// <summary>Overlapping opponents are pushed apart in proportion to the other's BodyDuelWin ^ this (0 = equally).</summary>
        public float BodyPushExponent { get; internal set; }

        // ---- cover ----

        /// <summary>The cover stands this far (m) goal-side of the ball, behind the presser.</summary>
        public float CoverDepth { get; internal set; }
        /// <summary>Only a defender within this distance (m) of the cover point takes the job.</summary>
        public float CoverMaxDistance { get; internal set; }

        // ---- runs in behind (ruptura) ----

        /// <summary>Space (m) between the opponents' last line and their goal area needed for a run.</summary>
        public float RunMinSpaceBehind { get; internal set; }
        /// <summary>Space score = space behind / this, compared to the carrier's RunTriggerThreshold (Visão).</summary>
        public float RunSpaceReference { get; internal set; }
        /// <summary>The runner aims this far (m) behind the line.</summary>
        public float RunDepthBehindLine { get; internal set; }
        public float RunMaxSeconds { get; internal set; }
        /// <summary>A run only starts, and the through ball is only played, this far (m) onside.</summary>
        public float RunOnsideMargin { get; internal set; }
        /// <summary>Only an attacker within this distance (m) of the line starts a run.</summary>
        public float RunMaxStartBehindLine { get; internal set; }
        /// <summary>A player does not start a new run this soon after the last one.</summary>
        public float RunCooldownSeconds { get; internal set; }
        /// <summary>Only a runner within this distance (m) of the carrier.</summary>
        public float RunMaxCarrierDistance { get; internal set; }
        /// <summary>Value of the through ball to a runner, × the run's threat (AI on-ball decision).</summary>
        public float RunThroughValue { get; internal set; }

        // ---- keeper rush (1×1) ----

        /// <summary>A carrier alone within this distance (m) of goal makes the keeper rush.</summary>
        public float RushRange { get; internal set; }
        /// <summary>The rushing keeper wins an exposed ball within GkDiveReach × this.</summary>
        public float RushSmotherReachFactor { get; internal set; }
        /// <summary>A wrong rush decision (GkRushDecisionQuality) costs this long before he goes.</summary>
        public float RushHesitationSeconds { get; internal set; }
        /// <summary>Through ball: he comes only if he gets there this many seconds before the nearest attacker.</summary>
        public float RushThroughBallMargin { get; internal set; }
        /// <summary>He aims this far (m) short of the ball, on its line to goal.</summary>
        public float RushStopDistance { get; internal set; }

        // Shot block (F1b, X-84 (a) "bolas desviadas"): a shot passing within reach of a defender's body is blocked with
        // his ShotBlockChance (Desarme) x chanceScale x closeness, once per defender per shot; the ball is deflected.
        public float BlockReach { get; internal set; }
        public float BlockMaxHeight { get; internal set; }
        public float BlockMinSpeed { get; internal set; }
        /// <summary>0 = no physical blocks (the A9 rule).</summary>
        public float BlockChanceScale { get; internal set; }
        public float BlockSpeedFactorMin { get; internal set; }
        public float BlockSpeedFactorMax { get; internal set; }
        public float BlockMaxDeflectDegrees { get; internal set; }
        public float BlockMaxUpSpeed { get; internal set; }

        // First touch (D-36, FirstTouchError = Drible 0,7 + Compostura 0,3, GAME_DESIGN §19 "Domínio usa Drible e Compostura"):
        // receiving a moving ball, the touch error (m) = FirstTouchError x scale x speed factor x PressureErrorMult (an opponent
        // within pressureRadius) x |triangular|; above threshold the ball escapes the receiver, loose.
        /// <summary>0 = every reception is clean (the behaviour before D-36).</summary>
        public float TouchScale { get; internal set; }
        public float TouchThreshold { get; internal set; }
        /// <summary>Slower balls are always controlled.</summary>
        public float TouchMinSpeed { get; internal set; }
        /// <summary>Ball speed at which the speed factor is 1; clamped to [speedFactorMin, speedFactorMax].</summary>
        public float TouchReferenceSpeed { get; internal set; }
        public float TouchSpeedFactorMin { get; internal set; }
        public float TouchSpeedFactorMax { get; internal set; }
        public float TouchPressureRadius { get; internal set; }
        /// <summary>An escaped ball rolls about (error x this) m away.</summary>
        public float TouchEscapeDistanceScale { get; internal set; }
        /// <summary>It goes on along the incoming line, turned by up to this.</summary>
        public float TouchMaxEscapeDegrees { get; internal set; }
    }
}
