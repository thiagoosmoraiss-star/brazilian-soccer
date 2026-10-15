using System;
using System.Numerics;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// The high ball (A9a; GAME_DESIGN §19/§20/§23; X-73):
    /// <list type="bullet">
    /// <item>Alto: lançamento, or a cross in the cross zone (<see cref="HighBallSystem"/>), by the user, by the AI's on-ball
    /// decision, and at corners, goal kicks and throw-ins.</item>
    /// <item>Header: a ball between foot and head height within reach of players is contested — the AerialDuel effect
    /// (Cabeceio + Força) and being closer decide — and the winner heads it: at goal in range, to a teammate, or clear.
    /// The user's buffered CHUTE heads at goal and PASSE to a teammate ("Cabeceio: Chute = ao gol, Passe = para
    /// companheiro").</item>
    /// <item>Drop zone: the receiver and the nearest players of each side go to where the ball comes down to head height.</item>
    /// <item>Crosses and the keeper: a ball coming down in his area within GkCrossClaimRange makes him decide once
    /// (GkCrossDecisionQuality) to come and claim it — catch or punch by the A6 rules.</item>
    /// </list>
    /// Zero allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string AerialStream = "Aerial";
        private const float DegToRad = MathF.PI / 180f;
        private const float AerialHorizonSeconds = 5f;

        private readonly AerialDefinition _aerial;
        /// <summary>The shot in flight was headed (goal statistics).</summary>
        private bool _headerShotInFlight;
        private readonly Game.Core.Random.Rng _aerialRng;
        private readonly int[] _duelists = new int[2 * Game.Core.Contracts.Match.MatchTeamSetup.StarterCount];
        private readonly float[] _duelWeights = new float[2 * Game.Core.Contracts.Match.MatchTeamSetup.StarterCount];

        /// <summary>Where the ball in the air comes down to head height (valid while <see cref="HasDropPoint"/>).</summary>
        public Vector3 DropPoint { get; private set; }
        public bool HasDropPoint { get; private set; }

        /// <summary>Head height the drop zone is judged at: between the foot (capture) height and the top of a jump.</summary>
        private float DropHeight => 0.5f * (_mv.PossessionCaptureMaxHeight + _aerial.HeaderReachHeight);

        /// <summary>Refreshed with the team layer: where an airborne ball comes down.</summary>
        private void UpdateDropPoint()
        {
            HasDropPoint = false;
            if (Ball.State != BallState.Airborne || _pendingShot >= 0) return; // not a shot in flight
            float t = HighBallSystem.TimeToHeight(Ball, DropHeight, _ballCfg.Gravity, _ballCfg.AirDragCoefficient, AerialHorizonSeconds);
            if (t < 0f) return;
            var p = HighBallSystem.PositionAfter(Ball, t, _ballCfg.Gravity, _ballCfg.AirDragCoefficient);
            DropPoint = new Vector3(p.X, p.Y, 0f);
            HasDropPoint = true;
        }

        // ---------------- headers ----------------

        /// <summary>A ball in the air between foot and head height near players: they contest it and the winner heads it.
        /// Runs after the keepers' hands and before the ground possession.</summary>
        private AiMatchEvent TryHeaders()
        {
            // A ball coming down from a high pass, cross or clearance; a shot in flight or a ball still rising off the
            // boot is not met with the head.
            if (Ball.State != BallState.Airborne || Restart != RestartKind.None || _pendingShot >= 0 || Ball.Velocity.Z > 0f) return AiMatchEvent.None;
            float z = Ball.Position.Z;
            if (z <= _mv.PossessionCaptureMaxHeight || z > _aerial.HeaderReachHeight) return AiMatchEvent.None;

            int n = 0;
            float total = 0f, r = _aerial.HeaderRadius;
            bool home = false, away = false;
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                var b = p.Body;
                if (p.IsGoalkeeper || b.OffPitch || b.Down || b.IgnoreBallUntilClear) continue;
                float dx = b.Position.X - Ball.Position.X, dy = b.Position.Y - Ball.Position.Y;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > r) continue;
                float w = _balance.Eval(Effect.AerialDuel, p.Setup.Attributes) * (1f + _aerial.HeaderClosenessWeight * (1f - d / r));
                _duelists[n] = i;
                _duelWeights[n] = w;
                total += w;
                n++;
                if (p.Team == Home) home = true; else away = true;
            }
            if (n == 0) return AiMatchEvent.None;
            if (home && away) { HomeStats.AerialDuels++; AwayStats.AerialDuels++; }

            float roll = _aerialRng.NextFloat() * total;
            int winner = _duelists[n - 1];
            for (int k = 0; k < n; k++)
            {
                roll -= _duelWeights[k];
                if (roll <= 0f) { winner = _duelists[k]; break; }
            }
            var header = Players[winner];
            if (OffsideOnTouch(header)) return AiMatchEvent.Offside;
            return Head(header);
        }

        /// <summary>The duel's winner heads the ball: at goal in range (or on the user's CHUTE), to a teammate (or on his
        /// PASSE), or — a defender, or nothing better on — a clearance away from his goal.</summary>
        private AiMatchEvent Head(AiPlayer p)
        {
            var team = p.Team;
            Stats(team).Headers++;
            _pendingShot = -1;
            var command = ActionCommand.None;
            if (IsHuman(p) && _buffered.Kind != ActionKind.None) { command = _buffered; _buffered = ActionCommand.None; }

            var f = team.Frame;
            var goal = f.TargetGoal;
            float toGoal = new Vector2(goal.X - Ball.Position.X, goal.Y - Ball.Position.Y).Length();
            bool attackingBall = f.U(Ball.Position) > Pitch.HalfLength;
            bool shoot = command.Kind == ActionKind.Shot || (command.Kind == ActionKind.None && attackingBall && toGoal <= _aerial.HeaderShotRange);
            if (shoot) return HeaderShot(p, command.Kind == ActionKind.Shot ? _humanAim : Vector2.Zero);

            bool pass = command.Kind != ActionKind.None || attackingBall;
            if (pass)
            {
                var aim = command.Kind != ActionKind.None && _humanAim.LengthSquared() > 0.01f ? _humanAim : AheadTeammate(p);
                return HeaderPass(p, aim);
            }
            return Clearance(p);
        }

        private AiMatchEvent HeaderShot(AiPlayer p, Vector2 aim)
        {
            var team = p.Team;
            var body = p.Body;
            var from = new Vector2(Ball.Position.X, Ball.Position.Y);
            float goalX = Pitch.AttackedGoalLineX(team.Frame.AttackingPositiveX);
            float cornerY = ShotSystem.ChooseCornerY(from, new Vector2(body.Facing.X, body.Facing.Y), aim, goalX, Pitch, _kicking.Shot);
            var target = new Vector3(goalX, cornerY, _aerial.HeaderShotTargetHeight);
            var delta = new Vector2(target.X - from.X, target.Y - from.Y);
            float distance = MathF.Max(delta.Length(), 0.5f);
            var dir = delta / distance;
            float speed = _balance.Eval(Effect.HeaderPower, p.Setup.Attributes);
            float accuracy = _balance.Eval(Effect.HeaderAccuracy, p.Setup.Attributes);
            float hError = accuracy * KickErrorRules.Triangular(_shotRng);
            float vError = accuracy * _aerial.HeaderVerticalErrorFraction * KickErrorRules.Triangular(_shotRng);
            float elevation = ShotSystem.LaunchElevation(speed, distance, target.Z - Ball.Position.Z, _ballCfg.Gravity) + vError * DegToRad;
            var h = Rotate(dir, hError * DegToRad);
            float c = MathF.Cos(elevation), s = MathF.Sin(elevation);
            Possession.Kick(p.Local, body, Ball, new Vector3(h.X * c * speed, h.Y * c * speed, s * speed));
            Ball.LastTouch = p.Global;
            SetIntention(p, AiIntention.HoldShape);
            OnShot(p);
            Stats(team).HeaderShots++;
            _headerShotInFlight = true;
            if (p.Team != HumanTeam) _opponentPassed = true;
            return AiMatchEvent.Shot;
        }

        private AiMatchEvent HeaderPass(AiPlayer p, Vector2 aim)
        {
            var team = p.Team;
            var body = p.Body;
            float speed = _balance.Eval(Effect.HeaderPower, p.Setup.Attributes) * _aerial.HeaderPassSpeedFactor;
            var dir = aim.LengthSquared() > 1e-4f ? Vector2.Normalize(aim) : team.Frame.Forward;
            int target = PassSystem.FindTarget(p.Local, team.Bodies, team.Bodies.Length, dir, _kicking.Pass);
            if (target >= 0)
            {
                var to = team.Players[target].Body.Position;
                var d = new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y);
                if (d.LengthSquared() > 1e-4f) dir = Vector2.Normalize(d);
            }
            float error = _balance.Eval(Effect.HeaderAccuracy, p.Setup.Attributes) * KickErrorRules.Triangular(_passRng);
            dir = Rotate(dir, error * DegToRad);
            float e = _aerial.HeaderPassElevationDegrees * DegToRad;
            Possession.Kick(p.Local, body, Ball, new Vector3(dir.X * MathF.Cos(e) * speed, dir.Y * MathF.Cos(e) * speed, MathF.Sin(e) * speed));
            Ball.LastTouch = p.Global;
            SetIntention(p, AiIntention.HoldShape);
            OnPassed(team, p, target);
            if (team == HumanTeam)
                Control.OnPassReleased(p.Local, target, HighBallSystem.LandingPoint(Ball, _ballCfg.Gravity, _ballCfg.AirDragCoefficient),
                    team.Bodies, team.Bodies.Length);
            else _opponentPassed = true;
            return AiMatchEvent.Header;
        }

        private AiMatchEvent Clearance(AiPlayer p)
        {
            var team = p.Team;
            float spread = (_aerialRng.NextFloat() * 2f - 1f) * _aerial.ClearanceSpreadDegrees * DegToRad;
            var dir = Rotate(team.Frame.Forward, spread);
            float speed = _balance.Eval(Effect.HeaderPower, p.Setup.Attributes);
            float e = _aerial.ClearanceElevationDegrees * DegToRad;
            Possession.Kick(p.Local, p.Body, Ball, new Vector3(dir.X * MathF.Cos(e) * speed, dir.Y * MathF.Cos(e) * speed, MathF.Sin(e) * speed));
            Ball.LastTouch = p.Global;
            ClearOffside();
            SetIntention(p, AiIntention.HoldShape);
            if (team != HumanTeam) _opponentPassed = true;
            return AiMatchEvent.Header;
        }

        /// <summary>Direction to the nearest teammate further up the pitch (the attack direction if there is none).</summary>
        private Vector2 AheadTeammate(AiPlayer p)
        {
            var team = p.Team;
            float u0 = team.Frame.U(p.Body.Position);
            float best = float.MaxValue;
            var dir = team.Frame.Forward;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (q == p || q.IsGoalkeeper || q.Body.OffPitch || team.Frame.U(q.Body.Position) <= u0) continue;
                float dx = q.Body.Position.X - p.Body.Position.X, dy = q.Body.Position.Y - p.Body.Position.Y;
                float d = dx * dx + dy * dy;
                if (d < best) { best = d; dir = new Vector2(dx, dy); }
            }
            return dir;
        }

        // ---------------- the keeper and crosses ----------------

        /// <summary>An opponent's ball in the air coming down in the keeper's area within his claim range: he decides once
        /// (GkCrossDecisionQuality) whether to come for it. True when he does.</summary>
        private bool ConsiderClaim(Keeper k)
        {
            if (Ball.State != BallState.Airborne) { k.ClaimDecided = false; return false; }
            if (k.ClaimDecided || Ball.LastTouch < 0 || k.Player.Team.Owns(Ball.LastTouch)) return false;
            float t = HandsMeetTime();
            if (t < 0f) return false;
            var point = HighBallSystem.PositionAfter(Ball, t, _ballCfg.Gravity, _ballCfg.AirDragCoefficient);
            var team = k.Player.Team;
            if (!Pitch.InPenaltyArea(point.X, point.Y, !team.Frame.AttackingPositiveX)) return false;
            var body = k.Player.Body;
            float dx = point.X - body.Position.X, dy = point.Y - body.Position.Y;
            if (dx * dx + dy * dy > Square(_balance.Eval(Effect.GkCrossClaimRange, k.Player.Setup.Attributes))) return false;
            k.ClaimDecided = true;
            if (_keeperRng.NextFloat() >= _balance.Eval(Effect.GkCrossDecisionQuality, k.Player.Setup.Attributes)) return false;
            k.State = KeeperState.Claiming;
            k.Target = new Vector3(point.X, point.Y, 0f);
            return true;
        }

        /// <summary>Claiming: heads for where the ball comes down to his hands, until it is his, gone, or on the ground.</summary>
        private void Claim(Keeper k)
        {
            if (Ball.State != BallState.Airborne) { k.State = KeeperState.Positioning; return; }
            float t = HandsMeetTime();
            var at = t >= 0f ? HighBallSystem.PositionAfter(Ball, t, _ballCfg.Gravity, _ballCfg.AirDragCoefficient) : Ball.Position;
            k.Target = new Vector3(at.X, at.Y, 0f);
        }

        /// <summary>Seconds until the ball comes down to the keeper's hands: to his reach height, or — a cross that never
        /// rises that high — to head height; -1 if neither.</summary>
        private float HandsMeetTime()
        {
            float t = HighBallSystem.TimeToHeight(Ball, _gk.ReachHeight, _ballCfg.Gravity, _ballCfg.AirDragCoefficient, AerialHorizonSeconds);
            return t >= 0f ? t : HighBallSystem.TimeToHeight(Ball, DropHeight, _ballCfg.Gravity, _ballCfg.AirDragCoefficient, AerialHorizonSeconds);
        }

        // ---------------- the AI's high balls ----------------

        /// <summary>The AI's Alto: a cross in the cross zone, else a lançamento to <paramref name="receiver"/>.</summary>
        private AiMatchEvent AiHighBall(AiPlayer p, int receiver)
        {
            var team = p.Team;
            var opp = Opponents(team);
            bool cross = HighBallSystem.InCrossZone(p.Body.Position, team.Frame.AttackingPositiveX, Pitch, _aerial);
            var to = receiver >= 0 ? team.Players[receiver].Body.Position : DefaultCrossPoint(team);
            var aim = new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y);
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, opp);
            var pass = HighBallSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _aerial, _ballCfg,
                Pitch, team.Frame.AttackingPositiveX, cross, aim, ActionCommand.AutoPower, false, pressure, float.PositiveInfinity, _passRng);
            Ball.LastTouch = p.Global;
            SetIntention(p, AiIntention.HoldShape);
            if (cross) Stats(team).Crosses++; else Stats(team).LongBalls++;
            OnPassed(team, p, pass.Target);
            if (team != HumanTeam) _opponentPassed = true;
            return AiMatchEvent.Pass;
        }

        /// <summary>The user's Alto in open play.</summary>
        private AiMatchEvent HumanHighBall(AiPlayer p, Vector2 aim, float power)
        {
            var team = p.Team;
            bool cross = HighBallSystem.InCrossZone(p.Body.Position, team.Frame.AttackingPositiveX, Pitch, _aerial);
            float pressure = OnBallDecision.NearestOpponentDistance(p.Body.Position, Opponents(team));
            var pass = HighBallSystem.Execute(p.Local, team.Bodies, team.Bodies.Length, Ball, p.Setup, _balance, _kicking, _aerial, _ballCfg,
                Pitch, team.Frame.AttackingPositiveX, cross, aim, power, LastKickFirstTime, pressure, float.PositiveInfinity, _passRng);
            Ball.LastTouch = p.Global;
            if (cross) Stats(team).Crosses++; else Stats(team).LongBalls++;
            OnPassed(team, p, pass.Target);
            Control.OnPassReleased(p.Local, pass.Target, pass.PredictedStop, team.Bodies, team.Bodies.Length);
            if (team.Players[Control.Controlled].IsGoalkeeper) Control.OnCaptured(p.Local);
            return AiMatchEvent.Pass;
        }

        private Vector3 DefaultCrossPoint(AiTeam team) => team.Frame.World(Pitch.Length - _aerial.CrossDefaultDepth, 0f);

        private static float Square(float x) => x * x;

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }
    }
}
