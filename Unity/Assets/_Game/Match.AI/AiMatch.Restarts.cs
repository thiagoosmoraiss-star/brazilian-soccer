using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;

namespace Game.Match.AI
{
    /// <summary>Simple restarts of the vertical slice (A7a; TECHNICAL_SPEC §19 "saída, lateral, tiro de meta, escanteio curto").</summary>
    public enum RestartKind
    {
        None = 0,
        Kickoff = 1,
        ThrowIn = 2,
        GoalKick = 3,
        Corner = 4,
    }

    public sealed partial class AiMatch
    {
        /// <summary>The restart being set up or waiting to be taken; <see cref="RestartKind.None"/> in open play.</summary>
        public RestartKind Restart { get; private set; }
        /// <summary>The side taking it.</summary>
        public AiTeam RestartTeam { get; private set; }
        public AiPlayer Taker { get; private set; }
        /// <summary>True once the taker stands at the ball: it can be played (by the user if the taker is his).</summary>
        public bool RestartReady { get; private set; }
        public Vector3 RestartSpot { get; private set; }

        private Vector3 _takerStand;
        private Vector3 _takerFacing;
        private float _restartTimer;

        /// <summary>The ball is dead at <paramref name="spot"/>; <paramref name="taker"/> walks to it while the others
        /// set up, then plays it (GAME_DESIGN §28: "Passe = companheiro livre na direção"; X-55: tiro de meta só curto).</summary>
        private void BeginRestart(RestartKind kind, AiTeam team, AiPlayer taker, Vector3 spot, Vector2 facing)
        {
            Restart = kind;
            RestartTeam = team;
            Taker = taker;
            RestartReady = false;
            RestartSpot = new Vector3(spot.X, spot.Y, 0f);
            _takerFacing = new Vector3(facing.X, facing.Y, 0f);
            _takerStand = RestartSpot - _takerFacing * (_mv.PlayerRadius + _ballCfg.Radius);
            _restartTimer = _restarts.SetupSeconds;

            Ball.Position = RestartSpot;
            Ball.Velocity = Vector3.Zero;
            Ball.Spin = 0f;
            Ball.State = BallState.Dead;
            Ball.Owner = Ball.NoPlayer;
            Ball.LastTouch = taker.Global; // the restarting side is the side in possession
            for (int i = 0; i < Players.Length; i++) Players[i].Body.IgnoreBallUntilClear = false;
            HomeKeeper.Reset();
            AwayKeeper.Reset();
            _buffered = ActionCommand.None;
            // A restart cut short (half-time during the user's goal kick) must not leave him driving the keeper.
            if (HumanTeam != null && Controlled.IsGoalkeeper)
                Control.OnCaptured(team == HumanTeam && !taker.IsGoalkeeper ? taker.Local : NearestOutfield(HumanTeam, RestartSpot).Local);
        }

        /// <summary>Set-up countdown, then the ball at the taker's feet; an AI taker (or a user who waits too long)
        /// plays it after a moment.</summary>
        private AiMatchEvent RestartTick(float dt)
        {
            _restartTimer -= dt;
            if (!RestartReady)
            {
                if (_restartTimer > 0f) return AiMatchEvent.None;
                RestartReady = true;
                var body = Taker.Body;
                body.Position = _takerStand;
                body.Velocity = Vector3.Zero;
                body.Facing = _takerFacing;
                Ball.State = BallState.Controlled;
                Ball.Owner = Taker.Global;
                Ball.LastTouch = Taker.Global;
                SetIntention(Taker, AiIntention.OnBall);
                if (Taker.IsGoalkeeper) KeeperOf(Taker.Team).State = KeeperState.Holding;
                bool user = RestartTeam == HumanTeam;
                if (user) Control.OnCaptured(Taker.Local); // a goal kick included: the user aims the keeper's short pass
                _restartTimer = user ? _restarts.HumanTimeoutSeconds : _restarts.AiTakeSeconds;
                _buffered = ActionCommand.None;
                return AiMatchEvent.None;
            }

            if (RestartTeam == HumanTeam && _buffered.Kind != ActionKind.None)
            {
                var command = _buffered;
                _buffered = ActionCommand.None;
                if (command.Kind == ActionKind.Shot) return AiMatchEvent.None; // no direct shot from these restarts
                var kind = command.Kind == ActionKind.Through && Restart != RestartKind.GoalKick ? PassKind.Through : PassKind.Ground;
                return TakeRestart(_humanAim, kind, command.Power);
            }
            if (_restartTimer > 0f) return AiMatchEvent.None;

            var opp = Opponents(RestartTeam);
            var to = RestartTeam.Players[Outlet(Taker, opp)].Body.Position;
            return TakeRestart(new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y), PassKind.Ground, ActionCommand.AutoPower);
        }

        private AiMatchEvent TakeRestart(Vector2 aim, PassKind kind, float power)
        {
            var p = Taker;
            var team = RestartTeam;
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, Opponents(team));
            var pass = PassSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _ballCfg,
                kind, aim, power, false, pressure, team.Frame.Forward, _passRng);
            Ball.LastTouch = p.Global;
            if (p.IsGoalkeeper) KeeperOf(team).State = KeeperState.Positioning;
            SetIntention(p, AiIntention.HoldShape);
            Restart = RestartKind.None;
            RestartReady = false;
            Taker = null;
            RestartTeam = null;
            OnPassed(team, p, pass.Target);
            if (team == HumanTeam)
            {
                Control.OnPassReleased(p.Local, pass.Target, pass.PredictedStop, team.Bodies, team.Bodies.Length);
                if (team.Players[Control.Controlled].IsGoalkeeper) Control.OnCaptured(Outlet(p, Opponents(team)));
            }
            else _opponentPassed = true;
            return AiMatchEvent.Pass;
        }

        /// <summary>The taker walks to his spot during the set-up and stands still once ready.</summary>
        private void MoveTaker(AiPlayer p, float dt)
        {
            var body = p.Body;
            if (RestartReady)
            {
                body.Position = _takerStand;
                body.Velocity = Vector3.Zero;
                body.Facing = _takerFacing;
                body.Sprinting = false;
                return;
            }
            float dx = _takerStand.X - body.Position.X, dy = _takerStand.Y - body.Position.Y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            var move = d > _ai.ArriveRadius ? new Vector2(dx / d, dy / d) : Vector2.Zero;
            Movement.Step(body, _balance, p.Setup, move, true, false, false, _mv, _fatigue, dt);
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt);
        }

        private float ExclusionRadius => Restart == RestartKind.ThrowIn ? _restarts.ThrowInExclusionRadius : _restarts.ExclusionRadius;

        /// <summary>Opponents of the restarting side keep their distance until the ball is played: a target inside the
        /// circle is moved to its edge, and a player inside walks out of it.</summary>
        private bool KeepOut(AiPlayer p, ref Vector3 target)
        {
            if (Restart == RestartKind.None || p.Team == RestartTeam) return false;
            float r = ExclusionRadius;
            float tx = target.X - RestartSpot.X, ty = target.Y - RestartSpot.Y;
            float td = MathF.Sqrt(tx * tx + ty * ty);
            if (td < r)
            {
                var away = td > 1e-3f ? new Vector2(tx / td, ty / td) : OutwardFrom(p);
                target = new Vector3(MathUtil.Clamp(RestartSpot.X + away.X * r, -Pitch.HalfLength, Pitch.HalfLength),
                    MathUtil.Clamp(RestartSpot.Y + away.Y * r, -Pitch.HalfWidth, Pitch.HalfWidth), 0f);
            }
            float bx = p.Body.Position.X - RestartSpot.X, by = p.Body.Position.Y - RestartSpot.Y;
            return bx * bx + by * by < r * r;
        }

        /// <summary>A player inside the exclusion circle sprints straight out of it.</summary>
        private void WalkOut(AiPlayer p, float dt)
        {
            var body = p.Body;
            float bx = body.Position.X - RestartSpot.X, by = body.Position.Y - RestartSpot.Y;
            float bd = MathF.Sqrt(bx * bx + by * by);
            var away = bd > 1e-3f ? new Vector2(bx / bd, by / bd) : OutwardFrom(p);
            // Never out over a line: at the edge of the pitch, leave the circle along the line instead.
            if (MathF.Abs(body.Position.X) >= Pitch.HalfLength - _restarts.LineInset && away.X * body.Position.X > 0f) away.X = 0f;
            if (MathF.Abs(body.Position.Y) >= Pitch.HalfWidth - _restarts.LineInset && away.Y * body.Position.Y > 0f) away.Y = 0f;
            if (away.LengthSquared() < 1e-4f) away = OutwardFrom(p);
            Movement.Step(body, _balance, p.Setup, away, true, false, false, _mv, _fatigue, dt);
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt);
        }

        private static Vector2 OutwardFrom(AiPlayer p) => -p.Team.Frame.Forward; // back towards his own goal

        /// <summary>Where a dead ball goes next (placeholder rules removed in A7a): over the touchline → throw-in to the
        /// other side of the last touch; over the goal line → goal kick if the attackers touched it last, corner if the
        /// defenders did.</summary>
        private void OnOut()
        {
            var p = Ball.Position;
            AiTeam lastTeam = Ball.LastTouch >= 0 ? Players[Ball.LastTouch].Team : null;
            bool overGoalLine = MathF.Abs(p.X) >= Pitch.HalfLength - 1e-3f;
            if (!overGoalLine)
            {
                var team = lastTeam == null ? Home : Opponents(lastTeam);
                float side = p.Y >= 0f ? 1f : -1f;
                var spot = new Vector3(MathUtil.Clamp(p.X, -Pitch.HalfLength + _restarts.CornerInset, Pitch.HalfLength - _restarts.CornerInset),
                    side * (Pitch.HalfWidth - _restarts.LineInset), 0f);
                BeginRestart(RestartKind.ThrowIn, team, NearestOutfield(team, spot), spot, new Vector2(0f, -side));
                Stats(team).ThrowIns++;
                return;
            }

            var defending = p.X > 0f ? Away : Home; // home defends the -x goal
            if (lastTeam == defending)
            {
                var attacking = Opponents(defending);
                float sx = p.X > 0f ? 1f : -1f, sy = p.Y >= 0f ? 1f : -1f;
                var spot = new Vector3(sx * (Pitch.HalfLength - _restarts.CornerInset), sy * (Pitch.HalfWidth - _restarts.CornerInset), 0f);
                var toCentre = Vector2.Normalize(new Vector2(-spot.X, -spot.Y));
                BeginRestart(RestartKind.Corner, attacking, NearestOutfield(attacking, spot), spot, toCentre);
                Stats(attacking).Corners++;
                RecordEvent(MatchEventType.Corner, attacking.Side, Game.Core.Ids.Id.None, Game.Core.Ids.Id.None);
                return;
            }

            var f = defending.Frame;
            float lateral = f.V(p) >= 0f ? _restarts.GoalKickLateral : -_restarts.GoalKickLateral;
            BeginRestart(RestartKind.GoalKick, defending, KeeperOf(defending).Player, f.World(_restarts.GoalKickDepth, lateral), f.Forward);
            Stats(defending).GoalKicks++;
        }

        private static AiPlayer NearestOutfield(AiTeam team, Vector3 spot)
        {
            AiPlayer best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                if (p.IsGoalkeeper) continue;
                float d = Vector3.DistanceSquared(p.Body.Position, spot);
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
            return best;
        }
    }
}
