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
        Tackle = 7,
        /// <summary>A goalkeeper caught or parried the ball (A6).</summary>
        Save = 8,
        /// <summary>The first half (of several) ended; the other side kicks off the next one (A7a).</summary>
        HalfTime = 9,
    }

    /// <summary>One step of the user's input for the controlled player (A5). Attack: move, sprint and released actions
    /// (pass / through ball / shot, as in A3). Defense: hold "Contenção", tap "Trocar".</summary>
    public readonly struct HumanInput
    {
        public static readonly HumanInput None = new HumanInput(System.Numerics.Vector2.Zero, false, ActionCommand.None, false, false);

        public readonly System.Numerics.Vector2 Move;
        public readonly bool Sprint;
        public readonly ActionCommand Action;
        public readonly bool ContainHeld;
        public readonly bool SwitchPressed;

        public HumanInput(System.Numerics.Vector2 move, bool sprint, ActionCommand action, bool containHeld, bool switchPressed)
        {
            Move = move;
            Sprint = sprint;
            Action = action;
            ContainHeld = containHeld;
            SwitchPressed = switchPressed;
        }
    }

    /// <summary>Optional hook for tests/tools (no allocation: plain value arguments).</summary>
    public interface IAiMatchObserver
    {
        void OnIntentionChanged(int player, AiIntention from, AiIntention to, float time);
        void OnPossessionChanged(MatchSide side, float time);
    }

    /// <summary>
    /// The vertical slice's match (A4-A7a): both teams driven by the three AI layers, optionally one side's selected
    /// player by the user; one MatchSetup in, one MatchResult out (<see cref="Result"/>); fixed steps in the TECHNICAL_SPEC
    /// §5 order (IA → ações → movimento → bola → relógio). Uses the same Movement / Possession / PassSystem / ShotSystem as
    /// the controlled player; standing tackles (A5), goalkeepers (A6, <see cref="Goalkeeper"/>), simple restarts — kick-off,
    /// throw-in, short goal kick, short corner — and a two-half clock (A7a). No referee (fouls, cards, offside, penalties:
    /// A8). The whole state can be saved and restored (<see cref="Snapshot"/>). Zero allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string HomeStream = "AI.Home";
        public const string AwayStream = "AI.Away";
        public const string PassStream = "Pass";
        public const string ShotStream = "Shot";
        public const string KeeperStream = "Goalkeeper";

        public Pitch Pitch { get; }
        public Ball Ball { get; } = new Ball();
        public AiTeam Home { get; }
        public AiTeam Away { get; }
        /// <summary>All 22 players, home 0-10 then away 11-21 (index = <see cref="AiPlayer.Global"/>).</summary>
        public AiPlayer[] Players { get; } = new AiPlayer[2 * MatchTeamSetup.StarterCount];
        /// <summary>Each side's goalkeeper (A6).</summary>
        public Keeper HomeKeeper { get; }
        public Keeper AwayKeeper { get; }
        public float ElapsedSeconds => _step * StepSeconds;
        /// <summary>Current half, 1-based.</summary>
        public int Half => (int)Math.Min(_restarts.Halves, _step / _halfSteps + 1);
        /// <summary>The accelerated match clock (GAME_DESIGN §14): 0 → 45 in the first half, 45 → 90 in the second.</summary>
        public float ClockMinutes
        {
            get
            {
                int half = Half;
                float within = (float)(_step - (half - 1) * _halfSteps) / _halfSteps;
                return ((half - 1) + Math.Min(1f, within)) * _restarts.DisplayMinutesPerHalf;
            }
        }
        public int TotalDisplayMinutes => (int)MathF.Round(_restarts.Halves * _restarts.DisplayMinutesPerHalf);
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
        private readonly DefenseDefinition _defense;
        private readonly GoalkeeperDefinition _gk;
        private readonly RestartsDefinition _restarts;
        private readonly long _halfSteps;
        private readonly ulong _seed;
        private AiTeam _firstKickoff;
        private Vector2 _humanAim;
        private readonly Rng _keeperRng;
        private readonly PlayerBody[] _allBodies = new PlayerBody[2 * MatchTeamSetup.StarterCount];
        private readonly float _durationMinutes;
        private readonly Rng _passRng;
        private readonly Rng _shotRng;
        private readonly IAiMatchObserver _observer;
        private readonly int _teamEvery, _roleEvery, _individualEvery;
        private long _step;
        private readonly long _totalSteps;
        private AiTeam _possession;
        private bool _possessionChanged;
        private float _secureTimer;

        // Human control (A5): one side's selected player follows the user's input; everyone else stays AI.
        public AiTeam HumanTeam { get; private set; }
        public ControlSelection Control { get; private set; }
        private readonly bool[] _eligible = new bool[MatchTeamSetup.StarterCount];
        private readonly MatchPlayerSetup[] _humanSetups = new MatchPlayerSetup[MatchTeamSetup.StarterCount];
        private ActionCommand _buffered = ActionCommand.None;
        private float _bufferedAge;
        private bool _bufferedWithoutBall;
        private bool _ballWasLoose;
        private bool _opponentPassed;
        /// <summary>True when the user's last kick was a buffered "de primeira" one.</summary>
        public bool LastKickFirstTime { get; private set; }

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
            _defense = db.Defense;
            _gk = db.Goalkeeper;
            _restarts = db.Restarts;
            _seed = setup.Seed;
            _observer = observer;
            StepSeconds = stepSeconds;
            _durationMinutes = setup.DurationMinutes;
            DurationSeconds = setup.DurationMinutes * 60f;
            _totalSteps = (long)MathF.Round(DurationSeconds / stepSeconds);
            _halfSteps = Math.Max(1, _totalSteps / _restarts.Halves);
            _totalSteps = _halfSteps * _restarts.Halves;
            Pitch = Pitch.From(db.Ball.Pitch);

            var streams = new RngStreams(setup.Seed);
            _passRng = streams.Get(PassStream);
            _shotRng = streams.Get(ShotStream);
            _keeperRng = streams.Get(KeeperStream);
            Home = new AiTeam(MatchSide.Home, new TeamFrame(true, Pitch.HalfLength), Formation(db, setup.Home), db.Tactics, setup.Home, 0, streams.Get(HomeStream));
            Away = new AiTeam(MatchSide.Away, new TeamFrame(false, Pitch.HalfLength), Formation(db, setup.Away), db.Tactics, setup.Away,
                MatchTeamSetup.StarterCount, streams.Get(AwayStream));
            for (int i = 0; i < MatchTeamSetup.StarterCount; i++)
            {
                Players[i] = Home.Players[i];
                Players[MatchTeamSetup.StarterCount + i] = Away.Players[i];
            }
            for (int i = 0; i < Players.Length; i++) _allBodies[i] = Players[i].Body;
            HomeKeeper = new Keeper(GoalkeeperOf(Home));
            AwayKeeper = new Keeper(GoalkeeperOf(Away));

            _teamEvery = Every(_ai.TeamHz, stepSeconds);
            _roleEvery = Every(_ai.RoleHz, stepSeconds);
            _individualEvery = Every(_ai.IndividualHz, stepSeconds);
            _firstKickoff = Home;
            Kickoff(Home);
        }

        private static FormationDefinition Formation(GameDatabase db, MatchTeamSetup team) =>
            db.Formation(team.Tactic.FormationId) ?? throw new ArgumentException("Unknown formation " + team.Tactic.FormationId);

        private static AiPlayer GoalkeeperOf(AiTeam team)
        {
            for (int i = 0; i < team.Players.Length; i++)
                if (team.Players[i].IsGoalkeeper) return team.Players[i];
            throw new ArgumentException("The formation " + team.Formation.Id + " has no GK slot.");
        }

        public Keeper KeeperOf(AiTeam team) => team == Home ? HomeKeeper : AwayKeeper;

        private static int Every(float hz, float step) => Math.Max(1, (int)MathF.Round(1f / (hz * step)));

        public AiTeam TeamOf(AiPlayer p) => p.Team;
        public AiTeam Opponents(AiTeam team) => team == Home ? Away : Home;
        public AiTeam PossessionTeam => _possession;

        /// <summary>The user takes over <paramref name="side"/> (A5): control starts on its kick-off taker / holder.</summary>
        public void EnableHuman(MatchSide side)
        {
            HumanTeam = side == MatchSide.Home ? Home : Away;
            for (int i = 0; i < HumanTeam.Players.Length; i++)
            {
                _eligible[i] = !HumanTeam.Players[i].IsGoalkeeper; // the goalkeeper stays AI (manual only in penalties, A8)
                _humanSetups[i] = HumanTeam.Players[i].Setup;
            }
            int start = 0;
            for (int i = 0; i < HumanTeam.Players.Length; i++)
                if (HumanTeam.Players[i].Slot.X > HumanTeam.Players[start].Slot.X) start = i;
            if (Ball.LastTouch >= 0 && HumanTeam.Owns(Ball.LastTouch) && !Players[Ball.LastTouch].IsGoalkeeper)
                start = Ball.LastTouch - HumanTeam.Players[0].Global;
            Control = new ControlSelection(start);
        }

        /// <summary>The player the user drives, or null in an all-AI match.</summary>
        public AiPlayer Controlled => HumanTeam == null ? null : HumanTeam.Players[Control.Controlled];
        /// <summary>The ring: who a manual switch would give control to (null when none).</summary>
        public AiPlayer NextCandidate => HumanTeam == null || Control.Next < 0 ? null : HumanTeam.Players[Control.Next];

        private bool IsHuman(AiPlayer p) => HumanTeam != null && p == Controlled;

        public AiMatchEvent Step() => Step(HumanInput.None);

        public AiMatchEvent Step(in HumanInput input)
        {
            if (Finished) return AiMatchEvent.Finished;
            float dt = StepSeconds;
            var ev = AiMatchEvent.None;
            _possessionChanged = false;
            _opponentPassed = false;
            _humanAim = input.Move;

            UpdatePossessionTeam();
            if (_possession != null) Stats(_possession).PossessionSteps++;
            ClearReceiver(Home);
            ClearReceiver(Away);
            Home.TransitionTimer = MathF.Max(0f, Home.TransitionTimer - dt);
            Away.TransitionTimer = MathF.Max(0f, Away.TransitionTimer - dt);
            for (int i = 0; i < Players.Length; i++) DefenseSystem.Tick(Players[i].Body, dt);
            _secureTimer = MathF.Max(0f, _secureTimer - dt);
            if (HumanTeam != null)
            {
                Control.Tick(dt);
                if (input.Action.Kind != ActionKind.None)
                {
                    _buffered = input.Action;
                    _bufferedAge = 0f;
                    _bufferedWithoutBall = Ball.Owner != Controlled.Global;
                }
            }

            // 2. AI: team (5 Hz), function (10 Hz), individual (10 Hz, staggered by player).
            if (_step % _teamEvery == 0) { TeamTick(Home); TeamTick(Away); }
            if (_step % _roleEvery == 0) { RoleTick(Home); RoleTick(Away); }
            float individualTick = _individualEvery * dt;
            for (int i = 0; i < Players.Length; i++)
                if ((_step + i) % _individualEvery == 0 && !IsHuman(Players[i]) && !Players[i].IsGoalkeeper) IndividualTick(Players[i], individualTick);
            KeeperThink(HomeKeeper, dt);
            KeeperThink(AwayKeeper, dt);

            // 3. Actions: the carrier's decision (the user's buffered action when he is the carrier), then the
            // defenders' automatic standing tackles.
            if (Restart != RestartKind.None)
            {
                var taken = RestartTick(dt);
                if (taken != AiMatchEvent.None) ev = taken;
            }
            else if (Ball.State == BallState.Controlled && Ball.Owner >= 0)
            {
                var carrier = Players[Ball.Owner];
                var kick = carrier.IsGoalkeeper ? KeeperDistribute(KeeperOf(carrier.Team), dt)
                    : IsHuman(carrier) ? HumanOnBall(carrier, input) : OnBall(carrier, dt);
                if (kick != AiMatchEvent.None) ev = kick;
            }
            else if (_buffered.Kind != ActionKind.None)
            {
                _bufferedAge += dt;
                if (_bufferedAge > _mv.InputBufferSeconds) _buffered = ActionCommand.None;
            }
            if (TryTackles()) ev = AiMatchEvent.Tackle;

            // 4. Movement (+ fatigue), then contact.
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                var noTarget = p.Body.Position;
                if (Restart != RestartKind.None && p == Taker) MoveTaker(p, dt);
                else if (p.IsGoalkeeper) KeeperMove(KeeperOf(p.Team), dt);
                else if (IsHuman(p)) { if (KeepOut(p, ref noTarget)) WalkOut(p, dt); else MoveHuman(p, input, dt); }
                else Move(p, dt);
            }
            Contact.Separate(_allBodies, _allBodies.Length, _mv.PlayerRadius);
            Goalkeeper.ClampInFront(HomeKeeper, _gk);
            Goalkeeper.ClampInFront(AwayKeeper, _gk);

            // Goalkeeper saves (hands before feet), then possession and dribble.
            bool saved = KeeperSave(HomeKeeper) || KeeperSave(AwayKeeper);
            if (saved) ev = AiMatchEvent.Save;
            if (Ball.State != BallState.Controlled && Ball.State != BallState.Dead && !saved)
            {
                for (int k = 0; k < Players.Length; k++)
                {
                    int i = (int)((_step + k) % Players.Length); // rotating start: no side wins ties by index
                    var p = Players[i];
                    if (p.IsGoalkeeper && !KeeperCanUseFeet(KeeperOf(p.Team))) continue;
                    bool intercepting = Ball.LastTouch >= 0 && !p.Team.Owns(Ball.LastTouch);
                    if (Possession.Step(i, p.Body, Ball, _mv, intercepting) != PossessionEvent.Captured) continue;
                    TakeBall(p);
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

            if (HumanTeam != null) UpdateControl(input);
            _ballWasLoose = Ball.State != BallState.Controlled;
            _step++;

            // 8. Clock: at the end of a half the other side kicks off the next one.
            if (!Finished && _step % _halfSteps == 0)
            {
                int half = (int)(_step / _halfSteps); // halves completed
                Kickoff(half % 2 == 0 ? _firstKickoff : Opponents(_firstKickoff));
                return AiMatchEvent.HalfTime;
            }
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
            _possessionChanged = true;
            _assistCandidate = -1;
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
                        // The receiver of our own pass expects it: no reaction delay.
                        if (p.ChaseReactionTimer < 0f)
                            p.ChaseReactionTimer = p.Team.Receiver == p.Local ? 0f : _balance.Eval(Effect.LooseBallReaction, p.Setup.Attributes);
                        p.ChaseReactionTimer -= tick;
                        if (p.ChaseReactionTimer <= 0f) SetIntention(p, desired);
                    }
                    else SetIntention(p, desired);
                }
            }
            else p.ChaseReactionTimer = -1f;

            if (p.Intention != AiIntention.OnBall) p.IntentTarget = IndividualLayer.Target(p, opp, Ball, Pitch, _ai, tick, _balance, _ballCfg.RollingFrictionDeceleration);
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
                    var pass = PassSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _ballCfg,
                        PassKind.Ground, aim, ActionCommand.AutoPower, false, pressure, team.Frame.Forward, _passRng);
                    Ball.LastTouch = p.Global;
                    SetIntention(p, AiIntention.HoldShape);
                    OnPassed(team, p, pass.Target);
                    if (team != HumanTeam) _opponentPassed = true;
                    return AiMatchEvent.Pass;
                }
                case OnBallChoice.Shot:
                    ShotSystem.Execute(p.Local, p.Body, Ball, p.Setup, _balance, _kicking, _ballCfg, Pitch, team.Frame.AttackingPositiveX,
                        Vector2.Zero, _ai.ShotPower, false, pressure, _shotRng);
                    Ball.LastTouch = p.Global;
                    SetIntention(p, AiIntention.HoldShape);
                    OnShot(p);
                    return AiMatchEvent.Shot;
                default:
                    p.DribbleTarget = decision.DribbleTarget;
                    p.DribbleTimer = _ai.MinDribbleSeconds;
                    return AiMatchEvent.None;
            }
        }

        private void TakeBall(AiPlayer p)
        {
            _pendingShot = -1;
            SetIntention(p, AiIntention.OnBall);
            p.DecisionTimer = _ai.DecisionIntervalSeconds;
            p.DribbleTimer = 0f;
            p.DribbleTarget = p.Body.Position;
            _secureTimer = _defense.PossessionSecureSeconds;
            if (p.IsGoalkeeper)
            {
                // Whatever reaches the keeper ends in his hands (placeholder: no back-pass rule until A8).
                var k = KeeperOf(p.Team);
                k.Timer = k.Dove ? MathF.Max(_gk.HoldSeconds, Goalkeeper.GetUpSeconds(k, _balance, _gk)) : _gk.HoldSeconds;
                k.State = KeeperState.Holding;
                k.Dove = false;
                return; // the user never drives the keeper (GAME_DESIGN §23: manual only in penalties)
            }
            if (HumanTeam != null && p.Team == HumanTeam) Control.OnCaptured(p.Local);
        }

        /// <summary>Every defender of the side without the ball gets an automatic standing tackle (A5, no dice).</summary>
        private bool TryTackles()
        {
            if (Restart != RestartKind.None || Ball.State != BallState.Controlled || Ball.Owner < 0 || _secureTimer > 0f) return false;
            var carrier = Players[Ball.Owner];
            if (carrier.IsGoalkeeper) return false; // the ball is in his hands
            var defenders = Opponents(carrier.Team);
            int n = defenders.Players.Length;
            for (int k = 0; k < n; k++)
            {
                var d = defenders.Players[(int)((_step + k) % n)]; // rotating start: no player wins ties by index
                if (!DefenseSystem.TryStandingTackle(d.Global, d.Body, d.Setup, carrier.Body, carrier.Setup, Ball, _balance, _defense)) continue;
                SetIntention(carrier, AiIntention.HoldShape);
                TakeBall(d);
                Stats(d.Team).Tackles++;
                return true;
            }
            return false;
        }

        /// <summary>The user is the carrier: execute his buffered pass / through ball / shot (A3 rules).</summary>
        private AiMatchEvent HumanOnBall(AiPlayer p, in HumanInput input)
        {
            if (_buffered.Kind == ActionKind.None) return AiMatchEvent.None;
            var command = _buffered;
            _buffered = ActionCommand.None;
            LastKickFirstTime = _bufferedWithoutBall;
            var team = p.Team;
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, Opponents(team));
            if (command.Kind == ActionKind.Shot)
            {
                ShotSystem.Execute(p.Local, p.Body, Ball, p.Setup, _balance, _kicking, _ballCfg, Pitch, team.Frame.AttackingPositiveX,
                    input.Move, command.Power, LastKickFirstTime, pressure, _shotRng);
                Ball.LastTouch = p.Global;
                OnShot(p);
                return AiMatchEvent.Shot;
            }
            var kind = command.Kind == ActionKind.Through ? PassKind.Through : PassKind.Ground;
            var pass = PassSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _ballCfg, kind,
                input.Move, command.Power, LastKickFirstTime, pressure, team.Frame.Forward, _passRng);
            Ball.LastTouch = p.Global;
            OnPassed(team, p, pass.Target);
            Control.OnPassReleased(p.Local, pass.Target, pass.PredictedStop, team.Bodies, team.Bodies.Length);
            if (team.Players[Control.Controlled].IsGoalkeeper) Control.OnCaptured(p.Local); // a back-pass: stay with the passer
            return AiMatchEvent.Pass;
        }

        /// <summary>The user's player: the stick, or — holding "Contenção" while the opponent has the ball — automatic
        /// goal-side containment of the carrier (GAME_DESIGN §22).</summary>
        private void MoveHuman(AiPlayer p, in HumanInput input, float dt)
        {
            var body = p.Body;
            bool hasBall = Ball.State == BallState.Controlled && Ball.Owner == p.Global;
            var move = input.Move;
            if (IsContaining(p, input))
            {
                var ownGoal = p.Team.Frame.OwnGoal;
                var carrier = Ball.Position;
                var away = new Vector3(ownGoal.X - carrier.X, ownGoal.Y - carrier.Y, 0f);
                float len = away.Length();
                var target = len > 1e-3f ? carrier + away / len * _ai.ContainDistance : carrier;
                float dx = target.X - body.Position.X, dy = target.Y - body.Position.Y;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                move = d > _ai.ArriveRadius ? new Vector2(dx / d, dy / d) : Vector2.Zero;
            }
            Movement.Step(body, _balance, p.Setup, move, input.Sprint, hasBall, DribbleSystem.IsLongTouch(body), _mv, _fatigue, dt);
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt);
            if (IsContaining(p, input))
            {
                float bx = Ball.Position.X - body.Position.X, by = Ball.Position.Y - body.Position.Y;
                float bl = MathF.Sqrt(bx * bx + by * by);
                if (bl > 1e-3f) body.Facing = new Vector3(bx / bl, by / bl, 0f); // a containing defender faces the carrier
            }
        }

        private bool IsContaining(AiPlayer p, in HumanInput input) =>
            input.ContainHeld && Ball.State == BallState.Controlled && Ball.Owner >= 0 && !p.Team.Owns(Ball.Owner);

        /// <summary>A5 control selection: with the ball, control is on the holder (set on capture/pass); without it,
        /// automatic switch on a trigger, manual switch by tap.</summary>
        private void UpdateControl(in HumanInput input)
        {
            var team = HumanTeam;
            // In possession (including our own pass in flight) control is set by the play itself: holder, or the
            // receiver as the ball leaves the foot. The automatic switch is a defending tool.
            if (team.HasPossession) return;

            bool opponentHasIt = Ball.State == BallState.Controlled && Ball.Owner >= 0;
            bool lost = _possessionChanged && _possession != team;
            bool becameLoose = Ball.State != BallState.Controlled && !_ballWasLoose;
            bool beaten = opponentHasIt && ControlSelection.IsBeaten(Controlled.Body, Ball.Position, team.Frame.OwnGoal, _defense);
            bool trigger = lost || becameLoose || _opponentPassed || beaten;
            bool containing = IsContaining(Controlled, input);

            Control.UpdateWithoutBall(team.Bodies, _humanSetups, _eligible, team.Bodies.Length, Ball.Position, team.Frame.OwnGoal,
                input.Move, containing, trigger, _balance, _defense);
            if (input.SwitchPressed && !containing) Control.ManualSwitch(_defense);
        }

        private void Move(AiPlayer p, float dt)
        {
            var body = p.Body;
            bool hasBall = Ball.State == BallState.Controlled && Ball.Owner == p.Global;
            var target = hasBall ? p.DribbleTarget : p.IntentTarget;
            if (KeepOut(p, ref target)) { WalkOut(p, dt); return; }
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

        // ---- goalkeepers (A6) ----

        private bool BallLoose => Ball.State == BallState.Rolling || Ball.State == BallState.Airborne;

        private void KeeperThink(Keeper k, float dt)
        {
            switch (k.State)
            {
                case KeeperState.Holding:
                    return;
                case KeeperState.Grounded:
                    k.Timer -= dt;
                    if (k.Timer <= 0f) k.State = KeeperState.Positioning;
                    return;
                case KeeperState.Diving:
                    k.Timer -= dt;
                    if (k.Timer <= 0f || !BallLoose) Land(k);
                    return;
                case KeeperState.Reacting:
                    if (!BallLoose) { k.State = KeeperState.Positioning; break; }
                    k.Timer -= dt;
                    if (k.Timer > 0f) return;
                    k.State = KeeperState.Tracking;
                    Track(k);
                    return;
                case KeeperState.Tracking:
                    Track(k);
                    if (k.State != KeeperState.Positioning) return; // still tracking, or committed to a dive
                    break;
            }

            // Positioning: watch for a shot, otherwise hold the bisector.
            Goalkeeper.TickError(k, _balance, _gk, _keeperRng, dt);
            if (Goalkeeper.Incoming(k, Ball, _gk))
            {
                float reaction = Goalkeeper.ReactionSeconds(k, _balance, _gk);
                if (Goalkeeper.Scan(k, Ball, Pitch, _ballCfg, _balance, _gk, reaction, StepSeconds).Threat)
                {
                    k.State = KeeperState.Reacting;
                    k.Timer = reaction;
                    k.LastReactionSeconds = reaction;
                    k.Target = k.Player.Body.Position;
                    return;
                }
            }
            k.Target = Goalkeeper.PositionTarget(k, Ball.Position, Pitch, _gk);
        }

        /// <summary>After reacting: stand set if the ball comes to the hands, run if he gets there on his feet, dive when
        /// a dive now covers the rest (a step and a dive), or as a last — possibly hopeless — stretch.</summary>
        private void Track(Keeper k)
        {
            if (!BallLoose) { k.State = KeeperState.Positioning; return; }
            var scan = Goalkeeper.Scan(k, Ball, Pitch, _ballCfg, _balance, _gk, 0f, StepSeconds);
            if (!scan.Threat) { k.State = KeeperState.Positioning; return; }
            var body = k.Player.Body;
            if (scan.Required <= 0f) { k.Target = body.Position; return; }

            float dx = scan.Point.X - body.Position.X, dy = scan.Point.Y - body.Position.Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            var dir = len > 1e-4f ? new Vector3(dx / len, dy / len, 0f) : Vector3.Zero;
            var attrs = k.Player.Setup.Attributes;
            float run = Goalkeeper.RunCover(scan.Time, _balance.Eval(Effect.SprintSpeed, attrs), _balance.Eval(Effect.AccelTime, attrs));
            float budget = Goalkeeper.DiveBudget(k, _balance, _gk);
            bool diveCovers = MathF.Min(budget, _gk.DiveSpeed * scan.Time) >= scan.Required;
            bool lastChance = scan.Time <= budget / _gk.DiveSpeed + StepSeconds;
            if (scan.Reachable && (run >= scan.Required || (!diveCovers && !lastChance)))
            {
                k.Target = body.Position + dir * scan.Required; // on his feet, diving later if he must
                return;
            }
            float distance = MathF.Min(scan.Required, budget);
            k.Target = body.Position + dir * distance;
            k.State = KeeperState.Diving;
            k.Dove = true;
            k.Timer = MathF.Max(scan.Time, distance / _gk.DiveSpeed) + 2f * StepSeconds;
        }

        private void Land(Keeper k)
        {
            k.State = KeeperState.Grounded;
            k.Timer = Goalkeeper.GetUpSeconds(k, _balance, _gk);
            k.Player.Body.Velocity = Vector3.Zero;
        }

        private bool KeeperCanUseFeet(Keeper k) => k.State != KeeperState.Diving && k.State != KeeperState.Grounded;

        private void KeeperMove(Keeper k, float dt)
        {
            var p = k.Player;
            var body = p.Body;
            switch (k.State)
            {
                case KeeperState.Holding:
                case KeeperState.Grounded:
                    body.Velocity = Vector3.Zero;
                    body.Sprinting = false;
                    break;
                case KeeperState.Diving:
                {
                    float dx = k.Target.X - body.Position.X, dy = k.Target.Y - body.Position.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float step = MathF.Min(d, _gk.DiveSpeed * dt);
                    var dir = d > 1e-4f ? new Vector3(dx / d, dy / d, 0f) : Vector3.Zero;
                    body.Position += dir * step;
                    body.Velocity = d > 1e-4f ? dir * (step / dt) : Vector3.Zero;
                    body.Sprinting = false;
                    break;
                }
                case KeeperState.Reacting:
                    Movement.Step(body, _balance, p.Setup, Vector2.Zero, false, false, false, _mv, _fatigue, dt);
                    break;
                default:
                {
                    float dx = k.Target.X - body.Position.X, dy = k.Target.Y - body.Position.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    var move = d > _gk.ArriveRadius ? new Vector2(dx / d, dy / d) : Vector2.Zero;
                    bool sprint = k.State == KeeperState.Tracking || d > _gk.SprintDistance;
                    bool hasBall = Ball.State == BallState.Controlled && Ball.Owner == p.Global;
                    Movement.Step(body, _balance, p.Setup, move, sprint, hasBall, false, _mv, _fatigue, dt);
                    break;
                }
            }
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt);
            if (k.State != KeeperState.Diving && k.State != KeeperState.Grounded)
            {
                float bx = Ball.Position.X - body.Position.X, by = Ball.Position.Y - body.Position.Y;
                float bl = MathF.Sqrt(bx * bx + by * by);
                if (bl > 1e-3f) body.Facing = new Vector3(bx / bl, by / bl, 0f); // the keeper always watches the ball
            }
            Goalkeeper.ClampInFront(k, _gk);
        }

        /// <summary>The ball reaching a keeper: hands (catch or parry) inside his area once he has reacted, or for any
        /// opponent's ball while he is set; just the body (a block) while he is still reacting or on the ground.</summary>
        private bool KeeperSave(Keeper k)
        {
            if (!BallLoose || k.State == KeeperState.Holding) return false;
            var p = k.Player;
            var body = p.Body;
            if (body.IgnoreBallUntilClear || Ball.Velocity.LengthSquared() < 1e-2f) return false;
            bool inArea = Pitch.InPenaltyArea(body.Position.X, body.Position.Y, !p.Team.Frame.AttackingPositiveX);
            bool opponentsBall = Ball.LastTouch >= 0 && !p.Team.Owns(Ball.LastTouch);
            bool hands = inArea && (k.State == KeeperState.Tracking || k.State == KeeperState.Diving
                                    || (k.State == KeeperState.Positioning && opponentsBall));
            bool block = k.State == KeeperState.Reacting || k.State == KeeperState.Grounded;
            if (!hands && !block) return false;
            if (!Goalkeeper.Touches(body, Ball, hands ? _gk.HandReach : _mv.PlayerRadius, _gk.ReachHeight, StepSeconds, out var contact)) return false;

            k.Saves++;
            Stats(p.Team).Saves++;
            if (_pendingShot >= 0 && !p.Team.Owns(_pendingShot)) OnShotOnTarget();
            _pendingShot = -1;
            float speed = Ball.Velocity.Length();
            if (hands && _keeperRng.NextFloat() < Goalkeeper.CatchChance(k, speed, contact.Z, Ball.Spin, k.Dove, _balance, _gk))
            {
                k.Catches++;
                Ball.Position = new Vector3(contact.X, contact.Y, 0f);
                Ball.Velocity = Vector3.Zero;
                Ball.Spin = 0f;
                Ball.State = BallState.Controlled;
                Ball.Owner = p.Global;
                Ball.LastTouch = p.Global;
                TakeBall(p);
                return true;
            }

            k.Parries++;
            var v = Goalkeeper.ParryVelocity(k, Ball, contact, _gk, _keeperRng);
            Ball.Position = new Vector3(contact.X, contact.Y, MathF.Max(0f, contact.Z));
            Possession.Kick(p.Global, body, Ball, v);
            if (k.Dove || k.State == KeeperState.Grounded) Land(k);
            else k.State = KeeperState.Positioning;
            k.Dove = false;
            return true;
        }

        /// <summary>The keeper holds the ball for a moment, then rolls it to a teammate: the AI's pass choice, or the
        /// most open outfield player in range (placeholder distribution until restarts/tactics, A7/A9).</summary>
        private AiMatchEvent KeeperDistribute(Keeper k, float dt)
        {
            var p = k.Player;
            if (k.State != KeeperState.Holding) { k.State = KeeperState.Holding; k.Timer = _gk.HoldSeconds; }
            k.Timer -= dt;
            if (k.Timer > 0f) return AiMatchEvent.None;

            var team = p.Team;
            var opp = Opponents(team);
            var decision = OnBallDecision.Decide(p, opp, Pitch, _balance, _ai);
            int receiver = decision.Choice == OnBallChoice.Pass ? decision.Receiver : Outlet(p, opp);
            var to = team.Players[receiver].Body.Position;
            var aim = new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y);
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, opp);
            var pass = PassSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _ballCfg,
                PassKind.Ground, aim, ActionCommand.AutoPower, false, pressure, team.Frame.Forward, _passRng);
            Ball.LastTouch = p.Global;
            k.State = KeeperState.Positioning;
            SetIntention(p, AiIntention.HoldShape);
            OnPassed(team, p, pass.Target);
            if (team == HumanTeam)
            {
                Control.OnPassReleased(p.Local, pass.Target, pass.PredictedStop, team.Bodies, team.Bodies.Length);
                if (team.Players[Control.Controlled].IsGoalkeeper) Control.OnCaptured(receiver);
            }
            else _opponentPassed = true;
            return AiMatchEvent.Pass;
        }

        /// <summary>The most open outfield teammate within pass range of <paramref name="from"/>, else the nearest one.</summary>
        private int Outlet(AiPlayer from, AiTeam opp)
        {
            var keeper = from;
            var team = keeper.Team;
            int best = -1, nearest = -1;
            float bestOpen = float.MinValue, nearestSqr = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var t = team.Players[i];
                if (t.IsGoalkeeper) continue;
                float d = Vector3.Distance(t.Body.Position, keeper.Body.Position);
                if (d * d < nearestSqr) { nearestSqr = d * d; nearest = i; }
                if (d < _ai.PassMinDistance || d > _ai.PassMaxDistance) continue;
                float open = OnBallDecision.NearestOpponentDistance(t.Body.Position, opp);
                if (open > bestOpen) { bestOpen = open; best = i; }
            }
            return best >= 0 ? best : nearest;
        }

        private void OnGoal()
        {
            var scorer = Ball.Position.X > 0f ? Home : Away; // the ball crossed the away (+x) line → home scored
            CountGoal(scorer);
            Kickoff(Opponents(scorer));
        }

        /// <summary>Both sides back to their kick-off shape (placed, not walked, for now) and <paramref name="kicking"/>'s
        /// most advanced player takes the kick-off from the centre spot.</summary>
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
            BeginRestart(RestartKind.Kickoff, kicking, taker, Vector3.Zero, fwd);
            taker.Body.Position = _takerStand;
            kicking.Phase = TeamPhase.Build;
            Opponents(kicking).Phase = TeamPhase.Defend;
            kicking.TransitionTimer = Opponents(kicking).TransitionTimer = 0f;
            SetPossession(kicking);
        }

        /// <summary>Puts a loose ball at <paramref name="p"/>'s feet, in open play (dev sandboxes and scenario tests).</summary>
        public void PlaceBall(AiPlayer p)
        {
            CancelRestart();
            GiveBall(p);
        }

        /// <summary>Drops any pending restart: open play from the current ball state (dev sandboxes and scenario tests).</summary>
        public void CancelRestart()
        {
            if (Taker != null && Taker.IsGoalkeeper) KeeperOf(Taker.Team).Reset();
            Restart = RestartKind.None;
            RestartReady = false;
            Taker = null;
            RestartTeam = null;
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
