using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A6 (ROADMAP / TEST_PLAN: "defende chute fraco central; não alcança fora do alcance; tempo de reação por
    /// Reflexo; nunca anda para dentro do gol"; aceite: goleiro plausível). Scenarios run inside a real AiMatch with
    /// everyone but the away keeper parked at the far end. Ranges in Data/TestRanges/goalkeeper.json.</summary>
    public class GoalkeeperTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "goalkeeper.json"))));

        private sealed class ShotRun
        {
            public AiMatchEvent Outcome;
            public bool Saved;
            public AiMatch Match;
            public Keeper Keeper;
            public float MaxDisplacement;
            public float ReactedAfter = -1f;
            public bool ParryWentAway = true;
        }

        private static MatchSetup KeeperSetup(ulong seed, MatchPlayerSetup keeper)
        {
            var formation = Db().Formation("4-4-2");
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            MatchTeamSetup Team(int club, MatchPlayerSetup gk) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(i => formation.Slots[i].Role == Game.Data.Definitions.FormationRole.GK && gk != null ? gk : Player()).ToArray(),
                new MatchPlayerSetup[0]);
            return new MatchSetup(Team(1, null), Team(2, keeper), false, 6, 5, seed);
        }

        /// <summary>The away keeper defends the +x goal; a home player shoots from <paramref name="from"/> towards
        /// <paramref name="aim"/> (z = target height) at <paramref name="speed"/>.</summary>
        private static ShotRun Shoot(ulong seed, MatchPlayerSetup keeper, Vector3 from, Vector3 aim, float speed, Vector3? keeperAt = null)
        {
            return Run(Prepare(seed, keeper, from, aim, speed, keeperAt));
        }

        private static AiMatch Prepare(ulong seed, MatchPlayerSetup keeper, Vector3 from, Vector3 aim, float speed, Vector3? keeperAt = null)
        {
            var db = Db();
            var m = new AiMatch(db, KeeperSetup(seed, keeper), Dt);
            m.CancelRestart(); // open play: the scenario sets the ball itself
            var k = m.AwayKeeper;
            for (int i = 0; i < m.Players.Length; i++)
            {
                var p = m.Players[i];
                if (p == k.Player) continue;
                p.Body.Position = new Vector3(-45f + (i % 4), -20f + 2f * i, 0f); // parked at the far end
                p.Body.Velocity = Vector3.Zero;
            }
            k.AngleErrorDegrees = 0f;
            k.Player.Body.Position = keeperAt ?? Goalkeeper.PositionTarget(k, from, m.Pitch, db.Goalkeeper);
            k.Player.Body.Velocity = Vector3.Zero;
            k.Reset();

            var ball = m.Ball;
            ball.Position = new Vector3(from.X, from.Y, 0f);
            var flat = new Vector2(aim.X - from.X, aim.Y - from.Y);
            float dist = flat.Length();
            float elevation = aim.Z > 0f ? ShotSystem.LaunchElevation(speed, dist, aim.Z, db.Ball.Ball.Gravity) : 0f;
            var dir = flat / dist;
            ball.Kick(new Vector3(dir.X * MathF.Cos(elevation), dir.Y * MathF.Cos(elevation), MathF.Sin(elevation)) * speed, 0f);
            ball.LastTouch = m.Home.Players[10].Global;
            foreach (var p in m.Players) p.Body.IgnoreBallUntilClear = false;
            return m;
        }

        private static ShotRun Run(AiMatch m)
        {
            var k = m.AwayKeeper;
            var ball = m.Ball;
            var run = new ShotRun { Match = m, Keeper = k };
            var start = k.Player.Body.Position;
            for (int i = 0; i < 200; i++)
            {
                var ev = m.Step();
                if (ev != AiMatchEvent.Goal && ev != AiMatchEvent.Out) // those reset everyone to the kick-off shape
                    run.MaxDisplacement = MathF.Max(run.MaxDisplacement, Vector3.Distance(start, k.Player.Body.Position));
                if (run.ReactedAfter < 0f && k.State != KeeperState.Positioning && k.State != KeeperState.Reacting) run.ReactedAfter = m.ElapsedSeconds;
                bool keeperCaptured = ev == AiMatchEvent.Captured && ball.Owner == k.Player.Global;
                if (ev == AiMatchEvent.Save || keeperCaptured)
                {
                    run.Outcome = AiMatchEvent.Save;
                    run.Saved = true;
                    if (k.Parries > 0) run.ParryWentAway = ball.Velocity.X < 0f; // away from the +x goal
                    return run;
                }
                if (ev == AiMatchEvent.Goal || ev == AiMatchEvent.Out || ev == AiMatchEvent.Captured)
                {
                    run.Outcome = ev;
                    return run;
                }
            }
            run.Outcome = AiMatchEvent.None;
            return run;
        }

        [Test]
        public void KeeperSteps_AllocateNoMemory()
        {
            // Warm up the JIT on a full shot-and-save, then measure each step of another one (prediction, dive, save).
            Run(Prepare(1, Player(), new Vector3(GoalX - 15f, 0f, 0f), new Vector3(GoalX, 2.5f, 0.8f), 22f));
            var m = Prepare(2, Player(), new Vector3(GoalX - 15f, 0f, 0f), new Vector3(GoalX, 2.5f, 0.8f), 22f);
            long allocated = 0;
            bool saved = false;
            for (int i = 0; i < 150; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var ev = m.Step();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                saved |= ev == AiMatchEvent.Save;
            }
            Assert.IsTrue(saved || m.AwayKeeper.State != KeeperState.Positioning || m.Home.Goals > 0, "the scenario exercised the keeper.");
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per step.");
        }

        private static float GoalX => Db().Ball.Pitch.Length * 0.5f;

        [Test]
        public void WeakCentralShot_IsAlwaysSaved()
        {
            var r = R()["weakCentral"];
            float d = r["distance"].Value<float>(), speed = r["speed"].Value<float>();
            foreach (int attrs in r["keeperAttributes"].Values<int>())
            for (ulong seed = 1; seed <= r["seeds"].Value<ulong>(); seed++)
            {
                var run = Shoot(seed, Player(attrs), new Vector3(GoalX - d, 0f, 0f), new Vector3(GoalX, 0f, 0f), speed);
                Assert.IsTrue(run.Saved, $"keeper {attrs}, seed {seed}: a weak central shot ended {run.Outcome} (GAME_DESIGN §23: nunca ignorar chute fraco central).");
                Assert.IsTrue(run.ParryWentAway, $"keeper {attrs}, seed {seed}: the parry went back towards goal.");
            }
        }

        [Test]
        public void ShotOutOfReach_IsNeverTouched_AndTheKeeperDoesNotTeleport()
        {
            var r = R()["outOfReach"];
            var db = Db();
            float d = r["distance"].Value<float>(), speed = r["speed"].Value<float>();
            var keeperAt = new Vector3(GoalX - db.Goalkeeper.MinDepth, r["keeperPostY"].Value<float>(), 0f);
            var keeper = Player(99);
            float reach = db.Balance.Eval(Effect.GkDiveReach, keeper.Attributes);
            for (ulong seed = 1; seed <= r["seeds"].Value<ulong>(); seed++)
            {
                var run = Shoot(seed, keeper, new Vector3(GoalX - d, -2f, 0f), new Vector3(GoalX, r["farCornerY"].Value<float>(), 0.8f), speed, keeperAt);
                Assert.AreEqual(AiMatchEvent.Goal, run.Outcome, $"seed {seed}: Reflexo 99 still cannot reach the far corner from the other post.");
                Assert.AreEqual(0, run.Keeper.Saves, $"seed {seed}");
                Assert.LessOrEqual(run.MaxDisplacement, reach - db.Goalkeeper.HandReach + 0.05f, $"seed {seed}: the body moved further than a dive allows.");
            }
        }

        [Test]
        public void ReactionTime_FollowsReflexes()
        {
            var r = R()["reaction"];
            var db = Db();
            float lowReact = 0f, highReact = 0f;
            foreach (var attrs in new[] { r["low"].Value<int>(), r["high"].Value<int>() })
            {
                var keeper = Player(70, overrides: (Attr.GkReflexes, attrs));
                var run = Shoot(1, keeper, new Vector3(GoalX - 18f, 4f, 0f), new Vector3(GoalX, -3f, 0.8f), 20f);
                float react = run.Keeper.LastReactionSeconds;
                Assert.That(react, Is.InRange(r["minSeconds"].Value<float>(), r["maxSeconds"].Value<float>() + db.Goalkeeper.UnsetPenaltySeconds), $"Reflexo {attrs}");
                Assert.That(run.ReactedAfter, Is.GreaterThanOrEqualTo(react - Dt), $"Reflexo {attrs}: moved before reacting.");
                if (attrs == r["low"].Value<int>()) lowReact = run.ReactedAfter; else highReact = run.ReactedAfter;
            }
            Assert.Greater(lowReact, highReact, "Reflexo 90 reacts before Reflexo 30.");
        }

        [Test]
        public void BetterReflexes_SaveMore()
        {
            var r = R()["reflexes"];
            int targets = r["targets"].Value<int>();
            float spread = r["targetHalfSpread"].Value<float>();
            int Saves(int reflexes)
            {
                int saves = 0;
                for (int t = 0; t < targets; t++)
                {
                    float y = -spread + 2f * spread * t / (targets - 1);
                    var run = Shoot(1, Player(70, overrides: (Attr.GkReflexes, reflexes)), new Vector3(GoalX - r["distance"].Value<float>(), 0f, 0f),
                        new Vector3(GoalX, y, 0.8f), r["speed"].Value<float>());
                    if (run.Saved) saves++;
                }
                return saves;
            }
            int low = Saves(30), high = Saves(90);
            TestContext.WriteLine($"saves of {targets} shots across the goal: Reflexo 30 = {low}, Reflexo 90 = {high}");
            Assert.GreaterOrEqual(high - low, r["minExtraSaves"].Value<int>(), $"Reflexo 30 saved {low}/{targets}, Reflexo 90 {high}/{targets}.");
        }

        [Test]
        public void BetterHands_CatchMore_AndACaughtBallIsDistributed()
        {
            var r = R()["handling"];
            var db = Db();
            int n = r["seeds"].Value<int>();
            float Rate(int hands)
            {
                int catches = 0, saves = 0;
                for (ulong seed = 1; seed <= (ulong)n; seed++)
                {
                    var run = Shoot(seed, Player(70, overrides: (Attr.GkHandling, hands)), new Vector3(GoalX - r["distance"].Value<float>(), 1f, 0f),
                        new Vector3(GoalX, 0.5f, 1.0f), r["speed"].Value<float>());
                    Assert.IsTrue(run.Saved, $"Mãos {hands}, seed {seed}: a shot at the keeper ended {run.Outcome}.");
                    Assert.IsTrue(run.ParryWentAway, $"Mãos {hands}, seed {seed}: the parry went back towards goal.");
                    saves++;
                    if (run.Keeper.Catches == 0) continue;
                    catches++;
                    var m = run.Match;
                    bool passed = false;
                    for (int i = 0; i < (int)((db.Goalkeeper.HoldSeconds + 1f) / Dt) && !passed; i++)
                        passed = m.Step() == AiMatchEvent.Pass && m.Ball.LastTouch == run.Keeper.Player.Global;
                    Assert.IsTrue(passed, $"Mãos {hands}, seed {seed}: the keeper kept the ball.");
                }
                return catches / (float)saves;
            }
            float low = Rate(r["low"].Value<int>()), high = Rate(r["high"].Value<int>());
            TestContext.WriteLine($"catch rate: Mãos 30 = {low:P0}, Mãos 90 = {high:P0}");
            Assert.GreaterOrEqual(high - low, r["minCatchRateGap"].Value<float>(), $"catch rate Mãos 30 {low:P0}, Mãos 90 {high:P0}.");
        }

        [Test]
        public void InMatches_KeepersNeverWalkIntoTheGoal_AndSaveMostShotsTheyCanReact_To()
        {
            var r = R()["match"];
            var db = Db();
            float minArrival = r["minArrivalSeconds"].Value<float>();
            int saved = 0, conceded = 0;
            foreach (var seed in r["seeds"].Values<ulong>())
            {
                var m = new AiMatch(db, AiMatchTests.Setup(seed), Dt);
                Keeper tracked = null;
                while (!m.Finished)
                {
                    var ev = m.Step();
                    foreach (var k in new[] { m.HomeKeeper, m.AwayKeeper })
                        Assert.GreaterOrEqual(k.Player.Team.Frame.U(k.Player.Body.Position), db.Goalkeeper.MinDepth - 1e-4f,
                            $"seed {seed}, t={m.ElapsedSeconds:F2}: {k.Player.Team.Side} keeper inside his goal.");

                    if (ev == AiMatchEvent.Shot)
                    {
                        tracked = null;
                        var k = m.KeeperOf(m.Opponents(m.Players[m.Ball.LastTouch].Team));
                        var scan = Goalkeeper.Scan(k, m.Ball, m.Pitch, db.Ball.Ball, db.Balance, db.Goalkeeper,
                            Goalkeeper.ReactionSeconds(k, db.Balance, db.Goalkeeper), Dt);
                        if (scan.Threat && scan.Time >= minArrival) tracked = k;
                    }
                    else if (tracked != null)
                    {
                        if (ev == AiMatchEvent.Goal) { conceded++; tracked = null; }
                        else if (ev == AiMatchEvent.Save || (ev == AiMatchEvent.Captured && m.Ball.Owner == tracked.Player.Global)) { saved++; tracked = null; }
                        else if (ev != AiMatchEvent.None) tracked = null;
                    }
                }
            }
            float rate = saved / (float)Math.Max(1, saved + conceded);
            TestContext.WriteLine($"saved {saved} of {saved + conceded} shots on target arriving >= {minArrival} s after the shot ({rate:P0})");
            Assert.GreaterOrEqual(saved + conceded, r["minJudgedShots"].Value<int>(), "too few shots on target to judge.");
            Assert.That(rate, Is.InRange(r["minSaveRate"].Value<float>(), r["maxSaveRate"].Value<float>()),
                $"keepers saved {saved} of {saved + conceded} shots on target they had time to react to.");
        }

        [Test]
        public void TheUserNeverDrivesTheKeeper()
        {
            foreach (var seed in R()["match"]["seeds"].Values<ulong>().Take(3))
            {
                var m = new AiMatch(Db(), AiMatchTests.Setup(seed), Dt);
                m.EnableHuman(MatchSide.Home);
                var input = new HumanInput(-Vector2.UnitX, false, ActionCommand.None, false, false);
                for (int i = 0; !m.Finished; i++)
                {
                    m.Step(i % 150 == 0 ? new HumanInput(-Vector2.UnitX, false, new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower), false, false) : input);
                    bool takingGoalKick = m.Restart == RestartKind.GoalKick && m.RestartReady && m.Taker == m.Controlled;
                    Assert.IsFalse(m.Controlled.IsGoalkeeper && !takingGoalKick,
                        $"seed {seed}, t={m.ElapsedSeconds:F2}: control went to the keeper outside a goal kick (X-55: the user aims the short goal kick).");
                }
            }
        }

        [Test]
        public void RealGoalkeeperFile_Loads()
        {
            var r = GameDataLoader.LoadGoalkeeper(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.That(r.Value.GetUpMinSeconds, Is.EqualTo(0.6f).Within(1e-5f), "GAME_DESIGN §23: levanta em 0,6-1,0 s.");
            Assert.That(r.Value.GetUpMaxSeconds, Is.EqualTo(1.0f).Within(1e-5f));
        }

        [Test]
        public void ReactionCurve_MatchesTheDesignRange()
        {
            var b = Db().Balance;
            Assert.That(b.Eval(Effect.GkReactionTime, Player(1).Attributes), Is.EqualTo(0.30f).Within(1e-4f));
            Assert.That(b.Eval(Effect.GkReactionTime, Player(99).Attributes), Is.EqualTo(0.15f).Within(1e-4f));
        }

        [Test]
        public void GoalkeeperUnknownKey_IsRejected() =>
            Assert.IsFalse(GoalkeeperReader.Read(ReadGk().Replace("\"handReach\"", "\"hands\"")).IsSuccess);

        [Test]
        public void GoalkeeperMinDepthZero_IsRejected() =>
            Assert.IsFalse(GoalkeeperReader.Read(ReadGk().Replace("\"minDepth\": 0.8", "\"minDepth\": 0.0")).IsSuccess);

        [Test]
        public void ParryRestitutionOfOne_IsRejected() =>
            Assert.IsFalse(GoalkeeperReader.Read(ReadGk().Replace("\"parryRestitution\": 0.35", "\"parryRestitution\": 1.0")).IsSuccess);

        private static string ReadGk() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "goalkeeper.json"));

        [Test]
        public void Bisector_FacesTheBall_AndStaysInFront()
        {
            var db = Db();
            var m = new AiMatch(db, AiMatchTests.Setup(1), Dt);
            var k = m.AwayKeeper;
            k.AngleErrorDegrees = 0f;
            var central = Goalkeeper.PositionTarget(k, new Vector3(GoalX - 20f, 0f, 0f), m.Pitch, db.Goalkeeper);
            Assert.AreEqual(0f, central.Y, 1e-4f, "ball central → keeper central.");
            var wide = Goalkeeper.PositionTarget(k, new Vector3(GoalX - 10f, 15f, 0f), m.Pitch, db.Goalkeeper);
            Assert.Greater(wide.Y, 0f, "ball to one side → keeper shifts to that side.");
            Assert.LessOrEqual(wide.Y, m.Pitch.HalfGoalWidth + db.Goalkeeper.LateralLimit + 1e-4f);
            var byline = Goalkeeper.PositionTarget(k, new Vector3(GoalX - 0.2f, 20f, 0f), m.Pitch, db.Goalkeeper);
            Assert.LessOrEqual(byline.X, GoalX - db.Goalkeeper.MinDepth + 1e-4f, "never on or behind the line.");
            var near = Goalkeeper.PositionTarget(k, new Vector3(GoalX - 8f, 0f, 0f), m.Pitch, db.Goalkeeper);
            var far = Goalkeeper.PositionTarget(k, new Vector3(GoalX - 40f, 0f, 0f), m.Pitch, db.Goalkeeper);
            Assert.Less(far.X, near.X, "avança com a bola longe, recua com a bola perto.");
        }
    }
}
