using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;

namespace Game.Match.AI
{
    public enum AiMatchEvent
    {
        None = 0,
        Captured = 1,
        Pass = 2,
        Shot = 3,
        Goal = 4,
        Out = 5,
        Finished = 6,
    }

    /// <summary>Optional hook for tests/tools (no allocation: plain value arguments).</summary>
    public interface IAiMatchObserver
    {
        void OnIntentionChanged(int player, AiIntention from, AiIntention to, float time);
        void OnPossessionChanged(MatchSide side, float time);
    }

    /// <summary>
    /// A4 headless 11×11 (ROADMAP A4: "modo headless"): both teams driven by the three AI layers, one MatchSetup in,
    /// fixed steps, the TECHNICAL_SPEC §5 order (IA → ações → movimento → bola). Uses the same Movement / Possession /
    /// PassSystem / ShotSystem as the controlled player. Not yet the MatchEngine: no clock phases, referee, goalkeeper
    /// AI, tackles or real restarts — after a goal the teams line up again and the conceding side kicks off, after the
    /// ball goes out it is handed to the nearest player of the other side (placeholders until A5-A8). Zero allocation
    /// per step.
    /// </summary>
    public sealed class AiMatch
    {
        public const string HomeStream = "AI.Home";
        public const string AwayStream = "AI.Away";
        public const string PassStream = "Pass";
        public const string ShotStream = "Shot";

        public Pitch Pitch { get; }
        public Ball Ball { get; } = new Ball();
        public AiTeam Home { get; }
        public AiTeam Away { get; }
        /// <summary>All 22 players, home 0-10 then away 11-21 (index = <see cref="AiPlayer.Global"/>).</summary>
        public AiPlayer[] Players { get; } = new AiPlayer[2 * MatchTeamSetup.StarterCount];
        public float ElapsedSeconds => _step * StepSeconds;
        public float DurationSeconds { get; }
        /// <summary>Counted in whole steps (summing a float step drifts).</summary>
        public bool Finished => _step >= _totalSteps;
        public float StepSeconds { get; }

        private readonly Balance _balance;
        private readonly BallParameters _ballCfg;
        private readonly MovementDefinition _mv;
        private readonly FatigueDefinition _fatigue;
        private readonly KickingDefinition _kicking;
        private readonly AiDefinition _ai;
        private readonly float _durationMinutes;
        private readonly Rng _passRng;
        private readonly Rng _shotRng;
        private readonly IAiMatchObserver _observer;
        private readonly int _teamEvery, _roleEvery, _individualEvery;
        private long _step;
        private readonly long _totalSteps;
        private AiTeam _possession;

        public AiMatch(GameDatabase db, MatchSetup setup, float stepSeconds, IAiMatchObserver observer = null)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (stepSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            _balance = db.Balance;
            _ballCfg = db.Ball.Ball;
            _mv = db.Movement;
            _fatigue = db.Fatigue;
            _kicking = db.Kicking;
            _ai = db.Ai;
            _observer = observer;
            StepSeconds = stepSeconds;
            _durationMinutes = setup.DurationMinutes;
            DurationSeconds = setup.DurationMinutes * 60f;
            _totalSteps = (long)MathF.Round(DurationSeconds / stepSeconds);
            Pitch = Pitch.From(db.Ball.Pitch);

            var streams = new RngStreams(setup.Seed);
            _passRng = streams.Get(PassStream);
            _shotRng = streams.Get(ShotStream);
            Home = new AiTeam(MatchSide.Home, new TeamFrame(true, Pitch.HalfLength), Formation(db, setup.Home), db.Tactics, setup.Home, 0, streams.Get(HomeStream));
            Away = new AiTeam(MatchSide.Away, new TeamFrame(false, Pitch.HalfLength), Formation(db, setup.Away), db.Tactics, setup.Away,
                MatchTeamSetup.StarterCount, streams.Get(AwayStream));
            for (int i = 0; i < MatchTeamSetup.StarterCount; i++)
            {
                Players[i] = Home.Players[i];
                Players[MatchTeamSetup.StarterCount + i] = Away.Players[i];
            }

            _teamEvery = Every(_ai.TeamHz, stepSeconds);
            _roleEvery = Every(_ai.RoleHz, stepSeconds);
            _individualEvery = Every(_ai.IndividualHz, stepSeconds);
            Kickoff(Home);
        }

        private static FormationDefinition Formation(GameDatabase db, MatchTeamSetup team) =>
            db.Formation(team.Tactic.FormationId) ?? throw new ArgumentException("Unknown formation " + team.Tactic.FormationId);

        private static int Every(float hz, float step) => Math.Max(1, (int)MathF.Round(1f / (hz * step)));

        public AiTeam TeamOf(AiPlayer p) => p.Team;
        public AiTeam Opponents(AiTeam team) => team == Home ? Away : Home;
        public AiTeam PossessionTeam => _possession;

        public AiMatchEvent Step()
        {
            if (Finished) return AiMatchEvent.Finished;
            float dt = StepSeconds;
            var ev = AiMatchEvent.None;

            UpdatePossessionTeam();
            Home.TransitionTimer = MathF.Max(0f, Home.TransitionTimer - dt);
            Away.TransitionTimer = MathF.Max(0f, Away.TransitionTimer - dt);

            // 2. AI: team (5 Hz), function (10 Hz), individual (10 Hz, staggered by player).
            if (_step % _teamEvery == 0) { TeamTick(Home); TeamTick(Away); }
            if (_step % _roleEvery == 0) { RoleTick(Home); RoleTick(Away); }
            float individualTick = _individualEvery * dt;
            for (int i = 0; i < Players.Length; i++)
                if ((_step + i) % _individualEvery == 0) IndividualTick(Players[i], individualTick);

            // 3. Actions (the carrier's decision).
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0)
            {
                var kick = OnBall(Players[Ball.Owner], dt);
                if (kick != AiMatchEvent.None) ev = kick;
            }

            // 4. Movement (+ fatigue).
            for (int i = 0; i < Players.Length; i++) Move(Players[i], dt);

            // Possession and dribble.
            if (Ball.State != BallState.Controlled)
            {
                for (int k = 0; k < Players.Length; k++)
                {
                    int i = (int)((_step + k) % Players.Length); // rotating start: no side wins ties by index
                    var p = Players[i];
                    if (Possession.Step(i, p.Body, Ball, _mv) != PossessionEvent.Captured) continue;
                    SetIntention(p, AiIntention.OnBall);
                    p.DecisionTimer = _ai.DecisionIntervalSeconds;
                    p.DribbleTimer = 0f;
                    p.DribbleTarget = p.Body.Position;
                    if (ev == AiMatchEvent.None) ev = AiMatchEvent.Captured;
                    break;
                }
            }
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0)
            {
                var owner = Players[Ball.Owner];
                DribbleSystem.Step(owner.Body, Ball, _balance, owner.Setup, _mv);
            }

            // 5. Ball.
            if (Ball.State != BallState.Controlled)
            {
                var ballEvent = BallPhysics.Step(Ball, Pitch, _ballCfg, dt);
                if (ballEvent == BallEvent.Goal) { OnGoal(); ev = AiMatchEvent.Goal; }
                else if (ballEvent == BallEvent.Out) { OnOut(); ev = AiMatchEvent.Out; }
            }

            _step++;
            return Finished && ev == AiMatchEvent.None ? AiMatchEvent.Finished : ev;
        }

        /// <summary>Runs the whole match headless.</summary>
        public void RunToEnd()
        {
            while (!Finished) Step();
        }

        private void UpdatePossessionTeam()
        {
            AiTeam now = _possession;
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0) now = Players[Ball.Owner].Team;
            else if (Ball.LastTouch >= 0) now = Players[Ball.LastTouch].Team;
            if (now == null || now == _possession) return;
            SetPossession(now);
            TeamBrain.OnPossessionChanged(now, true, _ai);
            TeamBrain.OnPossessionChanged(Opponents(now), false, _ai);
        }

        private void SetPossession(AiTeam team)
        {
            _possession = team;
            team.HasPossession = true;
            Opponents(team).HasPossession = false;
            _observer?.OnPossessionChanged(team.Side, ElapsedSeconds);
        }


        private void TeamTick(AiTeam team)
        {
            var opp = Opponents(team);
            float ballU = team.Frame.U(Ball.Position);
            TeamBrain.UpdatePhase(team, team == _possession, ballU, Pitch.Length, _ai);
            team.OffsideU = RoleLayer.OffsideU(team, opp, ballU, Pitch.Length);
            IndividualLayer.Assign(team, opp, Ball, _balance, _ai);
        }

        private void RoleTick(AiTeam team)
        {
            float ballU = team.Frame.U(Ball.Position);
            float ballV = team.Frame.V(Ball.Position);
            float tick = _roleEvery * StepSeconds;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                var ideal = RoleLayer.IdealTarget(p, ballU, ballV, team.OffsideU, Pitch.Length, Pitch.HalfWidth, _ai);
                RoleLayer.Commit(p, ideal, _balance, _ai, tick);
            }
        }

        private void IndividualTick(AiPlayer p, float tick)
        {
            var opp = Opponents(p.Team);
            p.IntentionTime += tick;
            var desired = IndividualLayer.Desired(p, opp, Ball, _ai);
            if (desired != p.Intention)
            {
                // Urgent intentions (carry, press, chase a loose ball) start at once; the positional ones wait for the
                // current intention's minimum commitment, so a player never flickers between holding, supporting, marking.
                bool urgent = desired == AiIntention.OnBall || desired == AiIntention.Press || desired == AiIntention.ChaseBall;
                bool keep = !urgent && IndividualLayer.IsValid(p, p.Intention, Ball) && p.IntentionTime < _ai.MinCommitSeconds;
                if (!keep)
                {
                    if (desired == AiIntention.ChaseBall)
                    {
                        if (p.ChaseReactionTimer < 0f) p.ChaseReactionTimer = _balance.Eval(Effect.LooseBallReaction, p.Setup.Attributes);
                        p.ChaseReactionTimer -= tick;
                        if (p.ChaseReactionTimer <= 0f) SetIntention(p, desired);
                    }
                    else SetIntention(p, desired);
                }
            }
            else p.ChaseReactionTimer = -1f;

            if (p.Intention != AiIntention.OnBall) p.IntentTarget = IndividualLayer.Target(p, opp, Ball, Pitch, _ai, tick);
        }

        private void SetIntention(AiPlayer p, AiIntention to)
        {
            if (p.Intention == to) return;
            var from = p.Intention;
            p.Intention = to;
            p.IntentionTime = 0f;
            p.ChaseReactionTimer = -1f;
            p.SupportTimer = 0f;
            _observer?.OnIntentionChanged(p.Global, from, to, ElapsedSeconds);
        }

        private AiMatchEvent OnBall(AiPlayer p, float dt)
        {
            if (p.Intention != AiIntention.OnBall) SetIntention(p, AiIntention.OnBall);
            p.DecisionTimer -= dt;
            p.DribbleTimer -= dt;
            if (p.DecisionTimer > 0f || p.DribbleTimer > 0f) return AiMatchEvent.None;

            var team = p.Team;
            var opp = Opponents(team);
            var decision = OnBallDecision.Decide(p, opp, Pitch, _balance, _ai);
            p.DecisionTimer = _ai.DecisionIntervalSeconds;
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, opp);
            switch (decision.Choice)
            {
                case OnBallChoice.Pass:
                {
                    var to = team.Players[decision.Receiver].Body.Position;
                    var aim = new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y);
                    PassSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _ballCfg,
                        PassKind.Ground, aim, ActionCommand.AutoPower, false, pressure, team.Frame.Forward, _passRng);
                    Ball.LastTouch = p.Global;
                    SetIntention(p, AiIntention.HoldShape);
                    return AiMatchEvent.Pass;
                }
                case OnBallChoice.Shot:
                    ShotSystem.Execute(p.Local, p.Body, Ball, p.Setup, _balance, _kicking, _ballCfg, Pitch, team.Frame.AttackingPositiveX,
                        Vector2.Zero, _ai.ShotPower, false, pressure, _shotRng);
                    Ball.LastTouch = p.Global;
                    SetIntention(p, AiIntention.HoldShape);
                    return AiMatchEvent.Shot;
                default:
                    p.DribbleTarget = decision.DribbleTarget;
                    p.DribbleTimer = _ai.MinDribbleSeconds;
                    return AiMatchEvent.None;
            }
        }

        private void Move(AiPlayer p, float dt)
        {
            var body = p.Body;
            bool hasBall = Ball.State == BallState.Controlled && Ball.Owner == p.Global;
            var target = hasBall ? p.DribbleTarget : p.IntentTarget;
            float dx = target.X - body.Position.X, dy = target.Y - body.Position.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (p.Moving && d < _ai.ArriveRadius) p.Moving = false;
            else if (!p.Moving && d > _ai.RestartRadius) p.Moving = true;
            if (hasBall) p.Moving = d > _ai.ArriveRadius;

            var move = p.Moving && d > 1e-4f ? new Vector2(dx / d, dy / d) : Vector2.Zero;
            bool urgent = p.Intention == AiIntention.Press || p.Intention == AiIntention.ChaseBall;
            bool sprint = p.Moving && (d > _ai.SprintDistance || (urgent && d > _ai.ContainDistance));
            Movement.Step(body, _balance, p.Setup, move, sprint, hasBall, DribbleSystem.IsLongTouch(body), _mv, _fatigue, dt);
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt);

            if (!p.Moving && !hasBall)
            {
                float bx = Ball.Position.X - body.Position.X, by = Ball.Position.Y - body.Position.Y;
                float bl = MathF.Sqrt(bx * bx + by * by);
                if (bl > 1e-3f) body.Facing = new Vector3(bx / bl, by / bl, 0f); // idle players watch the ball
            }
        }

        private void OnGoal()
        {
            var scorer = Ball.Position.X > 0f ? Home : Away; // the ball crossed the away (+x) line → home scored
            scorer.Goals++;
            Kickoff(Opponents(scorer));
        }

        /// <summary>Placeholder until restarts (A7): the side that did not touch it last gets the ball at the nearest
        /// player's feet, at the point it went out (pulled back inside).</summary>
        private void OnOut()
        {
            var restart = Ball.LastTouch >= 0 ? Opponents(Players[Ball.LastTouch].Team) : Home;
            var spot = new Vector3(MathUtil.Clamp(Ball.Position.X, -Pitch.HalfLength + 1f, Pitch.HalfLength - 1f),
                MathUtil.Clamp(Ball.Position.Y, -Pitch.HalfWidth + 1f, Pitch.HalfWidth - 1f), 0f);
            AiPlayer nearest = restart.Players[0];
            float best = float.PositiveInfinity;
            for (int i = 0; i < restart.Players.Length; i++)
            {
                float d = Vector3.DistanceSquared(restart.Players[i].Body.Position, spot);
                if (d < best) { best = d; nearest = restart.Players[i]; }
            }
            GiveBall(nearest);
        }

        /// <summary>Both sides back to their kick-off shape; <paramref name="kicking"/> starts with the ball at the
        /// center (placeholder until restarts in A7).</summary>
        public void Kickoff(AiTeam kicking)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                var pos = RoleLayer.KickoffPosition(p, Pitch.Length, Pitch.HalfWidth, _ai);
                p.Body.Position = pos;
                p.Body.Velocity = Vector3.Zero;
                p.Body.Facing = new Vector3(p.Team.Frame.Forward.X, p.Team.Frame.Forward.Y, 0f);
                p.Body.IgnoreBallUntilClear = false;
                p.Body.TurnStunRemaining = 0f;
                p.RoleTarget = p.IdealTarget = p.PendingTarget = p.IntentTarget = pos;
                p.CorrectionTimer = 0f;
                p.Moving = false;
                SetIntention(p, AiIntention.HoldShape);
            }

            AiPlayer taker = kicking.Players[0];
            for (int i = 0; i < kicking.Players.Length; i++)
                if (kicking.Players[i].Slot.X > taker.Slot.X) taker = kicking.Players[i];
            var fwd = kicking.Frame.Forward;
            taker.Body.Position = new Vector3(-fwd.X * (_mv.PlayerRadius + _ballCfg.Radius), 0f, 0f);
            GiveBall(taker);
            kicking.Phase = TeamPhase.Build;
            Opponents(kicking).Phase = TeamPhase.Defend;
            kicking.TransitionTimer = Opponents(kicking).TransitionTimer = 0f;
            SetPossession(kicking);
        }

        private void GiveBall(AiPlayer p)
        {
            var facing = p.Body.Facing;
            Ball.Position = new Vector3(p.Body.Position.X + facing.X * (_mv.PlayerRadius + _ballCfg.Radius),
                p.Body.Position.Y + facing.Y * (_mv.PlayerRadius + _ballCfg.Radius), 0f);
            Ball.Velocity = Vector3.Zero;
            Ball.Spin = 0f;
            Ball.State = BallState.Rolling;
            Ball.Owner = Ball.NoPlayer;
            Ball.LastTouch = p.Global;
            for (int i = 0; i < Players.Length; i++) Players[i].Body.IgnoreBallUntilClear = false;
        }
    }
}
