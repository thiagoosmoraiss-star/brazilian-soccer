using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Match;

namespace Game.Match
{
    /// <summary>What happened on one <see cref="PracticeSession.Step"/> (the most relevant one).</summary>
    public enum PracticeEvent
    {
        None = 0,
        Pass = 1,
        Shot = 2,
        Captured = 3,
        PostHit = 4,
        Goal = 5,
        Out = 6,
    }

    /// <summary>
    /// A3 drill: a few teammates with no AI (ROADMAP A3: "2–3 jogadores trocando passes e chutando a gol vazio"),
    /// one of them controlled by the user. Runs the TECHNICAL_SPEC §5 step order (comandos → ações → movimento →
    /// posse → bola) so the Unity sandbox and the headless scenario tests drive exactly the same code. Not the
    /// MatchEngine (no opponents, clock, referee or restarts — those arrive in A4-A8). Zero allocation per step.
    /// </summary>
    public sealed class PracticeSession
    {
        public const string PassStream = "Pass";
        public const string ShotStream = "Shot";

        public Pitch Pitch { get; }
        public Ball Ball { get; } = new Ball();
        public PlayerBody[] Bodies { get; }
        public MatchPlayerSetup[] Players { get; }
        public ControlSelection Control { get; }
        public bool AttackingPositiveX { get; }

        public PassOutcome LastPass { get; private set; }
        public ShotOutcome LastShot { get; private set; }
        /// <summary>True when the last executed kick was a buffered "de primeira" one.</summary>
        public bool LastKickFirstTime { get; private set; }

        private readonly Balance _balance;
        private readonly BallParameters _ballCfg;
        private readonly MovementDefinition _mv;
        private readonly FatigueDefinition _fatigue;
        private readonly KickingDefinition _kicking;
        private readonly float _durationMinutes;
        private readonly Rng _passRng;
        private readonly Rng _shotRng;

        private ActionCommand _buffered = ActionCommand.None;
        private float _bufferedAge;
        private bool _bufferedWithoutBall;

        public PracticeSession(Balance balance, Pitch pitch, BallParameters ballCfg, MovementDefinition mv, FatigueDefinition fatigue,
            KickingDefinition kicking, MatchPlayerSetup[] players, Vector3[] startPositions, ulong seed,
            bool attackingPositiveX = true, float durationMinutes = 6f)
        {
            if (players == null || startPositions == null || players.Length == 0 || players.Length != startPositions.Length)
                throw new ArgumentException("One start position per player is required.");
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Pitch = pitch ?? throw new ArgumentNullException(nameof(pitch));
            _ballCfg = ballCfg ?? throw new ArgumentNullException(nameof(ballCfg));
            _mv = mv ?? throw new ArgumentNullException(nameof(mv));
            _fatigue = fatigue ?? throw new ArgumentNullException(nameof(fatigue));
            _kicking = kicking ?? throw new ArgumentNullException(nameof(kicking));
            _durationMinutes = durationMinutes;
            AttackingPositiveX = attackingPositiveX;

            Players = (MatchPlayerSetup[])players.Clone();
            Bodies = new PlayerBody[players.Length];
            var facing = attackingPositiveX ? Vector3.UnitX : -Vector3.UnitX;
            for (int i = 0; i < Bodies.Length; i++)
                Bodies[i] = new PlayerBody { Position = startPositions[i], Facing = facing, Energy = players[i].Energy };

            var streams = new RngStreams(seed);
            _passRng = streams.Get(PassStream);
            _shotRng = streams.Get(ShotStream);

            Control = new ControlSelection(0);
            PlaceBallAtFeet(0);
        }

        public Vector2 AttackDirection => AttackingPositiveX ? Vector2.UnitX : -Vector2.UnitX;

        /// <summary>Gives the ball to <paramref name="index"/> as a loose ball at his feet (dev reset / restart).</summary>
        public void PlaceBallAtFeet(int index)
        {
            var b = Bodies[index];
            Ball.Position = b.Position + b.Facing * (_mv.PlayerRadius + _ballCfg.Radius);
            Ball.Position = new Vector3(Ball.Position.X, Ball.Position.Y, 0f);
            Ball.Velocity = Vector3.Zero;
            Ball.Spin = 0f;
            Ball.State = BallState.Rolling;
            Ball.Owner = Ball.NoPlayer;
            for (int i = 0; i < Bodies.Length; i++) Bodies[i].IgnoreBallUntilClear = false;
            _buffered = ActionCommand.None;
            Control.OnCaptured(index);
        }

        /// <param name="move">Controlled player's stick (the others have no AI yet and stand still).</param>
        /// <param name="command">Action released this step, or <see cref="ActionCommand.None"/>.</param>
        public PracticeEvent Step(Vector2 move, bool sprint, ActionCommand command, Vector2 aim, float dt)
        {
            var ev = PracticeEvent.None;
            int controlled = Control.Controlled;

            // 1. Commands: hold the latest action in the input buffer (GAME_DESIGN §17: ~150 ms).
            if (command.Kind != ActionKind.None)
            {
                _buffered = command;
                _bufferedAge = 0f;
                _bufferedWithoutBall = Ball.Owner != controlled;
            }

            // 3. Actions.
            if (_buffered.Kind != ActionKind.None)
            {
                if (Ball.State == BallState.Controlled && Ball.Owner == controlled)
                {
                    ev = Execute(controlled, _buffered, aim, _bufferedWithoutBall);
                    _buffered = ActionCommand.None;
                }
                else
                {
                    _bufferedAge += dt;
                    if (_bufferedAge > _mv.InputBufferSeconds) _buffered = ActionCommand.None;
                }
            }
            controlled = Control.Controlled;

            // 4. Movement (+ fatigue).
            for (int i = 0; i < Bodies.Length; i++)
            {
                bool isControlled = i == controlled;
                var body = Bodies[i];
                bool hasBall = Ball.State == BallState.Controlled && Ball.Owner == i;
                bool wantsSprint = isControlled && sprint;
                Movement.Step(body, _balance, Players[i], isControlled ? move : Vector2.Zero, wantsSprint, hasBall,
                    DribbleSystem.IsLongTouch(body), _mv, _fatigue, dt);
                Fatigue.Drain(body, _balance, Players[i], body.Sprinting, _fatigue, _durationMinutes, dt);
            }

            // Possession and dribble.
            if (Ball.State != BallState.Controlled)
            {
                for (int i = 0; i < Bodies.Length; i++)
                {
                    if (Possession.Step(i, Bodies[i], Ball, _mv) != PossessionEvent.Captured) continue;
                    Control.OnCaptured(i);
                    if (ev == PracticeEvent.None) ev = PracticeEvent.Captured;
                    break;
                }
            }
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0)
                DribbleSystem.Step(Bodies[Ball.Owner], Ball, _balance, Players[Ball.Owner], _mv);

            // 5. Ball.
            if (Ball.State != BallState.Controlled)
            {
                var ballEvent = BallPhysics.Step(Ball, Pitch, _ballCfg, dt);
                if (ballEvent == BallEvent.Goal) ev = PracticeEvent.Goal;
                else if (ballEvent == BallEvent.Out) ev = PracticeEvent.Out;
                else if (ballEvent == BallEvent.PostHit && ev == PracticeEvent.None) ev = PracticeEvent.PostHit;
            }
            return ev;
        }

        private PracticeEvent Execute(int kicker, ActionCommand command, Vector2 aim, bool firstTime)
        {
            LastKickFirstTime = firstTime;
            var body = Bodies[kicker];
            var player = Players[kicker];
            if (command.Kind == ActionKind.Shot)
            {
                LastShot = ShotSystem.Execute(kicker, body, Ball, player, _balance, _kicking, _ballCfg, Pitch, AttackingPositiveX,
                    aim, command.Power, firstTime, NoOpponent, _shotRng);
                return PracticeEvent.Shot;
            }

            var kind = command.Kind == ActionKind.Through ? PassKind.Through : PassKind.Ground;
            LastPass = PassSystem.Execute(kicker, Bodies, Bodies.Length, Ball, player, _balance, _kicking, _ballCfg, kind, aim,
                command.Power, firstTime, NoOpponent, AttackDirection, _passRng);
            Control.OnPassReleased(kicker, LastPass.Target, LastPass.PredictedStop, Bodies, Bodies.Length);
            return PracticeEvent.Pass;
        }

        /// <summary>No opponents in A3; pressure enters with DefenseSystem/AI (A4-A5).</summary>
        private const float NoOpponent = float.PositiveInfinity;
    }
}
