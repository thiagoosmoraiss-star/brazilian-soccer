using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A8a (ROADMAP: "faltas, cartões, impedimento, cobranças de falta com barreira, pênalti"; X-64/X-65): the
    /// referee in the played match. Ranges in Data/TestRanges/referee.json.</summary>
    public class RefereeTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "referee.json"))));

        /// <summary>The real data with one file edited by <paramref name="edit"/>.</summary>
        private sealed class EditedData : IDataSource
        {
            private readonly DirectoryDataSource _inner = new DirectoryDataSource(TestPaths.DataRoot());
            private readonly string _file, _json;
            public EditedData(string file, Action<JObject> edit)
            {
                var j = JObject.Parse(_inner.ReadAllText(file));
                edit(j);
                _file = file;
                _json = j.ToString();
            }
            public string Description => "edited " + _file;
            public bool Exists(string relativePath) => _inner.Exists(relativePath);
            public string ReadAllText(string relativePath) => relativePath == _file ? _json : _inner.ReadAllText(relativePath);
        }

        private static GameDatabase _strict;
        /// <summary>A referee who books nearly every foul: second yellows and dismissals in every match.</summary>
        private static GameDatabase StrictDb()
        {
            if (_strict != null) return _strict;
            var r = GameDataLoader.Load(new EditedData(GameDataLoader.MatchRulesFile,
                j => j["discipline"]["yellowPerFoul"] = R()["strictReferee"]["yellowPerFoul"]));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return _strict = r.Value;
        }

        // ---------------- data ----------------

        [Test]
        public void RefereeFile_Loads_AndRejectsUnknownKeysAndBadValues()
        {
            Assert.IsNotNull(Db().Referee);
            Assert.AreEqual(4, Db().Referee.WallSize(15f));
            Assert.AreEqual(0, Db().Referee.WallSize(Db().Referee.WallMaxDistance.Last() + 1f));
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "referee.json"));
            var j = JObject.Parse(json);
            j["foul"]["extra"] = 1;
            Assert.IsFalse(RefereeReader.Read(j.ToString()).IsSuccess, "unknown key");
            j = JObject.Parse(json);
            j["penalty"]["keeperStayChance"] = 1.5;
            Assert.IsFalse(RefereeReader.Read(j.ToString()).IsSuccess, "chance above 1");
            j = JObject.Parse(json);
            j["freeKick"]["wall"][1]["maxDistance"] = 10.0;
            Assert.IsFalse(RefereeReader.Read(j.ToString()).IsSuccess, "wall rows out of order");
        }

        // ---------------- offside ----------------

        /// <summary>Home carrier 40 m from the away goal, away defenders in a line 30 m out (wide, the lane open), the
        /// home striker <paramref name="strikerFromGoal"/> m out; the user passes to him.</summary>
        private static (AiMatch m, AiPlayer striker) OffsideScenario(float strikerFromGoal, ulong seed = 1)
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
            m.EnableHuman(MatchSide.Home);
            var f = m.Home.Frame;
            float L = m.Pitch.Length;
            var home = m.Home.Players.Where(p => !p.IsGoalkeeper).ToArray();
            var carrier = home[0];
            var striker = home[1];
            for (int i = 2; i < home.Length; i++) Place(home[i], f.World(L - 50f - 2f * i, -20f + 4f * i));
            Place(carrier, f.World(L - 40f, 0f));
            Place(striker, f.World(L - strikerFromGoal, 0f));
            var away = m.Away.Players.Where(p => !p.IsGoalkeeper).ToArray();
            for (int i = 0; i < away.Length; i++) Place(away[i], f.World(L - 30f - (i % 3), (i % 2 == 0 ? 1f : -1f) * (14f + 2f * i)));
            Place(m.AwayKeeper.Player, f.World(L - 1f, 0f));
            m.PlaceBall(carrier);
            Own(m, carrier);
            return (m, striker);
        }

        /// <summary>Steps (no input) until <paramref name="p"/> controls the ball.</summary>
        private static void Own(AiMatch m, AiPlayer p)
        {
            for (int i = 0; i < 50 && m.Ball.Owner != p.Global; i++) m.Step(HumanInput.None);
            Assert.AreEqual(p.Global, m.Ball.Owner, "the carrier never took the ball.");
        }

        private static void Place(AiPlayer p, Vector3 at)
        {
            p.Body.Position = at;
            p.Body.Velocity = Vector3.Zero;
        }

        [Test]
        public void APassToAPlayerBeyondTheSecondLastDefender_IsOffside_AnIndirectFreeKickForTheDefence()
        {
            var (m, striker) = OffsideScenario(15f);
            var forward = m.Home.Frame.Forward;
            var pass = new HumanInput(forward, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false);
            Assert.AreEqual(AiMatchEvent.Pass, m.Step(pass));
            Assert.IsTrue(m.InOffsidePosition(striker), "judged when the ball is played.");
            var ev = AiMatchEvent.None;
            for (int i = 0; i < 250 && ev != AiMatchEvent.Offside; i++) ev = m.Step(HumanInput.None);
            Assert.AreEqual(AiMatchEvent.Offside, ev);
            Assert.AreEqual(RestartKind.FreeKick, m.Restart);
            Assert.AreSame(m.Away, m.RestartTeam);
            Assert.AreEqual(1, m.HomeStats.Offsides);
        }

        [Test]
        public void APlayerLevelWithOrBehindTheLine_IsOnside()
        {
            var (m, striker) = OffsideScenario(32f);
            var pass = new HumanInput(m.Home.Frame.Forward, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false);
            Assert.AreEqual(AiMatchEvent.Pass, m.Step(pass));
            Assert.IsFalse(m.InOffsidePosition(striker));
            for (int i = 0; i < 250; i++) Assert.AreNotEqual(AiMatchEvent.Offside, m.Step(HumanInput.None));
            Assert.AreEqual(0, m.HomeStats.Offsides);
        }

        [Test]
        public void ARunnerJustOnsideAsTheBallIsPlayed_IsSometimesFlagged_AStandingOneNever()
        {
            var r = Db().Referee;
            int flagged = 0, trials = 40;
            for (ulong seed = 1; seed <= (ulong)trials; seed++)
            {
                foreach (bool running in new[] { true, false })
                {
                    var (m, striker) = OffsideScenario(30f + r.CloseCallWindow * 0.5f, seed); // inside the close-call window
                    var fwd = m.Home.Frame.Forward;
                    striker.Body.Velocity = running ? new Vector3(fwd * (r.CloseCallRunSpeed + 3f), 0f) : Vector3.Zero;
                    var pass = new HumanInput(fwd, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false);
                    Assert.AreEqual(AiMatchEvent.Pass, m.Step(pass), $"seed {seed}");
                    if (!running) Assert.IsFalse(m.InOffsidePosition(striker), $"seed {seed}: a standing player behind the line is onside.");
                    else if (m.InOffsidePosition(striker)) flagged++;
                }
            }
            Assert.Greater(flagged, 0, "a runner on the line is sometimes caught a fraction early.");
            Assert.Less(flagged, trials, "…but not always.");
        }

        // ---------------- fouls ----------------

        [Test]
        public void FoulChance_IsHigherFromBehind_AndAtSpeed()
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(1), Dt);
            var carrier = m.Home.Players.First(p => !p.IsGoalkeeper);
            var defender = m.Away.Players.First(p => !p.IsGoalkeeper);
            carrier.Body.Facing = new Vector3(1f, 0f, 0f);
            float min = Db().Referee.MinClosingSpeed;
            float front = m.FoulChance(defender, carrier, min, 1f, 0f);
            float behind = m.FoulChance(defender, carrier, min, -1f, 0f);
            float fast = m.FoulChance(defender, carrier, min + Db().Referee.SpeedReference, 1f, 0f);
            Assert.Greater(front, 0f);
            Assert.AreEqual(front * Db().Referee.BehindMultiplier, behind, 1e-5f);
            Assert.AreEqual(front * (1f + Db().Referee.SpeedMultiplier), fast, 1e-5f);
        }

        /// <summary>The user's carrier stands still; an away defender 1.5 m away runs into him.</summary>
        private static (AiMatch m, AiPlayer carrier, AiPlayer defender) Contact(GameDatabase db, ulong seed, float defenderSpeed, bool fromBehind)
        {
            var m = new AiMatch(db, AiMatchTests.Setup(seed), Dt);
            m.EnableHuman(MatchSide.Home);
            var f = m.Home.Frame;
            var carrier = m.Home.Players.First(p => !p.IsGoalkeeper);
            var defender = m.Away.Players.First(p => !p.IsGoalkeeper);
            Place(carrier, f.World(40f, 0f));
            carrier.Body.Facing = new Vector3(f.Forward, 0f);
            m.PlaceBall(carrier);
            Own(m, carrier);
            // Past the new carrier's secure window (no tackle, no challenge).
            for (int i = 0; i * Dt < db.Defense.PossessionSecureSeconds + Dt; i++) m.Step(HumanInput.None);
            float side = fromBehind ? -1f : 1f;
            Place(defender, f.World(40f + side * 1.5f, 0f));
            defender.Body.Velocity = new Vector3(-side * defenderSpeed * f.Forward, 0f);
            defender.Body.Facing = new Vector3(-side * f.Forward, 0f);
            return (m, carrier, defender);
        }

        [Test]
        public void ARunIntoTheCarrier_IsSometimesAFoul_AndTheFouledSideGetsTheFreeKick()
        {
            int fouls = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var (m, carrier, defender) = Contact(Db(), seed, 6f, fromBehind: true);
                for (int i = 0; i < 25; i++)
                {
                    if (m.Step(HumanInput.None) != AiMatchEvent.Foul) continue;
                    fouls++;
                    Assert.AreEqual(1, m.AwayStats.Fouls, $"seed {seed}");
                    Assert.AreEqual(1, m.Players.Sum(p => m.Fouls(p)), $"seed {seed}");
                    Assert.That(m.Restart, Is.EqualTo(RestartKind.DirectFreeKick).Or.EqualTo(RestartKind.Penalty), $"seed {seed}");
                    Assert.AreSame(m.Home, m.RestartTeam, $"seed {seed}");
                    Assert.AreEqual(1, m.Events.Count(e => e.Type == MatchEventType.Foul), $"seed {seed}");
                    break;
                }
            }
            Assert.Greater(fouls, 0, "a defender running into the carrier from behind is sometimes whistled.");
            Assert.Less(fouls, 60, "…but not always.");
        }

        [Test]
        public void AJockeyingDefender_NeverFouls()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var (m, carrier, defender) = Contact(StrictDb(), seed, 0f, fromBehind: false);
                for (int i = 0; i < 10; i++) Assert.AreNotEqual(AiMatchEvent.Foul, m.Step(HumanInput.None), $"seed {seed}, step {i}");
            }
        }

        // ---------------- cards ----------------

        [Test]
        public void Cards_AddUp_TwoYellowsAreARed_AndASideKeepsItsMinimum()
        {
            int reds = 0, secondYellows = 0, minimum = StrictDb().Referee.MinPlayersOnPitch;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var m = new AiMatch(StrictDb(), AiMatchTests.Setup(seed), Dt);
                while (!m.Finished)
                {
                    m.Step();
                    Assert.GreaterOrEqual(m.PlayersOnPitch(m.Home), minimum, $"seed {seed}");
                    Assert.GreaterOrEqual(m.PlayersOnPitch(m.Away), minimum, $"seed {seed}");
                }
                var r = m.Result();
                foreach (var team in new[] { m.Home, m.Away })
                {
                    var s = team == m.Home ? r.HomeStats : r.AwayStats;
                    var mine = team.Players;
                    Assert.AreEqual(s.Fouls, mine.Sum(p => m.Fouls(p)), $"seed {seed}");
                    Assert.AreEqual(s.YellowCards, mine.Sum(p => m.Yellows(p)), $"seed {seed}");
                    Assert.AreEqual(s.RedCards, mine.Count(p => m.SentOff(p)), $"seed {seed}");
                    foreach (var p in mine)
                    {
                        if (m.Yellows(p) >= 2) secondYellows++;
                        // Two yellows send a player off unless his side is already at the minimum.
                        if (m.Yellows(p) >= 2 && !m.SentOff(p)) Assert.AreEqual(minimum, m.PlayersOnPitch(team), $"seed {seed}");
                        if (!m.SentOff(p)) continue;
                        reds++;
                        Assert.IsTrue(p.Body.OffPitch);
                        Assert.Greater(Math.Abs(p.Body.Position.Y), m.Pitch.HalfWidth, "a dismissed player waits off the pitch.");
                        var stats = r.PlayerStats[p.Global]; // Result() lists the players in match order
                        Assert.IsTrue(stats.RedCard);
                        Assert.Less(stats.MinutesPlayed, r.PlayerStats.Max(x => x.MinutesPlayed) + 1);
                    }
                }
                Assert.AreEqual(r.HomeStats.RedCards + r.AwayStats.RedCards, r.Events.Count(e => e.Type == MatchEventType.RedCard), $"seed {seed}");
            }
            Assert.Greater(reds, 0, "the strict referee sends players off.");
            Assert.Greater(secondYellows, 0, "…including for a second yellow.");
        }

        // ---------------- set pieces ----------------

        /// <summary>Steps until the shot's outcome (goal, save, out) or <paramref name="steps"/>.</summary>
        private static AiMatchEvent ShotOutcome(AiMatch m, int steps)
        {
            bool shot = false;
            for (int s = 0; s < steps; s++)
            {
                var ev = m.Step();
                if (ev == AiMatchEvent.Shot) { shot = true; continue; }
                if (shot && (ev == AiMatchEvent.Goal || ev == AiMatchEvent.Save || ev == AiMatchEvent.Out)) return ev;
            }
            return AiMatchEvent.None;
        }

        [Test]
        public void APenalty_IsTakenByTheBestTaker_WithTheAreaClear_AndConvertsInRange()
        {
            var p = R()["penalty"];
            int n = p["trials"].Value<int>(), goals = 0;
            for (int i = 0; i < n; i++)
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup((ulong)(i + 1)), Dt);
                m.CancelRestart();
                m.AwardPenalty(MatchSide.Home);
                Assert.AreEqual(RestartKind.Penalty, m.Restart);
                Assert.AreEqual(1, m.HomeStats.Penalties);
                int steps = 0;
                while (!m.RestartReady && steps++ < 500) m.Step();
                Assert.IsTrue(m.RestartReady || m.Restart == RestartKind.None, $"trial {i}: the penalty never got ready.");
                if (m.RestartReady)
                {
                    var attack = m.Home.Frame;
                    float line = m.Pitch.Length - m.Pitch.PenaltyAreaDepth;
                    foreach (var q in m.Players)
                        if (q != m.Taker && q != m.AwayKeeper.Player)
                            Assert.LessOrEqual(attack.U(q.Body.Position), line + 1e-3f, $"trial {i}: player {q.Global} inside the area.");
                }
                if (ShotOutcome(m, 500) == AiMatchEvent.Goal) goals++;
            }
            double rate = (double)goals / n;
            TestContext.WriteLine($"penalties {n}, scored {rate:P0}");
            Assert.That(rate, Is.InRange(p["conversion"][0].Value<double>(), p["conversion"][1].Value<double>()));
        }

        [Test]
        public void ADirectFreeKickNearGoal_GetsAWall_AndIsShotAtWithoutStalling()
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(2), Dt);
            m.CancelRestart();
            var f = m.Home.Frame;
            var spot = f.World(m.Pitch.Length - 20f, 0f);
            m.AwardFreeKick(MatchSide.Home, spot, direct: true);
            Assert.AreEqual(RestartKind.DirectFreeKick, m.Restart);
            Assert.AreEqual(Db().Referee.WallSize(20f), m.WallCount);
            Assert.IsTrue(m.DirectShotInRange());
            for (int i = 0; i < m.WallCount; i++) Assert.AreSame(m.Away, m.WallPlayer(i).Team);

            int steps = 0, limit = (int)((Db().Referee.FreeKickSetupSeconds + Db().Restarts.AiTakeSeconds) / Dt) + 5;
            var wall = new Vector3[m.WallCount];
            var ev = AiMatchEvent.None;
            while (m.Restart != RestartKind.None && steps++ < limit)
            {
                for (int i = 0; i < wall.Length; i++) wall[i] = m.WallPlayer(i).Body.Position; // where they stand as it is struck
                ev = m.Step();
            }
            // The wall stands at the free-kick distance, between the ball and the goal.
            var goal = m.Home.Frame.TargetGoal;
            for (int i = 0; i < wall.Length; i++)
            {
                Assert.That(Vector3.Distance(wall[i], spot), Is.EqualTo(Db().Restarts.ExclusionRadius + Db().Referee.WallDistanceMargin).Within(1f), $"wall player {i}");
                Assert.Less(Vector3.Distance(wall[i], goal), Vector3.Distance(spot, goal), $"wall player {i} between the ball and the goal");
            }
            Assert.AreEqual(RestartKind.None, m.Restart, "the free kick was taken.");
            Assert.AreEqual(AiMatchEvent.Shot, ev);
            Assert.AreEqual(0, m.WallCount);
        }

        [Test]
        public void AFarFreeKick_HasNoWall_AndIsPlayedAsAPass()
        {
            var m = new AiMatch(Db(), AiMatchTests.Setup(2), Dt);
            m.CancelRestart();
            m.AwardFreeKick(MatchSide.Home, m.Home.Frame.World(40f, 10f), direct: true);
            Assert.AreEqual(0, m.WallCount);
            Assert.IsFalse(m.DirectShotInRange());
            var ev = AiMatchEvent.None;
            for (int i = 0; i < 500 && m.Restart != RestartKind.None; i++) ev = m.Step();
            Assert.AreEqual(AiMatchEvent.Pass, ev);
        }

        // ---------------- headless ----------------

        [Test, Category("Slow")]
        public void Headless_RefereeRates_AreInRange()
        {
            var h = R()["headless"];
            var vs = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db()).Value;
            var r = HeadlessReport.Run(Db(), vs.Team("strong").ToSetup(), vs.Team("weak").ToSetup(), h["matches"].Value<int>(),
                h["minutes"].Value<int>(), h["seed"].Value<ulong>(), Dt);
            TestContext.WriteLine($"fouls {r.PerMatch(r.Fouls):F2}, yellows {r.PerMatch(r.Yellows):F2}, reds {r.PerMatch(r.Reds):F2}, "
                                  + $"offsides {r.PerMatch(r.Offsides):F2}, free kicks {r.PerMatch(r.FreeKicks):F2}, penalties {r.PerMatch(r.Penalties):F2}");
            void InRange(string name, double v) => Assert.That(v, Is.InRange(h[name][0].Value<double>(), h[name][1].Value<double>()), name);
            InRange("foulsPerMatch", r.PerMatch(r.Fouls));
            InRange("yellowPerMatch", r.PerMatch(r.Yellows));
            InRange("redPerMatch", r.PerMatch(r.Reds));
            InRange("offsidesPerMatch", r.PerMatch(r.Offsides));
            InRange("penaltiesPerMatch", r.PerMatch(r.Penalties));
        }
    }
}
