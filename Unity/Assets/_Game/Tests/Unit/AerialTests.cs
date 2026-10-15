using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A9a (ROADMAP: "Alto (lançamento, cruzamento alto), cabeceio"; TECHNICAL_SPEC §19 "cabeceio disputado pela
    /// regra"; X-73): the high ball, the header and its duel, the keeper and crosses, Alto at restarts. Ranges in
    /// Data/TestRanges/aerial.json.</summary>
    public class AerialTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "aerial.json"))));

        // ---------------- data ----------------

        [Test]
        public void AerialFile_Loads_AndRejectsUnknownKeysAndBadValues()
        {
            Assert.IsNotNull(Db().Aerial);
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "aerial.json"));
            var j = JObject.Parse(json);
            j["header"]["extra"] = 1;
            Assert.IsFalse(AerialReader.Read(j.ToString()).IsSuccess, "unknown key");
            j = JObject.Parse(json);
            j["high"]["maxDistance"] = 5.0;
            Assert.IsFalse(AerialReader.Read(j.ToString()).IsSuccess, "maxDistance below minDistance");
            j = JObject.Parse(json);
            j["ai"]["cornerCrossChance"] = 2.0;
            Assert.IsFalse(AerialReader.Read(j.ToString()).IsSuccess, "chance above 1");
        }

        [Test]
        public void TheAltoButton_TapIsAutomaticForce_HoldIsTheDistanceBar()
        {
            var t = new KickTimings(0.2f, 1f, 0.8f);
            var f = new ActionInputFilter();
            f.Step(false, false, false, true, t, Dt);
            var tap = f.Step(false, false, false, false, t, Dt);
            Assert.AreEqual(ActionKind.High, tap.Kind);
            Assert.IsTrue(tap.IsAuto);
            f.Step(false, false, false, true, t, Dt);
            for (int i = 0; i < 25; i++) f.Step(false, false, false, true, t, Dt);
            var hold = f.Step(false, false, false, false, t, Dt);
            Assert.AreEqual(ActionKind.High, hold.Kind);
            Assert.That(hold.Power, Is.EqualTo(0.5f).Within(0.05f));
        }

        // ---------------- the flight ----------------

        [Test]
        public void TheClosedFormLaunch_AgreesWithTheIntegratedBall([Values(15f, 30f, 50f)] float range, [Values(0f, 1.7f)] float height)
        {
            var db = Db();
            var cfg = db.Ball.Ball;
            var pitch = Pitch.From(db.Ball.Pitch);
            float el = db.Aerial.LongElevationDegrees * MathF.PI / 180f;
            float v = HighBallSystem.LaunchSpeed(range, height, el, cfg.Gravity, cfg.AirDragCoefficient, 60f);
            var b = new Ball { Position = new Vector3(-30f, 0f, 0f) };
            b.Kick(new Vector3(v * MathF.Cos(el), 0f, v * MathF.Sin(el)), 0f);
            float z = float.NaN;
            for (int i = 0; i < 400 && float.IsNaN(z); i++)
            {
                BallPhysics.Step(b, pitch, cfg, Dt);
                if (b.Position.X + 30f >= range) z = b.Position.Z;
            }
            Assert.That(z, Is.EqualTo(height).Within(R()["flight"]["maxIntegrationErrorMeters"].Value<float>()), $"range {range} m, v {v:F2} m/s");
        }

        [Test]
        public void AnElitePassersLancamento_LandsNearTheTargetsFeet()
        {
            var db = Db();
            var f = R()["flight"];
            int trials = f["trials"].Value<int>();
            float total = 0f;
            for (int i = 0; i < trials; i++)
            {
                var bodies = new[] { new PlayerBody { Position = new Vector3(-20f, 0f, 0f), Facing = Vector3.UnitX }, new PlayerBody { Position = new Vector3(10f, 4f, 0f) } };
                var ball = new Ball { Position = new Vector3(-19.5f, 0f, 0f), State = BallState.Rolling };
                var rng = new RngStreams((ulong)(i + 1)).Get("test");
                var outcome = HighBallSystem.Execute(0, bodies, 2, ball, Player(99), db.Balance, db.Kicking, db.Aerial, db.Ball.Ball,
                    Pitch.From(db.Ball.Pitch), true, false, new Vector2(1f, 0.13f), ActionCommand.AutoPower, false, float.PositiveInfinity,
                    float.PositiveInfinity, rng);
                Assert.AreEqual(1, outcome.Target, "the cone picks the teammate");
                Assert.AreEqual(BallState.Airborne, ball.State);
                total += Vector3.Distance(outcome.PredictedStop, new Vector3(10f, 4f, 0f));
            }
            Assert.Less(total / trials, f["eliteLandingErrorMeters"].Value<float>(), "mean landing error");
        }

        [Test]
        public void ACrossWithNobodyInTheCone_GoesIntoTheMiddleOfTheBox()
        {
            var db = Db();
            var pitch = Pitch.From(db.Ball.Pitch);
            var a = db.Aerial;
            var from = new Vector3(pitch.HalfLength - 12f, pitch.HalfWidth - 6f, 0f);
            Assert.IsTrue(HighBallSystem.InCrossZone(from, true, pitch, a));
            var bodies = new[] { new PlayerBody { Position = from, Facing = -Vector3.UnitY } };
            var ball = new Ball { Position = from + new Vector3(0f, -0.5f, 0f), State = BallState.Rolling };
            var outcome = HighBallSystem.Execute(0, bodies, 1, ball, Player(99), db.Balance, db.Kicking, a, db.Ball.Ball, pitch, true, true,
                new Vector2(0f, -1f), ActionCommand.AutoPower, false, float.PositiveInfinity, float.PositiveInfinity, new RngStreams(1).Get("x"));
            Assert.AreEqual(-1, outcome.Target);
            Assert.AreEqual(pitch.HalfLength - a.CrossDefaultDepth, outcome.IntendedPoint.X, 1e-3f);
            Assert.AreEqual(0f, outcome.IntendedPoint.Y, 1e-3f);
            Assert.Greater(ball.Velocity.Z, 0f, "lofted");
        }

        // ---------------- headers ----------------

        private static MatchSetup DuelSetup(ulong seed, int homeHeading, int awayHeading)
        {
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            MatchTeamSetup Team(int club, int h) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(_ => Player(60, overrides: new[] { (Attr.Heading, h), (Attr.Strength, h) })).ToArray(),
                new MatchPlayerSetup[0]);
            return new MatchSetup(Team(1, homeHeading), Team(2, awayHeading), false, 6, 5, seed);
        }

        /// <summary>A ball dropping at head height between a home and an away player 0.4 m either side of it.</summary>
        private static (AiMatch m, AiPlayer home, AiPlayer away) Duel(ulong seed, int homeHeading, int awayHeading, float x = 0f)
        {
            var m = new AiMatch(Db(), DuelSetup(seed, homeHeading, awayHeading), Dt);
            m.CancelRestart();
            var home = m.Home.Players.First(p => !p.IsGoalkeeper);
            var away = m.Away.Players.First(p => !p.IsGoalkeeper);
            foreach (var p in m.Players) p.Body.Position = new Vector3(p.Body.Position.X, p.Body.Position.Y + 30f * MathF.Sign(p.Body.Position.Y + 0.01f), 0f);
            home.Body.Position = new Vector3(x - 0.4f, 0f, 0f);
            away.Body.Position = new Vector3(x + 0.4f, 0f, 0f);
            m.Ball.Position = new Vector3(x, 0f, 2.0f);
            m.Ball.Kick(new Vector3(0f, 0f, -2f), 0f);
            m.Ball.LastTouch = m.Players.First(p => p.Team == m.Home && p.IsGoalkeeper).Global;
            return (m, home, away);
        }

        [Test]
        public void AnAerialDuel_IsWonMostlyByTheBetterHeader()
        {
            var d = R()["duel"];
            int trials = d["trials"].Value<int>(), strongWins = 0, contested = 0;
            for (int i = 0; i < trials; i++)
            {
                var (m, home, away) = Duel((ulong)(i + 1), d["strong"].Value<int>(), d["weak"].Value<int>());
                m.Step();
                if (m.Ball.LastTouch == home.Global) { strongWins++; contested++; }
                else if (m.Ball.LastTouch == away.Global) contested++;
            }
            Assert.AreEqual(trials, contested, "every dropping ball between them was headed by one of them");
            Assert.GreaterOrEqual((double)strongWins / trials, d["strongWinMin"].Value<double>());
            Assert.Less(strongWins, trials, "the weaker header still wins some.");
        }

        [Test]
        public void AnAttackerNearGoal_HeadsAtIt_ADefenderClearsAway()
        {
            var db = Db();
            float L = db.Ball.Pitch.Length * 0.5f;
            // Only the home player near the ball, 8 m from the away goal (+x): a header at goal.
            var (m, home, away) = Duel(1, 99, 1, L - 8f);
            away.Body.Position = new Vector3(0f, 25f, 0f);
            Assert.AreEqual(AiMatchEvent.Shot, m.Step());
            Assert.AreEqual(home.Global, m.Ball.LastTouch);
            Assert.Greater(m.Ball.Velocity.X, 0f, "towards the away goal");
            Assert.AreEqual(1, m.HomeStats.HeaderShots);

            // Only the away player, in front of his own goal (+x): a clearance away from it.
            (m, home, away) = Duel(2, 1, 99, L - 8f);
            home.Body.Position = new Vector3(0f, 25f, 0f);
            Assert.AreEqual(AiMatchEvent.Header, m.Step());
            Assert.AreEqual(away.Global, m.Ball.LastTouch);
            Assert.Less(m.Ball.Velocity.X, 0f, "away from his goal");
            Assert.Greater(m.Ball.Velocity.Z, 0f, "up and out");
        }

        [Test]
        public void TheUsersChute_HeadsAtGoal_HisPasse_ToATeammate()
        {
            float L = Db().Ball.Pitch.Length * 0.5f;
            foreach (var kind in new[] { ActionKind.Shot, ActionKind.Pass })
            {
                var (m, home, away) = Duel(3, 70, 1, 20f); // midfield: the AI would head on, not at goal
                away.Body.Position = new Vector3(0f, 25f, 0f);
                m.Ball.LastTouch = home.Global; // EnableHuman hands the user the player who touched the ball last
                m.EnableHuman(MatchSide.Home);
                Assert.AreSame(home, m.Controlled);
                var ev = m.Step(new HumanInput(new Vector2(1f, 0f), false, new ActionCommand(kind, 0.5f), false, false));
                Assert.AreEqual(home.Global, m.Ball.LastTouch, kind.ToString());
                if (kind == ActionKind.Shot) Assert.AreEqual(AiMatchEvent.Shot, ev, "CHUTE = ao gol");
                else Assert.AreEqual(AiMatchEvent.Header, ev, "PASSE = para companheiro");
            }
            Assert.Greater(L, 20f);
        }

        // ---------------- the keeper ----------------

        [Test]
        public void AKeeper_ComesForACrossDroppingInHisSixYardBox_SometimesAndClaimsIt()
        {
            int claimed = 0, came = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
                m.CancelRestart();
                var keeper = m.AwayKeeper; // away defends +x
                float L = m.Pitch.HalfLength;
                foreach (var p in m.Players) if (!p.IsGoalkeeper) p.Body.Position = new Vector3(-20f, p.Body.Position.Y, 0f);
                keeper.Player.Body.Position = new Vector3(L - 2f, 0f, 0f);
                // A cross from the right: it comes down 5 m out, in front of goal.
                var crosser = m.Home.Players.First(p => !p.IsGoalkeeper);
                m.Ball.Position = new Vector3(L - 12f, 25f, 0.2f);
                float el = 25f * MathF.PI / 180f;
                var target = new Vector3(L - 5f, 0f, 0f);
                var dir = Vector2.Normalize(new Vector2(target.X - m.Ball.Position.X, target.Y - m.Ball.Position.Y));
                float range = Vector2.Distance(new Vector2(target.X, target.Y), new Vector2(m.Ball.Position.X, m.Ball.Position.Y));
                float v = HighBallSystem.LaunchSpeed(range, 1.5f, el, Db().Ball.Ball.Gravity, Db().Ball.Ball.AirDragCoefficient, 40f);
                m.Ball.Kick(new Vector3(dir.X * MathF.Cos(el) * v, dir.Y * MathF.Cos(el) * v, MathF.Sin(el) * v), 0f);
                m.Ball.LastTouch = crosser.Global;
                bool sawClaim = false;
                for (int i = 0; i < 150; i++)
                {
                    m.Step();
                    if (keeper.State == KeeperState.Claiming) sawClaim = true;
                    if (keeper.State == KeeperState.Holding || keeper.Saves > 0) break;
                }
                if (sawClaim) came++;
                if (keeper.Saves > 0) claimed++;
            }
            Assert.Greater(came, 0, "the keeper comes for crosses in his box");
            Assert.Less(came, 30, "…not every time (GkCrossDecisionQuality)");
            Assert.Greater(claimed, 0, "and gets to some of them");
        }

        // ---------------- restarts and the AI ----------------

        [Test]
        public void TheUsersAltoAtACorner_IsACross()
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(1), Dt);
            m.EnableHuman(MatchSide.Home);
            m.CancelRestart();
            foreach (var p in m.Players) p.Body.Position = new Vector3(p.Body.Position.X * 0.3f, p.Body.Position.Y * 0.3f, 0f);
            m.Ball.Position = new Vector3(45f, 10f, 0f);
            m.Ball.Kick(new Vector3(15f, 0f, 0f), 0f);
            m.Ball.LastTouch = m.Away.Players.First(p => !p.IsGoalkeeper).Global; // away defender: corner for home
            for (int i = 0; i < 250 && m.Restart != RestartKind.Corner; i++) m.Step(HumanInput.None);
            Assert.AreEqual(RestartKind.Corner, m.Restart);
            for (int i = 0; i < 500 && !m.RestartReady; i++) m.Step(HumanInput.None);
            var ev = m.Step(new HumanInput(new Vector2(-1f, -0.6f), false, new ActionCommand(ActionKind.High, ActionCommand.AutoPower), false, false));
            Assert.AreEqual(AiMatchEvent.Pass, ev);
            Assert.AreEqual(BallState.Airborne, m.Ball.State);
            Assert.AreEqual(1, m.HomeStats.Crosses);
        }

        [Test, Category("Slow")]
        public void Headless_TheAiCrossesAndHeads()
        {
            var h = R()["headless"];
            var vs = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db()).Value;
            var r = HeadlessReport.Run(Db(), vs.Team("strong").ToSetup(), vs.Team("weak").ToSetup(), h["matches"].Value<int>(),
                h["minutes"].Value<int>(), h["seed"].Value<ulong>(), Dt);
            TestContext.WriteLine($"crosses {r.PerMatch(r.Crosses):F2}, long balls {r.PerMatch(r.LongBalls):F2}, headers {r.PerMatch(r.Headers):F2}, "
                                  + $"header shots {r.PerMatch(r.HeaderShots):F2}, header goals {r.PerMatch(r.HeaderGoals):F2}");
            void InRange(string name, double v) => Assert.That(v, Is.InRange(h[name][0].Value<double>(), h[name][1].Value<double>()), name);
            InRange("crossesPerMatch", r.PerMatch(r.Crosses));
            InRange("headersPerMatch", r.PerMatch(r.Headers));
            InRange("headerShotsPerMatch", r.PerMatch(r.HeaderShots));
        }
    }
}
