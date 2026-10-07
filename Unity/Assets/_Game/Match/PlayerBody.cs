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
    }
}
