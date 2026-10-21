using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Math;
using Game.Data.Effects;
using Game.Data.Match;
using Game.Rules.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// Defence and attack duels (A9b; GAME_DESIGN §21-§23; TECHNICAL_SPEC §7; coefficients in Data/Balance/duels.json):
    /// <list type="bullet">
    /// <item>Carrinho: the user's CARRINHO (aimed at the ball when it is roughly where the stick points) and the AI presser
    /// in range, from the front. The leg meets the ball first → the ball is poked away; the body meets an opponent first →
    /// foul ("contato antes da bola"), from behind booked with a high chance; a miss leaves him on the ground.</item>
    /// <item>Second defender ("Pressionar", held): the nearest teammate goes straight at the carrier.</item>
    /// <item>Body duel (basic): a loose ball reached by both sides goes by BodyDuelWin (Força) and closeness; overlapping
    /// bodies are pushed apart by strength.</item>
    /// <item>Cover: a beaten presser is replaced by the nearest goal-side defender; one defender covers behind the presser.</item>
    /// <item>Runs (ruptura): with space behind the opponents' line and a carrier whose Visão sees it (RunTriggerThreshold),
    /// the attacker nearest the line runs in behind, starting onside; the AI carrier plays the through ball while he is onside.</item>
    /// <item>Keeper 1×1: a carrier alone in front of goal makes the keeper rush (GkRushDecisionQuality: a bad call hesitates)
    /// and smother an exposed ball in his area; for a through ball he comes only if he gets there first.</item>
    /// </list>
    /// Zero allocation per step.
    /// </summary>
    public sealed partial class AiMatch
    {
        public const string DuelsStream = "Duels";
        public const string ShotBlockStream = "ShotBlock";
        public const string FirstTouchStream = "FirstTouch";

        private readonly DuelsDefinition _duels;
        private readonly Game.Core.Random.Rng _duelsRng;
        private Game.Core.Random.Rng _blockRng;
        private Game.Core.Random.Rng _touchRng;
        /// <summary>Who already had his one chance to block the shot in flight (F1b).</summary>
        private readonly bool[] _blockTried = new bool[2 * MatchTeamSetup.StarterCount];
        /// <summary>BodyDuelWin ^ pushExponent per player (attributes do not change during a match).</summary>
        private readonly float[] _bodyWeight = new float[2 * MatchTeamSetup.StarterCount];
        private readonly int[] _contestants = new int[2 * MatchTeamSetup.StarterCount];
        private readonly float[] _contestWeights = new float[2 * MatchTeamSetup.StarterCount];

        /// <summary>A shot in flight passing a defender's body this step (F1b, "bolas desviadas"): each outfield opponent of
        /// the shooter gets one chance per shot, his ShotBlockChance (Desarme) x chanceScale x closeness. A block deflects the
        /// ball (slower, turned, maybe up): it may run loose, or out for a corner.</summary>
        private bool TryShotBlock()
        {
            if (_duels.BlockChanceScale <= 0f || _pendingShot < 0 || !BallLoose || Ball.Position.Z > _duels.BlockMaxHeight) return false;
            var v = Ball.Velocity;
            float speed2 = v.X * v.X + v.Y * v.Y;
            if (speed2 < _duels.BlockMinSpeed * _duels.BlockMinSpeed) return false;
            var shooterTeam = Players[_pendingShot].Team;
            var defenders = Opponents(shooterTeam).Players;
            var a = new Vector2(Ball.Position.X, Ball.Position.Y);
            var ab = new Vector2(v.X, v.Y) * StepSeconds;
            float len2 = ab.LengthSquared();
            for (int i = 0; i < defenders.Length; i++)
            {
                var d = defenders[i];
                if (d.IsGoalkeeper || d.Body.OffPitch || d.Body.Down || _blockTried[d.Global]) continue;
                var bp = new Vector2(d.Body.Position.X, d.Body.Position.Y);
                float t = len2 > 1e-8f ? MathUtil.Clamp01(Vector2.Dot(bp - a, ab) / len2) : 0f;
                float dist = Vector2.Distance(a + ab * t, bp);
                if (dist > _duels.BlockReach) continue;
                _blockTried[d.Global] = true;
                float chance = _balance.Eval(Effect.ShotBlockChance, d.Setup.Attributes) * _duels.BlockChanceScale * (1f - dist / _duels.BlockReach);
                if (_blockRng.NextFloat() >= chance) continue;
                float angle = (2f * _blockRng.NextFloat() - 1f) * _duels.BlockMaxDeflectDegrees * MathF.PI / 180f;
                float factor = _duels.BlockSpeedFactorMin + (_duels.BlockSpeedFactorMax - _duels.BlockSpeedFactorMin) * _blockRng.NextFloat();
                float c = MathF.Cos(angle), s = MathF.Sin(angle);
                var out2 = new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c) * factor;
                Possession.Kick(d.Global, d.Body, Ball, new Vector3(out2.X, out2.Y, _blockRng.NextFloat() * _duels.BlockMaxUpSpeed));
                Stats(d.Team).ShotBlocks++;
                _pendingShot = -1;
                _headerShotInFlight = false;
                return true;
            }
            return false;
        }

        /// <summary>The first touch of <paramref name="p"/>, who just captured a ball arriving at <paramref name="incoming"/>
        /// (D-36): error (m) = FirstTouchError (Drible, Compostura) x scale x speed factor x PressureErrorMult when an opponent is
        /// within pressureRadius x |triangular|. Above threshold the touch escapes: the ball runs on along its line, turned, about
        /// error x escapeDistanceScale m, loose for anyone (the receiver too, once it is clear of him). Goalkeepers use their hands.</summary>
        private bool FirstTouchEscapes(AiPlayer p, Vector3 incoming)
        {
            if (_duels.TouchScale <= 0f || p.IsGoalkeeper) return false;
            float speed = MathF.Sqrt(incoming.X * incoming.X + incoming.Y * incoming.Y);
            if (speed < _duels.TouchMinSpeed) return false;
            float error = _balance.Eval(Effect.FirstTouchError, p.Setup.Attributes) * _duels.TouchScale
                          * Math.Clamp(speed / _duels.TouchReferenceSpeed, _duels.TouchSpeedFactorMin, _duels.TouchSpeedFactorMax);
            if (OnBallDecision.NearestOpponentDistance(p.Body.Position, Opponents(p.Team)) < _duels.TouchPressureRadius)
                error *= _balance.Eval(Effect.PressureErrorMult, p.Setup.Attributes);
            error *= MathF.Abs(KickErrorRules.Triangular(_touchRng));
            if (error <= _duels.TouchThreshold) return false;
            float angle = (2f * _touchRng.NextFloat() - 1f) * _duels.TouchMaxEscapeDegrees * MathF.PI / 180f;
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            var dir = new Vector2(incoming.X, incoming.Y) / speed;
            dir = new Vector2(dir.X * c - dir.Y * s, dir.X * s + dir.Y * c);
            // Rolling to rest over d m at constant friction a: v = sqrt(2 a d).
            float v = MathF.Sqrt(2f * _ballCfg.RollingFrictionDeceleration * error * _duels.TouchEscapeDistanceScale);
            Possession.Release(Ball);
            Possession.Kick(p.Global, p.Body, Ball, new Vector3(dir.X * v, dir.Y * v, 0f));
            // He touched it: whatever was in flight (a shot, a penalty, an offside position) is over, as in TakeBall.
            _pendingShot = -1;
            _penaltyInPlay = false;
            _headerShotInFlight = false;
            ClearOffside();
            Stats(p.Team).Miscontrols++;
            return true;
        }

        private void InitDuels()
        {
            for (int i = 0; i < Players.Length; i++) _bodyWeight[i] = BodyWeight(Players[i].Setup);
        }

        /// <summary>Contact weight of a player (BodyDuelWin); recomputed when a substitute takes the slot (A9c).</summary>
        private float BodyWeight(MatchPlayerSetup setup) =>
            MathF.Pow(MathF.Max(1e-3f, _balance.Eval(Effect.BodyDuelWin, setup.Attributes)), _duels.BodyPushExponent);

        // ---------------- carrinho ----------------

        /// <summary>The user's CARRINHO: along the stick (or facing), turned onto the ball when it is in range and within
        /// the assist angle of that direction (Semi assistance).</summary>
        private void HumanSlide(in HumanInput input)
        {
            if (!input.SlidePressed || HumanTeam == null || Restart != RestartKind.None) return;
            var p = Controlled;
            var b = p.Body;
            if (b.Down || b.OffPitch) return;
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0 && HumanTeam.Owns(Ball.Owner)) return; // our ball: no slide

            var dir = input.Move.LengthSquared() > 0.01f ? Vector2.Normalize(input.Move) : new Vector2(b.Facing.X, b.Facing.Y);
            var aim = SlideAim(p);
            var to = new Vector2(aim.X - b.Position.X, aim.Y - b.Position.Y);
            float d = to.Length();
            if (d > 1e-3f && d <= DefenseSystem.SlideRange(p.Setup, _balance, _duels)
                && Vector2.Dot(dir, to / d) >= MathF.Cos(_duels.SlideAssistAngleDegrees * DegToRad))
                dir = to / d;
            BeginSlide(p, new Vector3(dir.X, dir.Y, 0f));
        }

        /// <summary>Where the ball will be when a slide from <paramref name="p"/> gets there (it moves with its carrier).</summary>
        private Vector3 SlideAim(AiPlayer p)
        {
            var ball = new Vector3(Ball.Position.X, Ball.Position.Y, 0f);
            var v = Ball.State == BallState.Controlled && Ball.Owner >= 0 ? Players[Ball.Owner].Body.Velocity : Ball.Velocity;
            float d = Vector2.Distance(new Vector2(ball.X, ball.Y), new Vector2(p.Body.Position.X, p.Body.Position.Y));
            float t = d / _duels.SlideMinSpeed;
            return new Vector3(ball.X + v.X * t, ball.Y + v.Y * t, 0f);
        }

        private void BeginSlide(AiPlayer p, Vector3 direction)
        {
            DefenseSystem.StartSlide(p.Body, direction, _duels);
            p.Moving = false;
            Stats(p.Team).Slides++;
        }

        /// <summary>An AI presser (or the second defender) with the carrier in slide range, in front of him and the ball
        /// nearer than the carrier's body, slides with the data hazard. Never from behind.</summary>
        private void AiSlides(float dt)
        {
            if (Restart != RestartKind.None || Ball.State != BallState.Controlled || Ball.Owner < 0) return;
            var carrier = Players[Ball.Owner];
            if (carrier.IsGoalkeeper) return;
            var cb = carrier.Body;
            var defenders = Opponents(carrier.Team);
            float cosMax = MathF.Cos(_duels.AiSlideMaxApproachDegrees * DegToRad);
            for (int i = 0; i < defenders.Players.Length; i++)
            {
                var p = defenders.Players[i];
                var b = p.Body;
                if (p.IsGoalkeeper || b.OffPitch || b.Down || IsHuman(p) || p.Intention != AiIntention.Press) continue;
                float toBall = Distance2D(b.Position, Ball.Position);
                if (toBall <= _balance.Eval(Effect.TackleReach, p.Setup.Attributes)) continue; // the standing tackle's job
                if (toBall > DefenseSystem.SlideRange(p.Setup, _balance, _duels)) continue;
                if (toBall >= Distance2D(b.Position, cb.Position)) continue; // the carrier's body is in the way
                float dx = b.Position.X - cb.Position.X, dy = b.Position.Y - cb.Position.Y;
                float dl = MathF.Sqrt(dx * dx + dy * dy);
                if (dl < 1e-3f || (cb.Facing.X * dx + cb.Facing.Y * dy) / dl < cosMax) continue; // not from the front
                if (_duelsRng.NextFloat() >= _duels.AiSlideChancePerSecond * dt) continue;
                var aim = SlideAim(p);
                float ax = aim.X - b.Position.X, ay = aim.Y - b.Position.Y;
                float al = MathF.Sqrt(ax * ax + ay * ay);
                if (al < 1e-3f) continue;
                BeginSlide(p, new Vector3(ax / al, ay / al, 0f));
            }
        }

        /// <summary>Sliding players against the ball and the bodies, after movement and before the separation: the leg on
        /// the ball first wins it; an opponent's body first is a foul.</summary>
        private AiMatchEvent ResolveSlides()
        {
            if (Restart != RestartKind.None) return AiMatchEvent.None;
            for (int k = 0; k < Players.Length; k++)
            {
                var p = Players[(int)((_step + k) % Players.Length)];
                var b = p.Body;
                if (b.SlideRemaining <= 0f || b.SlideWonBall || b.OffPitch) continue;

                bool ownBall = Ball.State == BallState.Controlled && Ball.Owner >= 0 && p.Team.Owns(Ball.Owner);
                bool inHands = Ball.State == BallState.Controlled && Ball.Owner >= 0 && Players[Ball.Owner].IsGoalkeeper;
                if (!ownBall && !inHands && Ball.State != BallState.Dead && DefenseSystem.SlideMeetsBall(b, p.Setup, Ball, _balance, _duels))
                {
                    AiPlayer carrier = Ball.State == BallState.Controlled && Ball.Owner >= 0 ? Players[Ball.Owner] : null;
                    DefenseSystem.SlideWin(p.Global, b, carrier?.Body, Ball, _defense, _duels);
                    if (carrier != null) SetIntention(carrier, AiIntention.HoldShape);
                    _pendingShot = -1;
                    _headerShotInFlight = false;
                    ClearOffside();
                    Stats(p.Team).SlidesWon++;
                    Stats(p.Team).Tackles++;
                    return AiMatchEvent.Tackle;
                }

                var opp = Opponents(p.Team);
                for (int i = 0; i < opp.Players.Length; i++)
                {
                    var o = opp.Players[i];
                    if (o.Body.OffPitch || o.IsGoalkeeper) continue;
                    float dx = b.Position.X - o.Body.Position.X, dy = b.Position.Y - o.Body.Position.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d > _duels.SlideContactDistance) continue;
                    // Contact before the ball: the slide stops on him, the referee whistles.
                    b.SlideRemaining = 0f;
                    b.GroundRemaining = _duels.SlideGroundSeconds;
                    b.Velocity = Vector3.Zero;
                    var f = o.Body.Facing;
                    bool behind = d > 1e-4f && (f.X * dx + f.Y * dy) / d < MathF.Cos(_referee.BehindAngleDegrees * DegToRad);
                    Stats(p.Team).SlideFouls++;
                    OnFoul(p, o, behind ? _duels.SlideBehindYellowChance : 0f);
                    return AiMatchEvent.Foul;
                }
            }
            return AiMatchEvent.None;
        }

        private static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // ---------------- second defender ----------------

        /// <summary>"Pressionar" held while the opponents have the ball: the nearest available teammate (not the controlled
        /// player) goes to win it, until the button is released.</summary>
        private void UpdateSecondDefender(in HumanInput input)
        {
            var team = HumanTeam;
            if (team == null) return;
            bool active = input.PressHeld && Restart == RestartKind.None && Ball.State == BallState.Controlled && Ball.Owner >= 0
                          && !team.Owns(Ball.Owner);
            if (!active) { team.SecondDefender = -1; return; }
            int current = team.SecondDefender;
            if (current >= 0 && current != Control.Controlled && !team.Players[current].Body.Down && !team.Players[current].Body.OffPitch) return;

            int best = -1;
            float bestSqr = _duels.SecondDefenderMaxDistance * _duels.SecondDefenderMaxDistance;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (i == Control.Controlled || q.IsGoalkeeper || q.Body.OffPitch || q.Body.Down) continue;
                float dx = q.Body.Position.X - Ball.Position.X, dy = q.Body.Position.Y - Ball.Position.Y;
                float d = dx * dx + dy * dy;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            team.SecondDefender = best;
            if (best >= 0)
            {
                var q = team.Players[best];
                SetIntention(q, AiIntention.Press);
                q.IntentTarget = new Vector3(Ball.Position.X, Ball.Position.Y, 0f);
            }
        }

        // ---------------- body duel ----------------

        /// <summary>Who takes the loose ball this step: everyone within capture range; when both sides are there the body
        /// duel decides (BodyDuelWin and closeness), otherwise the first in the rotating order. -1 if nobody.</summary>
        private int LooseBallWinner()
        {
            int n = 0;
            float total = 0f;
            bool home = false, away = false;
            for (int k = 0; k < Players.Length; k++)
            {
                int i = (int)((_step + k) % Players.Length); // rotating start: no side wins ties by index
                var p = Players[i];
                if (p.Body.OffPitch || p.Body.Down || (p.IsGoalkeeper && !KeeperCanUseFeet(KeeperOf(p.Team)))) continue;
                bool intercepting = Ball.LastTouch >= 0 && !p.Team.Owns(Ball.LastTouch);
                if (!Possession.CanCapture(p.Body, Ball, _mv, intercepting, out float d, out float radius)) continue;
                float w = _balance.Eval(Effect.BodyDuelWin, p.Setup.Attributes) * (1f + _duels.BodyClosenessWeight * (1f - d / MathF.Max(1e-3f, radius)));
                _contestants[n] = i;
                _contestWeights[n] = w;
                total += w;
                n++;
                if (p.Team == Home) home = true; else away = true;
            }
            if (n == 0) return -1;
            if (!(home && away)) return _contestants[0];
            HomeStats.BodyDuels++;
            AwayStats.BodyDuels++;
            float roll = _duelsRng.NextFloat() * total;
            for (int k = 0; k < n; k++)
            {
                roll -= _contestWeights[k];
                if (roll <= 0f) return _contestants[k];
            }
            return _contestants[n - 1];
        }

        // ---------------- cover is in IndividualLayer.Assign; runs in behind ----------------

        /// <summary>Team rate: ends a finished run; starts one when there is space behind the opponents' line and the
        /// carrier's Visão sees it (RunTriggerThreshold): the onside attacker nearest the line, ahead of the ball.</summary>
        private void UpdateRuns(AiTeam team)
        {
            if (team.Runner >= 0)
            {
                var r = team.Players[team.Runner];
                bool over = !team.HasPossession || r.RunRemaining <= 0f || r.Body.OffPitch || Restart != RestartKind.None
                            || (Ball.State == BallState.Controlled && Ball.Owner == r.Global);
                if (!over) return;
                r.RunRemaining = 0f;
                r.RunCooldown = _duels.RunCooldownSeconds;
                team.Runner = -1;
                team.RunCooldown = _duels.RunCooldownSeconds * team.Runtime.RunCooldownScale; // mentality (A9c)
            }
            if (team.RunCooldown > 0f) { team.RunCooldown = MathF.Max(0f, team.RunCooldown - _teamEvery * StepSeconds); return; }
            if (Restart != RestartKind.None || !team.HasPossession || Ball.State != BallState.Controlled || Ball.Owner < 0) return;
            var carrier = Players[Ball.Owner];
            if (!team.Owns(carrier.Global) || carrier.IsGoalkeeper) return;

            float space = Pitch.Length - team.OffsideU;
            if (space < _duels.RunMinSpaceBehind) return;
            float score = MathUtil.Clamp01(space / _duels.RunSpaceReference);
            if (score < _balance.Eval(Effect.RunTriggerThreshold, carrier.Setup.Attributes)) return;

            var f = team.Frame;
            float carrierU = f.U(carrier.Body.Position);
            int best = -1;
            float bestU = float.MinValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var q = team.Players[i];
                if (q == carrier || q.IsGoalkeeper || q.Body.OffPitch || q.Body.Down || q.RunCooldown > 0f || i == team.Receiver) continue;
                float u = f.U(q.Body.Position);
                if (u < carrierU || u > team.OffsideU - _duels.RunOnsideMargin || u < team.OffsideU - _duels.RunMaxStartBehindLine) continue;
                if (Distance2D(q.Body.Position, carrier.Body.Position) > _duels.RunMaxCarrierDistance) continue;
                if (u > bestU) { bestU = u; best = i; }
            }
            if (best < 0) return;
            var runner = team.Players[best];
            float targetU = MathF.Min(team.OffsideU + _duels.RunDepthBehindLine, Pitch.Length - _ai.SidelineMargin);
            float maxV = Pitch.HalfWidth - _ai.SidelineMargin;
            runner.RunTarget = f.World(targetU, MathUtil.Clamp(f.V(runner.Body.Position), -maxV, maxV));
            runner.RunRemaining = _duels.RunMaxSeconds;
            team.Runner = best;
            Stats(team).Runs++;
            SetIntention(runner, AiIntention.Run);
            runner.IntentTarget = runner.RunTarget;
        }

        // ---------------- keeper 1×1 ----------------

        /// <summary>Positioning keeper: rush at a carrier alone in front of goal (after a hesitation if his
        /// GkRushDecisionQuality gets the call wrong), or come for a through ball he reaches first.</summary>
        private bool ConsiderRush(Keeper k, float dt)
        {
            if (Restart != RestartKind.None) { k.RushDecided = false; return false; }
            var team = k.Player.Team;
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0 && !team.Owns(Ball.Owner))
            {
                var carrier = Players[Ball.Owner];
                if (carrier.IsGoalkeeper || !OneOnOne(k, carrier)) { k.RushDecided = false; return false; }
                if (!k.RushDecided)
                {
                    k.RushDecided = true;
                    bool right = _keeperRng.NextFloat() < _balance.Eval(Effect.GkRushDecisionQuality, k.Player.Setup.Attributes);
                    k.RushDelay = right ? 0f : _duels.RushHesitationSeconds;
                }
                k.RushDelay -= dt;
                if (k.RushDelay > 0f) return false;
                k.State = KeeperState.Rushing;
                Stats(team).KeeperRushes++;
                return Rush(k);
            }
            k.RushDecided = false;
            if (ThroughBallPoint(k, out var point))
            {
                k.State = KeeperState.Rushing;
                k.Target = point;
                Stats(team).KeeperRushes++;
                return true;
            }
            return false;
        }

        /// <summary>Rushing: keeps going at the carrier (or the through ball) while the situation lasts.</summary>
        private bool Rush(Keeper k)
        {
            var team = k.Player.Team;
            if (Restart != RestartKind.None) { k.State = KeeperState.Positioning; return false; }
            if (Ball.State == BallState.Controlled && Ball.Owner >= 0 && !team.Owns(Ball.Owner))
            {
                var carrier = Players[Ball.Owner];
                if (carrier.IsGoalkeeper || !OneOnOne(k, carrier)) { k.State = KeeperState.Positioning; return false; }
                var goal = team.Frame.OwnGoal;
                float gx = goal.X - Ball.Position.X, gy = goal.Y - Ball.Position.Y;
                float gl = MathF.Sqrt(gx * gx + gy * gy);
                var target = gl > 1e-3f ? Ball.Position + new Vector3(gx, gy, 0f) / gl * _duels.RushStopDistance : Ball.Position;
                k.Target = new Vector3(target.X, target.Y, 0f);
                return true;
            }
            if (ThroughBallPoint(k, out var point)) { k.Target = point; return true; }
            k.State = KeeperState.Positioning;
            return false;
        }

        /// <summary>The carrier is within the rush range of goal with no outfield defender nearer the goal than him.</summary>
        private bool OneOnOne(Keeper k, AiPlayer carrier)
        {
            var team = k.Player.Team;
            var goal = team.Frame.OwnGoal;
            float cd = Distance2D(carrier.Body.Position, goal);
            if (cd > _duels.RushRange) return false;
            for (int i = 0; i < team.Players.Length; i++)
            {
                var t = team.Players[i];
                if (t.IsGoalkeeper || t.Body.OffPitch || t.Body.Down) continue;
                if (Distance2D(t.Body.Position, goal) < cd) return false; // a defender nearer the goal than him
            }
            return true;
        }

        /// <summary>A rolling ball from the opponents (not a shot): the point in his area where the keeper meets it, if he
        /// gets there <see cref="DuelsDefinition.RushThroughBallMargin"/> s before every attacker.</summary>
        private bool ThroughBallPoint(Keeper k, out Vector3 point)
        {
            point = default;
            var team = k.Player.Team;
            if (Ball.State != BallState.Rolling || _pendingShot >= 0 || Ball.LastTouch < 0 || team.Owns(Ball.LastTouch)) return false;
            var kb = k.Player.Body;
            float kSpeed = _balance.Eval(Effect.SprintSpeed, k.Player.Setup.Attributes);
            point = IndividualLayer.InterceptPoint(kb.Position, kSpeed, Ball, _ballCfg.RollingFrictionDeceleration, _ai);
            if (!Pitch.InPenaltyArea(point.X, point.Y, !team.Frame.AttackingPositiveX)) return false;
            float tk = Distance2D(kb.Position, point) / kSpeed;
            var opp = Opponents(team);
            for (int i = 0; i < opp.Players.Length; i++)
            {
                var a = opp.Players[i];
                if (a.IsGoalkeeper || a.Body.OffPitch || a.Body.Down) continue;
                float s = _balance.Eval(Effect.SprintSpeed, a.Setup.Attributes);
                var pa = IndividualLayer.InterceptPoint(a.Body.Position, s, Ball, _ballCfg.RollingFrictionDeceleration, _ai);
                if (Distance2D(a.Body.Position, pa) / s <= tk + _duels.RushThroughBallMargin) return false;
            }
            return true;
        }

        /// <summary>A rushing keeper in his area wins a carrier's exposed ball (GkDiveReach × factor, the carrier's body
        /// protecting it as against a tackle) into his hands.</summary>
        private bool TrySmothers() => TrySmother(HomeKeeper) || TrySmother(AwayKeeper);

        private bool TrySmother(Keeper k)
        {
            if (k.State != KeeperState.Rushing || Restart != RestartKind.None || Ball.State != BallState.Controlled || Ball.Owner < 0) return false;
            var p = k.Player;
            if (p.Team.Owns(Ball.Owner)) return false;
            var body = p.Body;
            if (!Pitch.InPenaltyArea(body.Position.X, body.Position.Y, !p.Team.Frame.AttackingPositiveX)) return false;
            var carrier = Players[Ball.Owner];
            float toKeeper = Distance2D(body.Position, Ball.Position);
            if (toKeeper > _balance.Eval(Effect.GkDiveReach, p.Setup.Attributes) * _duels.RushSmotherReachFactor) return false;
            float shield = _balance.Eval(Effect.ShieldStrength, carrier.Setup.Attributes) * _defense.ShieldMeters;
            if (toKeeper + shield >= Distance2D(carrier.Body.Position, Ball.Position)) return false;

            carrier.Body.TackleCooldown = _defense.ReTackleCooldownSeconds;
            carrier.Body.TurnStunRemaining = MathF.Max(carrier.Body.TurnStunRemaining, _defense.DispossessedStunSeconds);
            SetIntention(carrier, AiIntention.HoldShape);
            Ball.Owner = p.Global;
            Ball.LastTouch = p.Global;
            Ball.Velocity = Vector3.Zero;
            Ball.Position = new Vector3(Ball.Position.X, Ball.Position.Y, 0f);
            k.Saves++;
            Stats(p.Team).Saves++;
            Stats(p.Team).Smothers++;
            TakeBall(p);
            return true;
        }
    }
}
