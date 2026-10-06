using System.Numerics;

namespace Game.Match
{
    /// <summary>TECHNICAL_SPEC §6: "estado (Controlled, Rolling, Airborne, Dead)".</summary>
    public enum BallState
    {
        /// <summary>Attached to a player's foot; A1 does not simulate this (no players/possession yet).</summary>
        Controlled = 0,
        Rolling = 1,
        Airborne = 2,
        /// <summary>Fixed until the next restart (TECHNICAL_SPEC §6: "Bola parada: Dead fixa até o reinício").</summary>
        Dead = 3,
    }

    /// <summary>What happened during a <see cref="BallPhysics.Step"/> (at most one per call; ties broken by the
    /// earliest point along the frame's movement).</summary>
    public enum BallEvent
    {
        None = 0,
        /// <summary>Hit a post or the crossbar; reflected, still in play.</summary>
        PostHit = 1,
        /// <summary>Crossed the goal line between the posts, at or below the crossbar; now <c>Dead</c>.</summary>
        Goal = 2,
        /// <summary>Crossed the touchline, or the goal line outside the goal mouth; now <c>Dead</c>.</summary>
        Out = 3,
    }

    /// <summary>
    /// The ball (TECHNICAL_SPEC §6): position, velocity, spin and state. A plain mutable holder — all behavior
    /// lives in <see cref="BallPhysics"/> so prediction and real integration share the exact same code
    /// (GAME_DESIGN §25: "mesmas fórmulas integram e preveem").
    /// </summary>
    public sealed class Ball
    {
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Spin magnitude driving the lateral curve while <c>Airborne</c> (colocado, cruzamento, falta); decays over time.</summary>
        public float Spin;
        public BallState State = BallState.Dead;

        /// <summary>Sets the ball in motion (A1 has no players/ShotSystem yet; later stages kick through those).</summary>
        public void Kick(Vector3 velocity, float spin)
        {
            Velocity = velocity;
            Spin = spin;
            State = velocity.Z > 0f || Position.Z > 0f ? BallState.Airborne : BallState.Rolling;
        }

        public Ball Clone() => new Ball { Position = Position, Velocity = Velocity, Spin = Spin, State = State };

        public void CopyFrom(Ball other)
        {
            Position = other.Position;
            Velocity = other.Velocity;
            Spin = other.Spin;
            State = other.State;
        }
    }
}
