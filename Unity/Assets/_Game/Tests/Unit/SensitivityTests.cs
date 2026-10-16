using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>
    /// A9c attribute sensitivity of the played match (TEST_PLAN "sensibilidade: para cada atributo, time com +15 nele vs
    /// clone → diferença estatisticamente significativa na métrica associada"; GAME_DESIGN §26 "15+ óbvios"; X-81). Each of
    /// the 18 attributes, raised by 15 over a clone, must move the action it drives by z ≥ minZ: in full headless matches
    /// where the attribute shows in every match (speed, agility, stamina, positioning), otherwise in the action itself
    /// repeated in the match engine. Ranges and trial counts in Data/TestRanges/sensitivity.json; QuickSim has its own
    /// sensitivity test.
    /// </summary>
    [Category("Slow")]
    public class SensitivityTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "sensitivity.json"))));
        private static int Base => R()["base"].Value<int>();
        private static int Boosted => Base + R()["boost"].Value<int>();
        private static double MinZ => R()["minZ"].Value<double>();
        private static int Trials(string key) => R()["trials"][key].Value<int>();

        // ---------------- statistics ----------------

        private sealed class Sample
        {
            private double _sum, _sumSq;
            public int N { get; private set; }
            public void Add(double x) { _sum += x; _sumSq += x * x; N++; }
            public double Mean => N > 0 ? _sum / N : 0;
            public double Variance => N > 1 ? Math.Max(0, (_sumSq - _sum * _sum / N) / (N - 1)) : 0;
        }

        /// <summary>Welch z of "boosted better than clone" (higher or lower metric is better).</summary>
        private static double Z(Sample boosted, Sample clone, bool higherIsBetter)
        {
            double diff = higherIsBetter ? boosted.Mean - clone.Mean : clone.Mean - boosted.Mean;
            double se = Math.Sqrt(boosted.Variance / boosted.N + clone.Variance / clone.N);
            if (se < 1e-12) return diff > 0 ? double.PositiveInfinity : 0;
            return diff / se;
        }

        private static void Expect(Attr attr, string metric, Sample boosted, Sample clone, bool higherIsBetter)
        {
            double z = Z(boosted, clone, higherIsBetter);
            TestContext.WriteLine($"{attr} {Boosted} vs {Base}: {metric} {boosted.Mean:F4} vs {clone.Mean:F4} (n {boosted.N}/{clone.N}), z = {z:F2}");
            Assert.GreaterOrEqual(z, MinZ, $"{attr}: +{R()["boost"]} must clearly change {metric} (seed {R()["seed"]}).");
        }

        /// <summary>Paired z: the same matches, boosted side minus clone side.</summary>
        private static void ExpectPaired(Attr attr, string metric, Sample diff, bool higherIsBetter)
        {
            double mean = higherIsBetter ? diff.Mean : -diff.Mean;
            double se = Math.Sqrt(diff.Variance / diff.N);
            double z = se < 1e-12 ? (mean > 0 ? double.PositiveInfinity : 0) : mean / se;
            TestContext.WriteLine($"{attr} {Boosted} vs {Base}: {metric}, mean difference {diff.Mean:F4} over {diff.N} matches, z = {z:F2}");
            Assert.GreaterOrEqual(z, MinZ, $"{attr}: +{R()["boost"]} must clearly change {metric} (seed {R()["seed"]}).");
        }

        // ---------------- players and matches ----------------

        private static MatchPlayerSetup Player(Attr? boosted)
        {
            var a = Enumerable.Repeat(Base, AttrInfo.Count).ToArray();
            if (boosted.HasValue) a[(int)boosted.Value] = Boosted;
            return new MatchPlayerSetup(new Id(1), a, 0, new int[0], 100f, 3, null, false, 5);
        }

        private static bool IsKeeperAttr(Attr a) => a >= Attr.GkReflexes;

        /// <summary>A 4-4-2 side whose outfield players (or, for a goalkeeping attribute, keeper) have <paramref name="boosted"/> +15.</summary>
        private static MatchTeamSetup Team(int club, Attr? boosted)
        {
            var f = Db().Formation("4-4-2");
            return new MatchTeamSetup(new Id(club), new TacticSetup("4-4-2", 3, 2, 2), Enumerable.Range(0, 11).Select(i =>
            {
                bool gk = f.Slots[i].Role == FormationRole.GK;
                return Player(boosted.HasValue && IsKeeperAttr(boosted.Value) == gk ? boosted : null);
            }).ToArray(), new MatchPlayerSetup[0]);
        }

        private static MatchSetup Setup(ulong seed, Attr? home, Attr? away, int minutes = 6) =>
            new MatchSetup(Team(1, home), Team(2, away), false, minutes, 5, seed);

        /// <summary>Open play with every player parked far away except the ones the scenario places.</summary>
        private static AiMatch Open(MatchSetup setup)
        {
            var m = new AiMatch(Db(), setup, Dt);
            m.CancelRestart();
            foreach (var p in m.Players)
                p.Body.Position = new Vector3(p.Body.Position.X * 0.3f, (p.Local % 2 == 0 ? 1f : -1f) * 30f, 0f);
            return m;
        }

        private struct TeamRun { public double Distance, Energy, PositionError, KeeperAngleError; }

        /// <summary>Full headless matches, the boosted side alternating home/away; per side and match the metrics.</summary>
        private static void Matches(Attr attr, int count, out Sample[] boosted, out Sample[] clone) => Matches(attr, count, out boosted, out clone, out _);

        /// <param name="diff">Per match, boosted minus clone (same matches: a paired comparison).</param>
        private static void Matches(Attr attr, int count, out Sample[] boosted, out Sample[] clone, out Sample[] diff)
        {
            boosted = new[] { new Sample(), new Sample(), new Sample(), new Sample() };
            clone = new[] { new Sample(), new Sample(), new Sample(), new Sample() };
            diff = new[] { new Sample(), new Sample(), new Sample(), new Sample() };
            int minutes = R()["matches"]["minutes"].Value<int>();
            ulong seed0 = R()["seed"].Value<ulong>();
            for (int s = 0; s < count; s++)
            {
                bool home = s % 2 == 0;
                var m = new AiMatch(Db(), Setup(seed0 + (ulong)s, home ? attr : (Attr?)null, home ? (Attr?)null : attr, minutes), Dt);
                var b = home ? m.Home : m.Away;
                var runs = Run(m);
                var rb = runs[b == m.Home ? 0 : 1];
                var rc = runs[b == m.Home ? 1 : 0];
                Add(boosted, rb);
                Add(clone, rc);
                Add(diff, new TeamRun { Distance = rb.Distance - rc.Distance, Energy = rb.Energy - rc.Energy,
                    PositionError = rb.PositionError - rc.PositionError, KeeperAngleError = rb.KeeperAngleError - rc.KeeperAngleError });
            }
        }

        private static void Add(Sample[] s, TeamRun r)
        {
            s[0].Add(r.Distance);
            s[1].Add(r.Energy);
            s[2].Add(r.PositionError);
            s[3].Add(r.KeeperAngleError);
        }

        private static TeamRun[] Run(AiMatch m)
        {
            var runs = new TeamRun[2];
            var last = m.Players.Select(p => p.Body.Position).ToArray();
            int samples = 0, step = 0;
            while (!m.Finished)
            {
                m.Step();
                for (int i = 0; i < m.Players.Length; i++)
                {
                    var p = m.Players[i];
                    int t = p.Team == m.Home ? 0 : 1;
                    runs[t].Distance += Vector3.Distance(p.Body.Position, last[i]);
                    last[i] = p.Body.Position;
                }
                if (++step % 25 != 0) continue;
                samples++;
                for (int t = 0; t < 2; t++)
                {
                    var team = t == 0 ? m.Home : m.Away;
                    runs[t].PositionError += team.Players.Where(p => !p.IsGoalkeeper).Average(p => Vector3.Distance(p.RoleTarget, p.IdealTarget));
                    runs[t].KeeperAngleError += Math.Abs(m.KeeperOf(team).AngleErrorDegrees);
                }
            }
            for (int t = 0; t < 2; t++)
            {
                var team = t == 0 ? m.Home : m.Away;
                runs[t].Energy = team.Players.Average(p => p.Body.Energy);
                runs[t].PositionError /= samples;
                runs[t].KeeperAngleError /= samples;
            }
            return runs;
        }

        // ---------------- physical (full matches) ----------------

        [Test]
        public void Speed_RunsMoreGround()
        {
            Matches(Attr.Speed, R()["matches"]["speed"].Value<int>(), out _, out _, out var d);
            ExpectPaired(Attr.Speed, "distance covered per match (m)", d[0], true);
        }

        [Test]
        public void Agility_RunsMoreGround()
        {
            Matches(Attr.Agility, R()["matches"]["agility"].Value<int>(), out _, out _, out var d);
            ExpectPaired(Attr.Agility, "distance covered per match (m)", d[0], true);
        }

        [Test]
        public void Stamina_EndsWithMoreEnergy()
        {
            Matches(Attr.Stamina, R()["matches"]["stamina"].Value<int>(), out var b, out var c);
            Expect(Attr.Stamina, "energy at the end", b[1], c[1], true);
        }

        [Test]
        public void Positioning_StandsCloserToTheIdealSpot()
        {
            Matches(Attr.Positioning, R()["matches"]["positioning"].Value<int>(), out var b, out var c);
            Expect(Attr.Positioning, "distance from the ideal position (m)", b[2], c[2], false);
        }

        [Test]
        public void GkPositioning_KeepsASmallerAngleError()
        {
            Matches(Attr.GkPositioning, Trials("gkPositioningMatches"), out var b, out var c);
            Expect(Attr.GkPositioning, "keeper angle error (deg)", b[3], c[3], false);
        }

        // ---------------- duels ----------------

        [Test]
        public void Strength_WinsLooseBallsBetweenTwo()
        {
            var b = new Sample();
            var c = new Sample();
            int trials = Trials("strength");
            for (int s = 0; s < trials; s++)
            {
                var m = Open(Setup((ulong)(s + 1), Attr.Strength, null));
                var h = m.Home.Players[1];
                var a = m.Away.Players[1];
                float side = s % 2 == 0 ? 1f : -1f; // no side of the ball favoured
                h.Body.Position = new Vector3(-0.6f * side, 0f, 0f);
                a.Body.Position = new Vector3(0.6f * side, 0f, 0f);
                m.Ball.Position = Vector3.Zero;
                m.Ball.Velocity = Vector3.Zero;
                m.Ball.State = BallState.Rolling;
                m.Ball.Owner = Ball.NoPlayer;
                m.Ball.LastTouch = m.HomeKeeper.Player.Global;
                m.Step();
                if (m.Ball.Owner != h.Global && m.Ball.Owner != a.Global) continue;
                b.Add(m.Ball.Owner == h.Global ? 1 : 0);
                c.Add(m.Ball.Owner == a.Global ? 1 : 0);
            }
            Expect(Attr.Strength, "share of loose balls won", b, c, true);
        }

        [Test]
        public void Heading_WinsAerialDuels()
        {
            var b = new Sample();
            var c = new Sample();
            int trials = Trials("heading");
            for (int s = 0; s < trials; s++)
            {
                var m = Open(Setup((ulong)(s + 1), Attr.Heading, null));
                var h = m.Home.Players[1];
                var a = m.Away.Players[1];
                float side = s % 2 == 0 ? 1f : -1f;
                h.Body.Position = new Vector3(-0.4f * side, 0f, 0f);
                a.Body.Position = new Vector3(0.4f * side, 0f, 0f);
                m.Ball.Position = new Vector3(0f, 0f, 2.0f);
                m.Ball.Kick(new Vector3(0f, 0f, -2f), 0f);
                m.Ball.LastTouch = m.HomeKeeper.Player.Global;
                m.Step();
                if (m.Ball.LastTouch != h.Global && m.Ball.LastTouch != a.Global) continue;
                b.Add(m.Ball.LastTouch == h.Global ? 1 : 0);
                c.Add(m.Ball.LastTouch == a.Global ? 1 : 0);
            }
            Expect(Attr.Heading, "share of aerial duels won", b, c, true);
        }

        [Test]
        public void Tackling_WinsMoreBallsWhenTheyAreThere()
        {
            var db = Db();
            var rng = new RngStreams(R()["seed"].Value<ulong>()).Get("Sensitivity.Tackling");
            var b = new Sample();
            var c = new Sample();
            var good = Player(Attr.Tackling);
            var plain = Player(null);
            var carrierSetup = Player(null);
            int trials = Trials("tackling");
            for (int s = 0; s < trials; s++)
            {
                // A random approach to a ball the carrier pushed ahead: the defender 0.6-1.3 m from it, the carrier 1.4-2.0 m behind.
                float dd = 0.6f + rng.NextFloat() * 0.7f, cd = 1.4f + rng.NextFloat() * 0.6f, angle = (rng.NextFloat() - 0.5f) * 2f;
                var ball = new Ball { Position = Vector3.Zero, State = BallState.Controlled, Owner = 1 };
                var carrier = new PlayerBody { Position = new Vector3(-cd, 0f, 0f) };
                var defender = new PlayerBody { Position = new Vector3(dd * MathF.Cos(angle), dd * MathF.Sin(angle), 0f) };
                b.Add(DefenseSystem.BallExposedTo(defender, good, carrier, carrierSetup, ball, db.Balance, db.Defense) ? 1 : 0);
                c.Add(DefenseSystem.BallExposedTo(defender, plain, carrier, carrierSetup, ball, db.Balance, db.Defense) ? 1 : 0);
            }
            Expect(Attr.Tackling, "share of approaches that win the ball", b, c, true);
        }

        // ---------------- on the ball ----------------

        private static Sample PassErrors(MatchPlayerSetup player, PassKind kind, int samples, float pressure, string stream)
        {
            var db = Db();
            var rng = new RngStreams(R()["seed"].Value<ulong>()).Get(stream);
            var bodies = new[] { new PlayerBody { Position = Vector3.Zero, Facing = Vector3.UnitX }, new PlayerBody { Position = new Vector3(20f, 0f, 0f) } };
            var errors = new Sample();
            for (int i = 0; i < samples; i++)
            {
                bodies[0].IgnoreBallUntilClear = false;
                var ball = new Ball { Position = new Vector3(0.6f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
                var o = PassSystem.Execute(0, bodies, 2, ball, player, db.Balance, db.Kicking, db.Ball.Ball, kind, Vector2.UnitX,
                    ActionCommand.AutoPower, false, pressure, Vector2.UnitX, rng);
                errors.Add(Math.Abs(o.AngleErrorDegrees));
            }
            return errors;
        }

        [Test]
        public void Passing_PassesMoreAccurately()
        {
            int n = Trials("passing");
            Expect(Attr.Passing, "pass direction error (deg)", PassErrors(Player(Attr.Passing), PassKind.Ground, n, float.PositiveInfinity, "a"),
                PassErrors(Player(null), PassKind.Ground, n, float.PositiveInfinity, "b"), false);
        }

        [Test]
        public void Vision_PlaysMoreAccurateThroughBalls()
        {
            int n = Trials("vision");
            Expect(Attr.Vision, "through-ball direction error (deg)", PassErrors(Player(Attr.Vision), PassKind.Through, n, float.PositiveInfinity, "a"),
                PassErrors(Player(null), PassKind.Through, n, float.PositiveInfinity, "b"), false);
        }

        [Test]
        public void Composure_KeepsPassesAccurateUnderPressure()
        {
            int n = Trials("composure");
            float pressed = Db().Ai.ReceiverPressureRadius * 0.3f; // an opponent right on him
            Expect(Attr.Composure, "pass direction error under pressure (deg)", PassErrors(Player(Attr.Composure), PassKind.Ground, n, pressed, "a"),
                PassErrors(Player(null), PassKind.Ground, n, pressed, "b"), false);
        }

        private static Sample CrossErrors(MatchPlayerSetup player, int samples, string stream)
        {
            var db = Db();
            var pitch = Pitch.From(db.Ball.Pitch);
            var rng = new RngStreams(R()["seed"].Value<ulong>()).Get(stream);
            var from = new Vector3(pitch.HalfLength - 12f, pitch.HalfWidth - 6f, 0f);
            var errors = new Sample();
            for (int i = 0; i < samples; i++)
            {
                var bodies = new[] { new PlayerBody { Position = from, Facing = -Vector3.UnitY } };
                var ball = new Ball { Position = from + new Vector3(0f, -0.5f, 0f), State = BallState.Rolling };
                var o = HighBallSystem.Execute(0, bodies, 1, ball, player, db.Balance, db.Kicking, db.Aerial, db.Ball.Ball, pitch, true, true,
                    new Vector2(0f, -1f), ActionCommand.AutoPower, false, float.PositiveInfinity, float.PositiveInfinity, rng);
                errors.Add(Math.Abs(o.AngleErrorDegrees));
            }
            return errors;
        }

        [Test]
        public void Crossing_CrossesMoreAccurately()
        {
            int n = Trials("crossing");
            Expect(Attr.Crossing, "cross direction error (deg)", CrossErrors(Player(Attr.Crossing), n, "a"), CrossErrors(Player(null), n, "b"), false);
        }

        private static Sample ShotErrors(MatchPlayerSetup player, float distance, int samples, string stream)
        {
            var db = Db();
            var pitch = Pitch.From(db.Ball.Pitch);
            var rng = new RngStreams(R()["seed"].Value<ulong>()).Get(stream);
            var errors = new Sample();
            for (int i = 0; i < samples; i++)
            {
                var body = new PlayerBody { Position = new Vector3(pitch.HalfLength - distance - 0.6f, 0f, 0f), Facing = Vector3.UnitX };
                var ball = new Ball { Position = new Vector3(pitch.HalfLength - distance, 0f, 0f), State = BallState.Controlled, Owner = 0 };
                var o = ShotSystem.Execute(0, body, ball, player, db.Balance, db.Kicking, db.Ball.Ball, pitch, true, Vector2.Zero, 0.7f, false,
                    float.PositiveInfinity, rng);
                errors.Add(Math.Abs(o.HorizontalErrorDegrees));
            }
            return errors;
        }

        [Test]
        public void Finishing_ShootsMoreAccuratelyInTheBox()
        {
            int n = Trials("finishing");
            Expect(Attr.Finishing, "shot direction error in the box (deg)", ShotErrors(Player(Attr.Finishing), 10f, n, "a"),
                ShotErrors(Player(null), 10f, n, "b"), false);
        }

        [Test]
        public void LongShots_ShootMoreAccuratelyFromDistance()
        {
            int n = Trials("longShots");
            Expect(Attr.LongShots, "shot direction error from 25 m (deg)", ShotErrors(Player(Attr.LongShots), 25f, n, "a"),
                ShotErrors(Player(null), 25f, n, "b"), false);
        }

        private static Sample DribbleExposure(MatchPlayerSetup player)
        {
            var db = Db();
            var body = new PlayerBody { Facing = Vector3.UnitX };
            var ball = new Ball { Position = new Vector3(0.6f, 0f, 0f), State = BallState.Controlled, Owner = 0 };
            var exposure = new Sample();
            int steps = Trials("dribblingSteps");
            for (int i = 0; i < steps; i++)
            {
                // A weave: a change of direction every second, as a carrier beating a man.
                var move = (i / 50) % 2 == 0 ? new Vector2(1f, 0.4f) : new Vector2(1f, -0.4f);
                Movement.Step(body, db.Balance, player, Vector2.Normalize(move), false, true, false, db.Movement, db.Fatigue, Dt);
                DribbleSystem.Step(body, ball, db.Balance, player, db.Movement);
                if (i >= 50) exposure.Add(Vector2.Distance(new Vector2(ball.Position.X, ball.Position.Y), new Vector2(body.Position.X, body.Position.Y)));
            }
            return exposure;
        }

        [Test]
        public void Dribbling_KeepsTheBallCloser()
        {
            Expect(Attr.Dribbling, "ball distance from the body while dribbling (m)", DribbleExposure(Player(Attr.Dribbling)), DribbleExposure(Player(null)), false);
        }

        // ---------------- goalkeepers ----------------

        /// <summary>Shots at the away keeper (+x goal) from 16 m; per shot: saved, and caught. Reflexes: hard low shots across
        /// him, aimed between 2.0 and 3.3 m from the centre (the edge of his reach). Handling: central shots at chest height.</summary>
        private static void KeeperShots(Attr? keeperAttr, int count, Sample saved, Sample caught, bool central)
        {
            var db = Db();
            float goalX = db.Ball.Pitch.Length * 0.5f;
            for (int s = 0; s < count; s++)
            {
                var m = Open(Setup((ulong)(s + 1), null, keeperAttr));
                var k = m.AwayKeeper;
                float sideSign = s % 2 == 0 ? 1f : -1f;
                var from = new Vector3(goalX - 16f, central ? 0f : sideSign * 3f, 0f);
                k.AngleErrorDegrees = 0f;
                k.Player.Body.Position = Goalkeeper.PositionTarget(k, from, m.Pitch, db.Goalkeeper);
                k.Player.Body.Velocity = Vector3.Zero;
                k.Reset();
                var aim = central ? new Vector3(goalX, 0f, 1.0f) : new Vector3(goalX, -sideSign * (2.0f + (s % 13) * 0.11f), 0.6f);
                var flat = new Vector2(aim.X - from.X, aim.Y - from.Y);
                float speed = central ? 18f : 24f, el = ShotSystem.LaunchElevation(speed, flat.Length(), aim.Z, db.Ball.Ball.Gravity);
                var dir = Vector2.Normalize(flat);
                m.Ball.Position = from;
                m.Ball.Kick(new Vector3(dir.X * MathF.Cos(el), dir.Y * MathF.Cos(el), MathF.Sin(el)) * speed, 0f);
                m.Ball.LastTouch = m.Home.Players[10].Global;
                foreach (var p in m.Players) p.Body.IgnoreBallUntilClear = false;
                bool save = false;
                for (int i = 0; i < 150; i++)
                {
                    var ev = m.Step();
                    if (ev == AiMatchEvent.Save || (ev == AiMatchEvent.Captured && m.Ball.Owner == k.Player.Global)) { save = true; break; }
                    if (ev == AiMatchEvent.Goal || ev == AiMatchEvent.Out || ev == AiMatchEvent.Captured) break;
                }
                saved.Add(save ? 1 : 0);
                if (save) caught.Add(k.Catches > 0 || m.Ball.Owner == k.Player.Global ? 1 : 0);
            }
        }

        [Test]
        public void GkReflexes_SavesMoreShots()
        {
            var bs = new Sample(); var cs = new Sample();
            KeeperShots(Attr.GkReflexes, Trials("gkReflexes"), bs, new Sample(), false);
            KeeperShots(null, Trials("gkReflexes"), cs, new Sample(), false);
            Expect(Attr.GkReflexes, "share of shots saved", bs, cs, true);
        }

        [Test]
        public void GkHandling_CatchesMoreOfWhatHeSaves()
        {
            var bc = new Sample(); var cc = new Sample();
            KeeperShots(Attr.GkHandling, Trials("gkHandling"), new Sample(), bc, true);
            KeeperShots(null, Trials("gkHandling"), new Sample(), cc, true);
            Expect(Attr.GkHandling, "share of saves caught (not parried)", bc, cc, true);
        }

        /// <summary>A cross dropping 5 m out in front of the away keeper's goal; per cross: did he come for it.</summary>
        private static Sample CrossClaims(Attr? keeperAttr, int count)
        {
            var db = Db();
            var came = new Sample();
            for (int s = 0; s < count; s++)
            {
                var m = new AiMatch(db, Setup((ulong)(s + 1), null, keeperAttr), Dt);
                m.CancelRestart();
                var keeper = m.AwayKeeper;
                float L = m.Pitch.HalfLength;
                foreach (var p in m.Players) if (!p.IsGoalkeeper) p.Body.Position = new Vector3(-20f, p.Body.Position.Y, 0f);
                keeper.Player.Body.Position = new Vector3(L - 2f, 0f, 0f);
                m.Ball.Position = new Vector3(L - 12f, 25f, 0.2f);
                float el = 25f * MathF.PI / 180f;
                var target = new Vector3(L - 5f + (s % 3), (s % 5 - 2) * 0.8f, 0f);
                var dir = Vector2.Normalize(new Vector2(target.X - m.Ball.Position.X, target.Y - m.Ball.Position.Y));
                float range = Vector2.Distance(new Vector2(target.X, target.Y), new Vector2(m.Ball.Position.X, m.Ball.Position.Y));
                float v = HighBallSystem.LaunchSpeed(range, 1.5f, el, db.Ball.Ball.Gravity, db.Ball.Ball.AirDragCoefficient, 40f);
                m.Ball.Kick(new Vector3(dir.X * MathF.Cos(el) * v, dir.Y * MathF.Cos(el) * v, MathF.Sin(el) * v), 0f);
                m.Ball.LastTouch = m.Home.Players[2].Global;
                bool claimed = false;
                for (int i = 0; i < 150 && !claimed; i++)
                {
                    m.Step();
                    claimed = keeper.State == KeeperState.Claiming;
                    if (keeper.State == KeeperState.Holding) break;
                }
                came.Add(claimed ? 1 : 0);
            }
            return came;
        }

        [Test]
        public void GkAerial_ComesForMoreCrosses()
        {
            int n = Trials("gkAerial");
            Expect(Attr.GkAerial, "share of crosses in his box he comes for", CrossClaims(Attr.GkAerial, n), CrossClaims(null, n), true);
        }

        [Test]
        public void EveryAttribute_HasASensitivityTest()
        {
            var names = typeof(SensitivityTests).GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), false).Length > 0).Select(m => m.Name).ToArray();
            foreach (Attr a in Enum.GetValues(typeof(Attr)))
                Assert.IsTrue(names.Any(n => n.StartsWith(a + "_", StringComparison.Ordinal)), $"no sensitivity test for {a}");
        }
    }
}
