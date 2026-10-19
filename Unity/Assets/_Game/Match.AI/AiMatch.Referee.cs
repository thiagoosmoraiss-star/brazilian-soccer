using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;
using SharedRules = Game.Rules.Match.MatchRules;

namespace Game.Match.AI
{
    /// <summary>
    /// The referee (A8a; TECHNICAL_SPEC §7 "Referee | faltas, cartões, impedimento"; GAME_DESIGN §22/§29; X-64/X-65):
    /// <list type="bullet">
    /// <item>Fouls: a defender who runs into the carrier without winning the ball ("contato antes da bola") commits a
    /// foul with the FoulChance effect (Desarme), more from behind and at speed, times the referee's strictness,
    /// normalised by the match duration. Clean standing tackles (X-53) are never fouls.</item>
    /// <item>Cards: the per-foul chances shared with QuickSim (<see cref="SharedRules.CardForFoul"/>); denying an obvious
    /// goal-scoring opportunity is a straight red outside the area, a penalty and a yellow inside; two yellows = red.
    /// A dismissed player leaves the pitch (never below the minimum of players).</item>
    /// <item>Offside: judged when a teammate plays the ball (not from a throw-in, corner or goal kick); the offside
    /// player who then plays the ball concedes an indirect free kick where he is.</item>
    /// </list>
    /// Free kicks (indirect, direct with an automatic wall) and penalties are restarts (<see cref="RestartKind"/>).
    /// Zero allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string RefereeStream = "Referee";
        private const int MaxWall = 10;

        private readonly RefereeDefinition _referee;
        private readonly SharedRules _rules;
        private readonly Game.Core.Random.Rng _refereeRng;

        private readonly int[] _playerFouls = new int[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _playerYellows = new int[2 * MatchTeamSetup.StarterCount];
        /// <summary>Display minute a dismissed player left (0 while he is on).</summary>
        private readonly int[] _sentOffMinute = new int[2 * MatchTeamSetup.StarterCount];

        // Offside: who was in an offside position when a teammate last played the ball.
        private readonly bool[] _offsideMask = new bool[2 * MatchTeamSetup.StarterCount];
        private AiTeam _offsideTeam;
        /// <summary>Set while a throw-in / corner / goal kick is being played: no offside from those.</summary>
        private bool _offsideExempt;

        // The wall of a direct free kick.
        private readonly int[] _wallPlayer = new int[MaxWall];
        private readonly Vector3[] _wallSlot = new Vector3[MaxWall];
        private int _wallCount;

        /// <summary>The penalty kick is in flight (for the conversion statistics).</summary>
        private bool _penaltyInPlay;

        public int Fouls(AiPlayer p) => _playerFouls[p.Global];
        public int Yellows(AiPlayer p) => _playerYellows[p.Global];
        public bool SentOff(AiPlayer p) => _sentOffMinute[p.Global] > 0;
        public int WallCount => _wallCount;
        /// <summary>The user's stick this step (set-piece aim and the penalty dive; A8b presentation).</summary>
        public Vector2 HumanAim => _humanAim;
        public AiPlayer WallPlayer(int i) => Players[_wallPlayer[i]];
        /// <summary>True when <paramref name="p"/> was in an offside position at the last pass of his side.</summary>
        public bool InOffsidePosition(AiPlayer p) => _offsideTeam == p.Team && _offsideMask[p.Global];

        /// <summary>Awards a penalty to <paramref name="side"/> now (scenario tests, dev tools).</summary>
        public void AwardPenalty(MatchSide side) => BeginPenalty(side == MatchSide.Home ? Home : Away);

        /// <summary>Awards a free kick to <paramref name="side"/> at <paramref name="spot"/> now (scenario tests, dev tools).</summary>
        public void AwardFreeKick(MatchSide side, Vector3 spot, bool direct) =>
            BeginFreeKick(direct ? RestartKind.DirectFreeKick : RestartKind.FreeKick, side == MatchSide.Home ? Home : Away, spot);

        public int PlayersOnPitch(AiTeam team)
        {
            int n = 0;
            for (int i = 0; i < team.Players.Length; i++)
                if (!team.Players[i].Body.OffPitch) n++;
            return n;
        }

        // ---------------- fouls ----------------

        /// <summary>A defender who runs into the carrier — body contact while still closing in, the standing tackle having
        /// found no exposed ball this step ("contato antes da bola") — makes a challenge; the referee may whistle. A
        /// jockeying defender (no closing speed) never does. Runs after <see cref="TryTackles"/>.</summary>
        private bool TryChallenges()
        {
            if (Restart != RestartKind.None || Ball.State != BallState.Controlled || Ball.Owner < 0 || _secureTimer > 0f) return false;
            var carrier = Players[Ball.Owner];
            if (carrier.IsGoalkeeper) return false;
            var defenders = Opponents(carrier.Team);
            var cb = carrier.Body;
            int n = defenders.Players.Length;
            for (int k = 0; k < n; k++)
            {
                var d = defenders.Players[(int)((_step + k) % n)];
                var db = d.Body;
                if (d.IsGoalkeeper || db.OffPitch || db.Down || db.TackleCooldown > 0f) continue;

                float dx = cb.Position.X - db.Position.X, dy = cb.Position.Y - db.Position.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > _referee.ContactDistance || dist < 1e-4f) continue;
                float nx = dx / dist, ny = dy / dist;
                float closing = (db.Velocity.X - cb.Velocity.X) * nx + (db.Velocity.Y - cb.Velocity.Y) * ny;
                if (closing < _referee.MinClosingSpeed) continue; // jockeying, not lunging

                db.TackleCooldown = MathF.Max(db.TackleCooldown, _referee.ChallengeCooldownSeconds);
                float chance = FoulChance(d, carrier, closing, -nx, -ny);
                if (_refereeRng.NextFloat() >= chance) continue; // fair contact: play on
                OnFoul(d, carrier);
                return true;
            }
            return false;
        }

        /// <summary>Chance that this challenge is a foul (GAME_DESIGN §22: ângulo, velocidade, rigor do árbitro).
        /// (<paramref name="fromX"/>, <paramref name="fromY"/>) = unit vector from the carrier to the defender.</summary>
        public float FoulChance(AiPlayer defender, AiPlayer carrier, float closingSpeed, float fromX, float fromY)
        {
            var r = _referee;
            // Normalised by the configured duration (as fatigue, GAME_DESIGN §19): the same fouls per match in 4, 6 or 10 min.
            float duration = _fatigue.ReferenceDurationMinutes / _durationMinutes;
            float chance = _balance.Eval(Effect.FoulChance, defender.Setup.Attributes) * r.FoulScale * r.Strictness * duration;
            float over = MathUtil.Clamp01((closingSpeed - r.MinClosingSpeed) / r.SpeedReference);
            chance *= 1f + r.SpeedMultiplier * over;
            var f = carrier.Body.Facing;
            float cos = f.X * fromX + f.Y * fromY;
            if (cos < MathF.Cos(r.BehindAngleDegrees * MathF.PI / 180f)) chance *= r.BehindMultiplier;
            return MathUtil.Clamp01(chance);
        }

        /// <summary>Whistle: foul, card, then a direct free kick — or a penalty inside the fouler's area.
        /// <paramref name="minYellowChance"/>: a foul the card rules let go is booked with at least this chance (a slide
        /// from behind, A9b).</summary>
        private void OnFoul(AiPlayer fouler, AiPlayer fouled, float minYellowChance = 0f)
        {
            var foulingTeam = fouler.Team;
            var fouledTeam = fouled.Team;
            Stats(foulingTeam).Fouls++;
            _playerFouls[fouler.Global]++;
            RecordEvent(MatchEventType.Foul, foulingTeam.Side, fouler.Setup.PlayerId, fouled.Setup.PlayerId);

            var spot = new Vector3(MathUtil.Clamp(Ball.Position.X, -Pitch.HalfLength + _restarts.CornerInset, Pitch.HalfLength - _restarts.CornerInset),
                MathUtil.Clamp(Ball.Position.Y, -Pitch.HalfWidth + _restarts.LineInset, Pitch.HalfWidth - _restarts.LineInset), 0f);
            bool penalty = Pitch.InPenaltyArea(spot.X, spot.Y, fouledTeam.Frame.AttackingPositiveX);
            bool denial = DeniesGoal(fouler, fouled, spot);

            var card = _rules.CardForFoul(_refereeRng, _playerYellows[fouler.Global] > 0);
            if (card == SharedRules.Card.None && minYellowChance > 0f && _refereeRng.NextFloat() < minYellowChance) card = SharedRules.Card.Yellow;
            if (denial && card != SharedRules.Card.StraightRed) card = penalty ? SharedRules.Card.Yellow : SharedRules.Card.StraightRed;
            if (card == SharedRules.Card.Yellow) Book(fouler);
            else if (card == SharedRules.Card.StraightRed) SendOff(fouler);

            if (penalty) BeginPenalty(fouledTeam);
            else BeginFreeKick(RestartKind.DirectFreeKick, fouledTeam, spot);
        }

        /// <summary>Denying an obvious goal-scoring opportunity: close to goal and the fouler was the last outfield
        /// defender between the carrier and the goal.</summary>
        private bool DeniesGoal(AiPlayer fouler, AiPlayer fouled, Vector3 spot)
        {
            var attack = fouled.Team.Frame;
            var goal = attack.TargetGoal;
            if (Vector3.Distance(spot, goal) > _referee.DenialDistance) return false;
            float carrierU = attack.U(fouled.Body.Position);
            var defenders = fouler.Team.Players;
            for (int i = 0; i < defenders.Length; i++)
            {
                var t = defenders[i];
                if (t == fouler || t.IsGoalkeeper || t.Body.OffPitch) continue;
                if (attack.U(t.Body.Position) > carrierU) return false; // a covering defender between him and the goal
            }
            return true;
        }

        private void Book(AiPlayer p)
        {
            _playerYellows[p.Global]++;
            Stats(p.Team).YellowCards++;
            RecordEvent(MatchEventType.YellowCard, p.Team.Side, p.Setup.PlayerId, Id.None);
            if (_playerYellows[p.Global] >= 2) SendOff(p);
        }

        /// <summary>Red card: the player leaves the pitch (Law 3: never below the minimum of players, as in QuickSim).</summary>
        private void SendOff(AiPlayer p)
        {
            if (p.Body.OffPitch || PlayersOnPitch(p.Team) <= _referee.MinPlayersOnPitch) return;
            Stats(p.Team).RedCards++;
            _sentOffMinute[p.Global] = EventMinute;
            RecordEvent(MatchEventType.RedCard, p.Team.Side, p.Setup.PlayerId, Id.None);
            Park(p);
            if (Ball.Owner == p.Global) { Ball.Owner = Ball.NoPlayer; Ball.State = BallState.Rolling; }
            if (HumanTeam == p.Team)
            {
                _eligible[p.Local] = false;
                if (Control.Controlled == p.Local) Control.OnCaptured(NearestOutfield(HumanTeam, Ball.Position).Local);
            }
        }

        /// <summary>A dismissed player waits beyond the touchline, each in his own place (no contact with anybody).</summary>
        private void Park(AiPlayer p)
        {
            var b = p.Body;
            b.OffPitch = true;
            float side = p.Team == Home ? -1f : 1f;
            b.Position = new Vector3(-Pitch.HalfLength + 4f * p.Global, side * (Pitch.HalfWidth + 6f), 0f);
            b.Velocity = Vector3.Zero;
            b.Sprinting = false;
            p.Moving = false;
            SetIntention(p, AiIntention.HoldShape);
            p.IntentTarget = p.RoleTarget = p.IdealTarget = p.PendingTarget = b.Position;
        }

        // ---------------- offside ----------------

        /// <summary>A teammate of <paramref name="kicker"/> plays the ball now: mark who is in an offside position (in the
        /// opponents' half, ahead of the ball and of the second-last opponent).</summary>
        private void MarkOffside(AiTeam team, AiPlayer kicker)
        {
            ClearOffside();
            if (_offsideExempt) return;
            var f = team.Frame;
            float first = float.MinValue, second = float.MinValue;
            var opp = Opponents(team).Players;
            for (int i = 0; i < opp.Length; i++)
            {
                if (opp[i].Body.OffPitch) continue;
                float u = f.U(opp[i].Body.Position);
                if (u > first) { second = first; first = u; }
                else if (u > second) second = u;
            }
            float ballU = f.U(Ball.Position);
            float tol = _referee.OffsideTolerance;
            float duration = _fatigue.ReferenceDurationMinutes / _durationMinutes;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (q == kicker || q.Body.OffPitch) continue;
                float u = f.U(q.Body.Position);
                if (u <= Pitch.HalfLength || u <= ballU + tol) continue;
                if (u > second + tol) { _offsideMask[q.Global] = true; continue; }
                // Close call: running onto the line as the ball is played, he may have gone a fraction too early.
                if (u <= second - _referee.CloseCallWindow) continue;
                var v = q.Body.Velocity;
                if (v.X * f.Forward.X + v.Y * f.Forward.Y < _referee.CloseCallRunSpeed) continue;
                float chance = _referee.CloseCallChance * _balance.Eval(Effect.AiTargetError, q.Setup.Attributes) * duration;
                if (_refereeRng.NextFloat() < chance) _offsideMask[q.Global] = true;
            }
            _offsideTeam = team;
        }

        private void ClearOffside()
        {
            if (_offsideTeam == null) return;
            for (int i = 0; i < _offsideMask.Length; i++) _offsideMask[i] = false;
            _offsideTeam = null;
        }

        /// <summary>Called when <paramref name="p"/> is about to take a loose ball: true (and the indirect free kick
        /// awarded) when he was offside; otherwise the offside phase ends with this touch.</summary>
        private bool OffsideOnTouch(AiPlayer p)
        {
            if (_offsideTeam == null) return false;
            bool offside = _offsideTeam == p.Team && _offsideMask[p.Global];
            ClearOffside();
            if (!offside) return false;
            Stats(p.Team).Offsides++;
            var defending = Opponents(p.Team);
            var spot = new Vector3(MathUtil.Clamp(p.Body.Position.X, -Pitch.HalfLength + _restarts.CornerInset, Pitch.HalfLength - _restarts.CornerInset),
                MathUtil.Clamp(p.Body.Position.Y, -Pitch.HalfWidth + _restarts.LineInset, Pitch.HalfWidth - _restarts.LineInset), 0f);
            BeginFreeKick(RestartKind.FreeKick, defending, spot);
            return true;
        }

        // ---------------- free kicks and penalties ----------------

        /// <summary>A free kick for <paramref name="team"/> at <paramref name="spot"/>; a direct one near goal gets a wall.</summary>
        private void BeginFreeKick(RestartKind kind, AiTeam team, Vector3 spot)
        {
            var goal = team.Frame.TargetGoal;
            var toGoal = new Vector2(goal.X - spot.X, goal.Y - spot.Y);
            float dist = toGoal.Length();
            var facing = kind == RestartKind.DirectFreeKick && dist > 1e-3f ? toGoal / dist : team.Frame.Forward;
            BeginRestart(kind, team, NearestOutfield(team, spot), spot, facing);
            _restartTimer = _referee.FreeKickSetupSeconds;
            Stats(team).FreeKicks++;
            if (kind == RestartKind.DirectFreeKick) BuildWall(Opponents(team), spot, toGoal, dist);
        }

        /// <summary>The defenders nearest to the wall's centre form it on the line from the ball to the goal's centre,
        /// at the free-kick distance.</summary>
        private void BuildWall(AiTeam defending, Vector3 spot, Vector2 toGoal, float dist)
        {
            _wallCount = 0;
            int size = Math.Min(MaxWall, _referee.WallSize(dist));
            if (size <= 0 || dist < 1e-3f) return;
            var dir = toGoal / dist;
            var perp = new Vector2(-dir.Y, dir.X);
            float at = MathF.Min(_restarts.ExclusionRadius + _referee.WallDistanceMargin, dist - 1f);
            var centre = new Vector3(spot.X + dir.X * at, spot.Y + dir.Y * at, 0f);
            for (int n = 0; n < size; n++)
            {
                int best = -1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < defending.Players.Length; i++)
                {
                    var p = defending.Players[i];
                    if (p.IsGoalkeeper || p.Body.OffPitch || InWall(p.Global)) continue;
                    float d = Vector3.DistanceSquared(p.Body.Position, centre);
                    if (d < bestSqr) { bestSqr = d; best = p.Global; }
                }
                if (best < 0) break;
                float offset = (n - (size - 1) * 0.5f) * _referee.WallSpacing;
                _wallPlayer[_wallCount] = best;
                _wallSlot[_wallCount] = new Vector3(centre.X + perp.X * offset, centre.Y + perp.Y * offset, 0f);
                _wallCount++;
            }
        }

        private bool InWall(int global)
        {
            for (int i = 0; i < _wallCount; i++)
                if (_wallPlayer[i] == global) return true;
            return false;
        }

        /// <summary>When the free kick is ready, a wall player not yet in his place is put there (as the taker is placed
        /// at the ball): the kick never waits for the wall.</summary>
        private void SetWall()
        {
            for (int i = 0; i < _wallCount; i++)
            {
                var b = Players[_wallPlayer[i]].Body;
                if (Vector3.DistanceSquared(b.Position, _wallSlot[i]) <= _ai.ArriveRadius * _ai.ArriveRadius) continue;
                b.Position = _wallSlot[i];
                b.Velocity = Vector3.Zero;
            }
        }

        /// <summary>Where a wall player stands, or false when <paramref name="p"/> is not in the wall.</summary>
        private bool WallTarget(AiPlayer p, out Vector3 target)
        {
            for (int i = 0; i < _wallCount; i++)
            {
                if (_wallPlayer[i] != p.Global) continue;
                target = _wallSlot[i];
                return true;
            }
            target = default;
            return false;
        }

        /// <summary>A penalty for <paramref name="team"/>: its best taker (Compostura) at the spot, the keeper on his line,
        /// everybody else outside the area and the arc.</summary>
        private void BeginPenalty(AiTeam team)
        {
            var spot = team.Frame.World(Pitch.Length - _referee.PenaltySpotDistance, 0f);
            BeginRestart(RestartKind.Penalty, team, BestPenaltyTaker(team), spot, team.Frame.Forward);
            _restartTimer = _referee.PenaltySetupSeconds;
            Stats(team).Penalties++;
            RecordEvent(MatchEventType.Penalty, team.Side, Taker.Setup.PlayerId, Id.None);
        }

        private AiPlayer BestPenaltyTaker(AiTeam team)
        {
            AiPlayer best = null;
            float bestWobble = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var p = team.Players[i];
                if (p.IsGoalkeeper || p.Body.OffPitch) continue;
                float w = _balance.Eval(Effect.PenaltyAimWobble, p.Setup.Attributes);
                if (w < bestWobble) { bestWobble = w; best = p; }
            }
            return best;
        }

        /// <summary>The keeper facing a penalty waits on his line, in the middle of the goal.</summary>
        private bool PenaltyKeeper(Keeper k) => Restart == RestartKind.Penalty && k.Player.Team != RestartTeam;

        /// <summary>Penalty set-up: everybody but the taker and the defending keeper stays out of the area and of the
        /// circle around the spot. Same contract as <see cref="KeepOut"/>.</summary>
        private bool KeepOutOfPenaltyArea(AiPlayer p, ref Vector3 target)
        {
            if (Restart != RestartKind.Penalty || p == Taker || (p.IsGoalkeeper && p.Team != RestartTeam)) return false;
            var attack = RestartTeam.Frame;
            float lineU = Pitch.Length - Pitch.PenaltyAreaDepth - _referee.AreaClearance; // outside the area, towards halfway
            if (attack.U(target) > lineU) target = attack.World(lineU, attack.V(target));
            float r = _restarts.ExclusionRadius;
            float tx = target.X - RestartSpot.X, ty = target.Y - RestartSpot.Y;
            float td = MathF.Sqrt(tx * tx + ty * ty);
            if (td < r)
            {
                var away = td > 1e-3f ? new Vector2(tx / td, ty / td) : -attack.Forward;
                target = new Vector3(RestartSpot.X + away.X * r, RestartSpot.Y + away.Y * r, 0f);
            }
            var pos = p.Body.Position;
            float px = pos.X - RestartSpot.X, py = pos.Y - RestartSpot.Y;
            return attack.U(pos) > lineU || px * px + py * py < r * r;
        }

        /// <summary>When the penalty is ready, anybody still inside the area or the circle is placed at its edge and the
        /// keeper on his line (as the taker is placed at the ball): the kick never waits and nobody stands in its way.</summary>
        private void ClearPenaltyArea()
        {
            for (int i = 0; i < Players.Length; i++)
            {
                var p = Players[i];
                if (p.Body.OffPitch) continue;
                if (p.IsGoalkeeper && p.Team != RestartTeam)
                {
                    p.Body.Position = p.Team.Frame.World(_gk.MinDepth, 0f);
                    p.Body.Velocity = Vector3.Zero;
                    continue;
                }
                var target = p.Body.Position;
                if (!KeepOutOfPenaltyArea(p, ref target)) continue;
                p.Body.Position = target;
                p.Body.Velocity = Vector3.Zero;
            }
        }

        /// <summary>Out of the area (straight back towards halfway), then out of the circle.</summary>
        private void WalkOutOfPenaltyArea(AiPlayer p, float dt)
        {
            var attack = RestartTeam.Frame;
            var body = p.Body;
            float lineU = Pitch.Length - Pitch.PenaltyAreaDepth - _referee.AreaClearance;
            Vector2 away;
            if (attack.U(body.Position) > lineU) away = -attack.Forward;
            else
            {
                float bx = body.Position.X - RestartSpot.X, by = body.Position.Y - RestartSpot.Y;
                float bd = MathF.Sqrt(bx * bx + by * by);
                away = bd > 1e-3f ? new Vector2(bx / bd, by / bd) : -attack.Forward;
            }
            Movement.Step(body, _balance, p.Setup, away, true, false, false, _mv, _fatigue, dt);
            Fatigue.Drain(body, _balance, p.Setup, body.Sprinting, _fatigue, _durationMinutes, dt, p.Team.Runtime.FatigueIntensity);
        }

        /// <summary>The AI plays a ready free kick or penalty: a penalty is a shot to a random side, a direct free kick
        /// in range a shot, anything else a pass to the most open teammate.</summary>
        private AiMatchEvent AiSetPiece()
        {
            if (Restart == RestartKind.Penalty)
            {
                var fwd = RestartTeam.Frame.Forward;
                float side = _shotRng.NextFloat() < 0.5f ? 1f : -1f;
                return TakeShotRestart(new Vector2(-fwd.Y, fwd.X) * side, _referee.PenaltyShotPower);
            }
            if (Restart == RestartKind.DirectFreeKick && DirectShotInRange())
                return TakeShotRestart(Vector2.Zero, _referee.FreeKickShotPower);
            var opp = Opponents(RestartTeam);
            var to = RestartTeam.Players[Outlet(Taker, opp)].Body.Position;
            return TakeRestart(new Vector2(to.X - Ball.Position.X, to.Y - Ball.Position.Y), PassKind.Ground, ActionCommand.AutoPower);
        }

        /// <summary>Close enough and central enough for the AI to shoot from a direct free kick.</summary>
        public bool DirectShotInRange()
        {
            var f = RestartTeam.Frame;
            var goal = f.TargetGoal;
            float dx = goal.X - RestartSpot.X, dy = goal.Y - RestartSpot.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > _referee.DirectShotDistance || dist < 1e-3f) return false;
            float cos = (dx * f.Forward.X + dy * f.Forward.Y) / dist;
            return cos >= MathF.Cos(_referee.DirectShotMaxAngleDegrees * MathF.PI / 180f);
        }

        /// <summary>A direct free kick or a penalty shot (A3 shot rules; a penalty adds the taker's Compostura wobble and
        /// the keeper commits to a side as it is struck).</summary>
        private AiMatchEvent TakeShotRestart(Vector2 aim, float power)
        {
            var p = Taker;
            var team = RestartTeam;
            bool penalty = Restart == RestartKind.Penalty;
            float wobble = penalty ? _balance.Eval(Effect.PenaltyAimWobble, p.Setup.Attributes) : 0f;
            ShotSystem.Execute(p.Local, p.Body, Ball, p.Setup, _balance, _kicking, _ballCfg, Pitch, team.Frame.AttackingPositiveX,
                aim, power, false, float.PositiveInfinity, _shotRng, wobble);
            Ball.LastTouch = p.Global;
            SetIntention(p, AiIntention.HoldShape);
            EndRestart();
            OnShot(p);
            if (penalty)
            {
                _penaltyInPlay = true;
                var keeper = KeeperOf(Opponents(team));
                if (Opponents(team) == HumanTeam) UserKeeperGuess(keeper);
                else KeeperGuess(keeper);
            }
            if (team == HumanTeam) Control.OnCaptured(p.Local);
            else _opponentPassed = true;
            return AiMatchEvent.Shot;
        }

        /// <summary>GAME_DESIGN §28 "Pênalti defendendo: escolher lado no chute": the AI keeper stays or dives to a side
        /// as the ball is struck; whether he reaches it is the A6 save physics.</summary>
        private void KeeperGuess(Keeper k)
        {
            float roll = _keeperRng.NextFloat();
            if (roll < _referee.KeeperStayChance) return; // stays set in the middle: the normal reaction takes over
            var body = k.Player.Body;
            float side = roll < _referee.KeeperStayChance + (1f - _referee.KeeperStayChance) * 0.5f ? 1f : -1f;
            k.Target = new Vector3(body.Position.X, side * _referee.KeeperDiveLateral, 0f);
            k.State = KeeperState.Diving;
            k.Dove = true;
            k.Timer = MathF.Abs(k.Target.Y - body.Position.Y) / _gk.DiveSpeed + 0.3f;
        }

        /// <summary>GAME_DESIGN §28 "Pênalti defendendo: escolher lado no chute" (A8b): the user's keeper dives to the side
        /// the stick points to (across the goal) as the ball is struck; a stick at rest keeps him set in the middle.</summary>
        private void UserKeeperGuess(Keeper k)
        {
            float lateral = _humanAim.Y; // across the goal mouth (the goal lines are at ±x)
            if (MathF.Abs(lateral) < _referee.UserDiveDeadzone) return;
            var body = k.Player.Body;
            k.Target = new Vector3(body.Position.X, MathF.Sign(lateral) * _referee.KeeperDiveLateral, 0f);
            k.State = KeeperState.Diving;
            k.Dove = true;
            k.Timer = MathF.Abs(k.Target.Y - body.Position.Y) / _gk.DiveSpeed + 0.3f;
        }

        /// <summary>A penalty against the user: the stick picks the keeper's dive, so it does not move his player.</summary>
        private bool PenaltyAgainstUser => Restart == RestartKind.Penalty && HumanTeam != null && RestartTeam != HumanTeam;

        private void EndRestart()
        {
            Restart = RestartKind.None;
            RestartReady = false;
            Taker = null;
            RestartTeam = null;
            _wallCount = 0;
        }
    }
}
