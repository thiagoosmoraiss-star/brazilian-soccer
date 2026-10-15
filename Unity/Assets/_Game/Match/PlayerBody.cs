using System.Numerics;

namespace Game.Match
{
    /// <summary>
    /// A player's kinematic state (TECHNICAL_SPEC §7: "PlayerBody | Cinemática e energia"). On the ground, so
    /// <see cref="Position"/>.Z is always 0 (GAME_DESIGN §25: "jogadores como círculos ~0,4 m"). Zero knowledge of
    /// Unity; <see cref="Movement"/> mutates it each fixed step.
    /// </summary>
    public sealed class PlayerBody
    {
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Last meaningful movement direction (unit vector, Z=0); kept when the player stops, so a
        /// stationary player still faces somewhere (protection, dribble offset).</summary>
        public Vector3 Facing = Vector3.UnitX;
        /// <summary>0-100.</summary>
        public float Energy = 100f;
        public bool Sprinting;
        /// <summary>Seconds left in a sharp-turn stun (TECHNICAL_SPEC: perda de velocidade + 0,2-0,4 s em curva > 90°).</summary>
        public float TurnStunRemaining;
        /// <summary>Set by a kick: the kicker cannot recapture the ball until it is outside his capture radius and moving
        /// away from him (otherwise the ball, still at his feet on the kick step, would be caught straight back).</summary>
        public bool IgnoreBallUntilClear;
        /// <summary>Seconds before this player may win the ball with a standing tackle again (set when he loses it).</summary>
        public float TackleCooldown;
        /// <summary>Sent off (A8a): parked outside the pitch, ignored by every system (passes, possession, AI, control).</summary>
        public bool OffPitch;

        // Carrinho (A9b): a slide along SlideDir, decelerating to a stop, then on the ground for a moment.
        /// <summary>Seconds of slide left (the body moves on its own).</summary>
        public float SlideRemaining;
        public float SlideSpeed;
        public float SlideDeceleration;
        public Vector3 SlideDirection;
        /// <summary>The slide reached the ball (a clean tackle: later contact is no foul; he gets up sooner).</summary>
        public bool SlideWonBall;
        /// <summary>Seconds left on the ground after a slide.</summary>
        public float GroundRemaining;

        /// <summary>Sliding or on the ground: no running, no ball, no tackle.</summary>
        public bool Down => SlideRemaining > 0f || GroundRemaining > 0f;
    }
}
