using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Match;
using Game.Match.AI;
using Game.Rules.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A9c substitutions in the playable match (X-77; GAME_DESIGN §27 "5 em 3 paradas + intervalo"; TECHNICAL_SPEC
    /// §10 "Substituições | Mesmas regras e IA"; TEST_PLAN "11 ou menos jogadores por time em campo; substituições dentro do
    /// limite"). Ranges in Data/TestRanges/substitutions.json.</summary>
    public class SubstitutionTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "substitutions.json"))));

        private static SubstitutionRules Rules => Db().MatchRules.Substitutions;

        private static MatchPlayerSetup P(int id, Position position, params (Attr attr, int value)[] overrides)
        {
            var a = Enumerable.Repeat(70, AttrInfo.Count).ToArray();
            foreach (var (attr, value) in overrides) a[(int)attr] = value;
            return new MatchPlayerSetup(new Id(id), a, (int)position, new int[0], 100f, 3, null);
        }

        /// <summary>4-4-2 sides of 70s with a bench of <paramref name="bench"/> (ids club*100+50+k, positions of the slots in
        /// order); <paramref name="homeBench"/> / <paramref name="homeStarter"/> override single home players.</summary>
        private static MatchSetup Setup(ulong seed, int maxSubs = 5, int bench = 7, int minutes = 4,
            Func<int, MatchPlayerSetup> homeStarter = null, Func<int, MatchPlayerSetup> homeBench = null)
        {
            var formation = Db().Formation("4-4-2");
            var tactic = new TacticSetup("4-4-2", 3, 2, 2);
            MatchTeamSetup Team(int club, bool home) => new MatchTeamSetup(new Id(club), tactic,
                Enumerable.Range(0, 11).Select(i => (home ? homeStarter?.Invoke(i) : null) ?? P(club * 100 + i, formation.Slots[i].Position)).ToArray(),
                Enumerable.Range(0, bench).Select(k => (home ? homeBench?.Invoke(k) : null) ?? P(club * 100 + 50 + k, formation.Slots[k % 11].Position)).ToArray());
            return new MatchSetup(Team(1, true), Team(2, false), false, minutes, maxSubs, seed);
        }

        private static VerticalSliceDefinition Teams()
        {
            var r = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db());
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        private static MatchSetup SliceSetup(ulong seed, int minutes)
        {
            var vs = Teams();
            return new MatchSetup(vs.Team("strong").ToSetup(), vs.Team("weak").ToSetup(), false, minutes, 5, seed);
        }

        private static void Steps(AiMatch m, int n)
        {
            for (int i = 0; i < n && !m.Finished; i++) m.Step();
        }

        // ---------------- shared rules and data ----------------

        [Test]
        public void SharedRules_Limits_WhoLeaves_WhoEnters_AndTheWindows()
        {
            var rules = Rules;
            Assert.IsTrue(Substitutions.HasSubstitutionLeft(4, 5));
            Assert.IsFalse(Substitutions.HasSubstitutionLeft(5, 5));
            Assert.IsTrue(Substitutions.HasStoppageLeft(rules.MaxWindows - 1, false, rules));
            Assert.IsFalse(Substitutions.HasStoppageLeft(rules.MaxWindows, false, rules));
            Assert.IsTrue(Substitutions.HasStoppageLeft(rules.MaxWindows, true, rules), "half-time is not a stoppage");
            Assert.IsTrue(Substitutions.CanLeave(true, false));
            Assert.IsFalse(Substitutions.CanLeave(true, true), "sent off");
            Assert.IsFalse(Substitutions.CanLeave(false, false), "not on the pitch");
            Assert.IsTrue(Substitutions.CanEnter(false, false));
            Assert.IsFalse(Substitutions.CanEnter(true, false), "already used");
            Assert.IsFalse(Substitutions.CanEnter(false, true), "sent off");
            Assert.IsFalse(Substitutions.IsWindow(rules.HalftimeMinute, rules), "half-time is handled apart");
            foreach (int w in rules.Windows.Where(w => w != rules.HalftimeMinute)) Assert.IsTrue(Substitutions.IsWindow(w, rules), w.ToString());
            Assert.IsTrue(Substitutions.IsTired(rules.EnergyThreshold - 1f, rules));
            Assert.IsFalse(Substitutions.IsTired(rules.EnergyThreshold, rules));
        }

        [Test]
        public void SliceTeams_HaveABench_AndTheFileRejectsBadBenchEntries()
        {
            var vs = Teams();
            foreach (var key in new[] { "strong", "weak" })
            {
                var t = vs.Team(key);
                Assert.Greater(t.Bench.Count, 0, key);
                Assert.AreEqual(1, t.Bench.Count(p => p.Position == Position.GOL), $"{key}: one substitute keeper");
                var setup = t.ToSetup();
                Assert.AreEqual(t.Bench.Count, setup.Bench.Count);
                Assert.AreEqual(Position.GOL, (Position)setup.Starters[0].MainPosition, "starters get their slot's position");
            }
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "VerticalSlice", "teams.json"));
            var j = JObject.Parse(json);
            j["teams"][0]["bench"][0]["extra"] = 1;
            Assert.IsFalse(VerticalSliceReader.Read(j.ToString(), Db()).IsSuccess, "unknown key");
            j = JObject.Parse(json);
            j["teams"][0]["bench"][0]["id"] = j["teams"][0]["players"][3]["id"];
            Assert.IsFalse(VerticalSliceReader.Read(j.ToString(), Db()).IsSuccess, "duplicate id");
            j = JObject.Parse(json);
            j["teams"][0]["bench"][0]["positions"] = new JArray("XX");
            Assert.IsFalse(VerticalSliceReader.Read(j.ToString(), Db()).IsSuccess, "unknown position");
            j = JObject.Parse(json);
            ((JObject)j["teams"][0]).Remove("bench");
            Assert.IsTrue(VerticalSliceReader.Read(j.ToString(), Db()).IsSuccess, "the bench is optional");
        }

        // ---------------- the user's substitutions ----------------

        [Test]
        public void AValidSubstitution_TakesTheSlot_AndCountsInTheResult()
        {
            var setup = Setup(1);
            var m = new AiMatch(Db(), setup, Dt);
            m.EnableHuman(MatchSide.Home); // the AI never substitutes the user's side
            Steps(m, 50);
            var p = m.Home.Players[5];
            int global = p.Global;
            var leaving = p.Setup;
            var entering = setup.Home.Bench[2];
            Assert.IsTrue(m.Substitute(MatchSide.Home, 5, 2));
            Assert.AreSame(entering, p.Setup);
            Assert.AreSame(p, m.Players[global], "same slot, same global index");
            Assert.AreEqual(entering.Energy, p.Body.Energy);
            Assert.IsFalse(p.Body.Down);
            if (m.Ball.Owner != global) Assert.AreEqual(AiIntention.HoldShape, p.Intention);
            Assert.AreEqual(1, m.SubstitutionsMade(MatchSide.Home));
            Assert.AreEqual(4, m.SubstitutionsLeft(MatchSide.Home));
            Assert.IsTrue(m.BenchUsed(MatchSide.Home, 2));
            Assert.IsTrue(m.CameOn(p));

            m.RunToEnd();
            var r = m.Result();
            Assert.AreEqual(1, r.HomeStats.Substitutions);
            var ev = r.Events.Single(e => e.Type == MatchEventType.Substitution && e.Side == MatchSide.Home);
            Assert.AreEqual(leaving.PlayerId, ev.PlayerId);
            Assert.AreEqual(entering.PlayerId, ev.OtherPlayerId);
            var off = r.PlayerStats.Single(s => s.PlayerId == leaving.PlayerId);
            var on = r.PlayerStats.Single(s => s.PlayerId == entering.PlayerId);
            Assert.IsTrue(off.Started);
            Assert.IsFalse(on.Started);
            Assert.AreEqual(m.TotalDisplayMinutes, off.MinutesPlayed + on.MinutesPlayed);
            Assert.AreEqual(12, r.PlayerStats.Count(s => s.Side == MatchSide.Home));
        }

        [Test]
        public void BeyondTheSubstitutionLimit_IsRefused_AndChangesInOnePauseAreOneStoppage()
        {
            var m = new AiMatch(Db(), Setup(2, maxSubs: 2), Dt);
            m.EnableHuman(MatchSide.Home);
            Steps(m, 10);
            Assert.IsTrue(m.Substitute(MatchSide.Home, 1, 0));
            Assert.IsTrue(m.Substitute(MatchSide.Home, 2, 1));
            Assert.IsFalse(m.CanSubstitute(MatchSide.Home, 3, 2));
            Assert.IsFalse(m.Substitute(MatchSide.Home, 3, 2), "MatchSetup.MaxSubstitutions");
            Assert.AreEqual(0, m.SubstitutionsLeft(MatchSide.Home));
            Assert.AreEqual(Rules.MaxWindows - 1, m.StoppagesLeft(MatchSide.Home), "two changes in the same pause");
            Assert.AreEqual(2, m.Events.Count(e => e.Type == MatchEventType.Substitution));
        }

        [Test]
        public void BeyondTheStoppageLimit_IsRefused_ButHalfTimeDoesNotCount()
        {
            var m = new AiMatch(Db(), Setup(3, maxSubs: 10, bench: 9), Dt);
            m.EnableHuman(MatchSide.Home);
            int windows = Rules.MaxWindows;
            for (int k = 0; k < windows; k++)
            {
                m.Step();
                Assert.IsTrue(m.Substitute(MatchSide.Home, 1 + k, k), $"stoppage {k + 1}");
            }
            Assert.AreEqual(0, m.StoppagesLeft(MatchSide.Home));
            m.Step();
            Assert.IsFalse(m.Substitute(MatchSide.Home, 5, windows), "no stoppage left");

            int guard = 0;
            while (m.Step() != AiMatchEvent.HalfTime) Assert.Less(++guard, 100000);
            Assert.IsTrue(m.IsHalfTimeBreak);
            Assert.IsTrue(m.Substitute(MatchSide.Home, 5, windows), "half-time does not use a stoppage");
            Assert.IsTrue(m.Substitute(MatchSide.Home, 6, windows + 1));
            Assert.AreEqual(0, m.StoppagesLeft(MatchSide.Home));
            m.Step();
            Assert.IsFalse(m.IsHalfTimeBreak);
            Assert.IsFalse(m.Substitute(MatchSide.Home, 7, windows + 2), "back in the second half: still no stoppage left");
        }

        [Test]
        public void AUsedSubstitute_DoesNotComeBack_AndBadIndicesAreRefused()
        {
            var setup = Setup(4);
            var m = new AiMatch(Db(), setup, Dt);
            m.EnableHuman(MatchSide.Home);
            Steps(m, 10);
            Assert.IsTrue(m.Substitute(MatchSide.Home, 3, 0));
            Assert.IsTrue(m.Substitute(MatchSide.Home, 3, 1), "a substitute may be substituted");
            Assert.IsFalse(m.Substitute(MatchSide.Home, 4, 0), "he left: he does not come back");
            Assert.IsFalse(m.Substitute(MatchSide.Home, 4, 1), "already on the pitch");
            Assert.IsFalse(m.Substitute(MatchSide.Home, 11, 2));
            Assert.IsFalse(m.Substitute(MatchSide.Home, -1, 2));
            Assert.IsFalse(m.Substitute(MatchSide.Home, 4, setup.Home.Bench.Count));
            var ids = m.Home.Players.Select(p => p.Setup.PlayerId).ToArray();
            Assert.AreEqual(11, ids.Distinct().Count());
            CollectionAssert.DoesNotContain(ids, setup.Home.Bench[0].PlayerId);
            CollectionAssert.DoesNotContain(ids, setup.Home.Starters[3].PlayerId);
        }

        [Test]
        public void ASentOffPlayer_CannotBeSubstituted()
        {
            var c = R()["redCard"];
            int seeds = c["seeds"].Value<int>();
            for (int seed = 1; seed <= seeds; seed++)
            {
                var m = new AiMatch(Db(), SliceSetup((ulong)seed, c["minutes"].Value<int>()), Dt);
                AiPlayer red = null;
                while (!m.Finished && red == null)
                {
                    m.Step();
                    red = m.Players.FirstOrDefault(p => m.SentOff(p));
                }
                if (red == null) continue;
                var side = red.Team.Side;
                int bench = Enumerable.Range(0, m.Bench(side).Count).FirstOrDefault(i => !m.BenchUsed(side, i));
                var other = red.Team.Players.First(p => p != red && !m.SentOff(p) && !p.IsGoalkeeper);
                if (!m.CanSubstitute(side, other.Local, bench)) continue; // limits used up already: not this test's case
                Assert.IsFalse(m.CanSubstitute(side, red.Local, bench), $"seed {seed}");
                Assert.IsFalse(m.Substitute(side, red.Local, bench), $"seed {seed}");
                Assert.IsTrue(m.Substitute(side, other.Local, bench), $"seed {seed}: the same change for a player on the pitch");
                Assert.IsTrue(m.SentOff(red), "the dismissed player stays off");
                Assert.AreEqual(m.PlayersOnPitch(red.Team), red.Team.Players.Count(p => !p.Body.OffPitch));
                Assert.LessOrEqual(m.PlayersOnPitch(red.Team), 10, "one short after the substitution too");
                return;
            }
            Assert.Fail($"no dismissal with a substitution still allowed in {seeds} seeds");
        }

        [Test]
        public void TheSubstitute_PlaysWithHisOwnAttributes()
        {
            var a = R()["attributes"];
            int slow = a["slowSpeed"].Value<int>(), fast = a["fastSpeed"].Value<int>();
            const int slot = 9;
            var setup = Setup(5,
                homeStarter: i => i == slot ? P(100 + i, Position.ATA, (Attr.Speed, slow), (Attr.Agility, slow)) : null,
                homeBench: k => k == 0 ? P(150, Position.ATA, (Attr.Speed, fast), (Attr.Agility, fast)) : null);
            float Run(bool substitute)
            {
                var m = new AiMatch(Db(), setup, Dt);
                m.CancelRestart();
                foreach (var p in m.Players)
                    if (!p.IsGoalkeeper) p.Body.Position = new Vector3(p.Body.Position.X * 0.3f, (p.Local % 2 == 0 ? 1f : -1f) * 30f, 0f);
                m.EnableHuman(MatchSide.Home);
                if (substitute) Assert.IsTrue(m.Substitute(MatchSide.Home, slot, 0));
                var runner = m.Home.Players[slot];
                m.Control.OnCaptured(slot);
                runner.Body.Position = new Vector3(-5f, 10f, 0f);
                runner.Body.Velocity = Vector3.Zero;
                var input = new HumanInput(new Vector2(-1f, 0f), true, ActionCommand.None, false, false);
                int steps = (int)(a["seconds"].Value<float>() / Dt);
                for (int i = 0; i < steps; i++) m.Step(input);
                Assert.AreSame(runner, m.Controlled);
                return -5f - runner.Body.Position.X;
            }
            float slowDistance = Run(false), fastDistance = Run(true);
            TestContext.WriteLine($"slow starter {slowDistance:F2} m, fast substitute {fastDistance:F2} m");
            Assert.Greater(fastDistance, slowDistance * a["minDistanceRatio"].Value<float>());
        }

        // ---------------- the AI ----------------

        [Test, Category("Slow")]
        public void TheAi_ReplacesTiredPlayers_AtItsWindows_NeverMoreThanEleven()
        {
            var h = R()["headless"];
            int matches = h["matches"].Value<int>();
            ulong seed = h["seed"].Value<ulong>();
            var rules = Rules;
            int firstWindow = rules.Windows.Where(w => w != rules.HalftimeMinute).Min();
            int total = 0;
            for (int i = 0; i < matches; i++)
            {
                var m = new AiMatch(Db(), SliceSetup(seed + (ulong)i, h["minutes"].Value<int>()), Dt);
                while (!m.Finished)
                {
                    m.Step();
                    foreach (var t in new[] { m.Home, m.Away })
                    {
                        if (m.PlayersOnPitch(t) > MatchTeamSetup.StarterCount) Assert.Fail($"seed {seed + (ulong)i}: more than 11");
                        if (t.Players.Select(p => p.Setup.PlayerId).Distinct().Count() != MatchTeamSetup.StarterCount)
                            Assert.Fail($"seed {seed + (ulong)i}: a player in two slots");
                    }
                }
                var r = m.Result();
                foreach (var side in new[] { MatchSide.Home, MatchSide.Away })
                {
                    int subs = r.Stats(side).Substitutions;
                    Assert.LessOrEqual(subs, Math.Min(rules.MaxTiredSubstitutions, 5), $"seed {seed + (ulong)i}");
                    Assert.AreEqual(MatchTeamSetup.StarterCount + subs, r.PlayerStats.Count(p => p.Side == side), $"seed {seed + (ulong)i}");
                }
                foreach (var e in r.Events.Where(e => e.Type == MatchEventType.Substitution))
                {
                    Assert.That(e.Minute == rules.HalftimeMinute || e.Minute >= firstWindow, $"seed {seed + (ulong)i}: minute {e.Minute}");
                    var off = r.PlayerStats.Single(p => p.PlayerId == e.PlayerId);
                    Assert.Less(off.FinalEnergy, rules.EnergyThreshold, $"seed {seed + (ulong)i}: only tired players are replaced");
                    total++;
                }
            }
            double perMatch = (double)total / matches;
            TestContext.WriteLine($"AI substitutions per match (both sides): {perMatch:F2}");
            var range = h["substitutionsPerMatch"];
            Assert.That(perMatch, Is.InRange(range[0].Value<double>(), range[1].Value<double>()));
        }

        [Test]
        public void TheAi_NeverSubstitutesTheUsersSide()
        {
            int away = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var m = new AiMatch(Db(), SliceSetup(seed, 4), Dt);
                m.EnableHuman(MatchSide.Home);
                m.RunToEnd();
                var r = m.Result();
                Assert.AreEqual(0, r.HomeStats.Substitutions, $"seed {seed}");
                away += r.AwayStats.Substitutions;
            }
            Assert.Greater(away, 0, "the AI side does substitute");
        }

        // ---------------- session, snapshot, allocation ----------------

        [Test]
        public void TheSession_SubstitutesOnlyPausedOrWithTheBallDead_AndStopsAtHalfTime()
        {
            var s = new MatchSession(Db(), Setup(6), Dt, MatchSide.Home) { PauseAtHalfTime = true };
            var m = s.Match;
            int guard = 0;
            while (m.Restart != RestartKind.None) { s.Advance(Dt, Vector2.Zero, false, false); Assert.Less(++guard, 10000); }
            Assert.IsFalse(s.Substitute(MatchSide.Home, 3, 0), "open play, not paused");
            s.Pause();
            Assert.IsTrue(s.Substitute(MatchSide.Home, 3, 0));
            s.Resume();
            guard = 0;
            while (!s.Paused) { s.Advance(0.1f, Vector2.Zero, false, false); Assert.Less(++guard, 100000); }
            Assert.IsTrue(m.IsHalfTimeBreak, "the session stopped at half-time");
            Assert.IsTrue(s.Substitute(MatchSide.Home, 4, 1));
        }

        [Test]
        public void SnapshotAndRestore_WithSubstitutionsMade_PlaysTheSameMatch()
        {
            var setup = SliceSetup(7, 4);
            var a = new AiMatch(Db(), setup, Dt);
            a.EnableHuman(MatchSide.Home);
            HumanInput Input(int i) => new HumanInput(new Vector2(MathF.Cos(i * 0.01f), MathF.Sin(i * 0.02f)), i % 5 == 0,
                i % 300 == 0 ? new ActionCommand(ActionKind.Pass, ActionCommand.AutoPower) : ActionCommand.None, false, i % 400 == 0);
            int step = 0;
            while (step < 2000) a.Step(Input(step++));
            Assert.IsTrue(a.Substitute(MatchSide.Home, 7, 3));
            Assert.IsTrue(a.Substitute(MatchSide.Home, 2, 2));
            while (a.Step(Input(step++)) != AiMatchEvent.HalfTime) { }
            Assert.IsTrue(a.Substitute(MatchSide.Home, 7, 4), "the substitute is substituted at half-time");

            var b = AiMatch.Restore(Db(), setup, Dt, a.Snapshot());
            CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot());
            for (int i = 0; i < a.Players.Length; i++) Assert.AreSame(a.Players[i].Setup, b.Players[i].Setup, $"slot {i}");
            Assert.AreEqual(a.StoppagesLeft(MatchSide.Home), b.StoppagesLeft(MatchSide.Home));
            Assert.IsFalse(b.Substitute(MatchSide.Home, 3, 3), "a used substitute stays used after the restore");

            while (!a.Finished)
            {
                var input = Input(step++);
                a.Step(input);
                b.Step(input);
            }
            CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), "same seed, same inputs: the same match");
            var ra = a.Result();
            var rb = b.Result();
            Assert.AreEqual(ra.PlayerStats.Count, rb.PlayerStats.Count);
            Assert.AreEqual(3, ra.HomeStats.Substitutions);
            Assert.AreEqual(ra.AwayStats.Substitutions, rb.AwayStats.Substitutions);
        }

        [Test]
        public void AWholeMatch_WithAiSubstitutions_AllocatesNothingPerStep()
        {
            var m = new AiMatch(Db(), SliceSetup(1, 4), Dt);
            long allocated = 0;
            while (!m.Finished)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                m.Step();
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.Greater(m.SubstitutionsMade(MatchSide.Home) + m.SubstitutionsMade(MatchSide.Away), 0, "this seed has AI substitutions");
            Assert.AreEqual(0, allocated, "CLAUDE.md §10: zero allocation per step in the match loop.");
        }
    }
}
