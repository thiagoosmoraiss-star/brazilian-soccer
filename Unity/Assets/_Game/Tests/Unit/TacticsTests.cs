using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Loading;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A9c tactics (ROADMAP A9 "mudança tática altera posições médias"; TECHNICAL_SPEC §9; GAME_DESIGN §27;
    /// X-78): mentality, line and pressure in the played match, changed during it, and formation changes. Ranges in
    /// Data/TestRanges/tactics.json.</summary>
    public class TacticsTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "tactics.json"))));

        private static MatchSetup Setup(ulong seed, TacticSetup home, int attributes = 70, int minutes = 6)
        {
            MatchTeamSetup Team(int club, TacticSetup t) => new MatchTeamSetup(new Id(club), t,
                Enumerable.Range(0, 11).Select(_ => Player(attributes)).ToArray(), new MatchPlayerSetup[0]);
            return new MatchSetup(Team(1, home), Team(2, new TacticSetup("4-4-2", 3, 2, 2)), false, minutes, 5, seed);
        }

        [Test]
        public void TacticsFile_Loads_AndRejectsBadModifiers()
        {
            Assert.IsNotNull(Db().Tactics.Modifiers);
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Balance", "tactics.json"));
            var j = JObject.Parse(json);
            j["modifiers"]["mentality"]["lineOffset"] = new JArray(1.0, 2.0);
            Assert.IsFalse(AiReader.ReadTactics(j.ToString()).IsSuccess, "5 mentality levels");
            j = JObject.Parse(json);
            j["modifiers"]["pressure"]["pressersOffset"] = new JArray(-5, 0, 1);
            Assert.IsFalse(AiReader.ReadTactics(j.ToString()).IsSuccess, "negative pressers");
            j = JObject.Parse(json);
            j["modifiers"]["extra"] = 1;
            Assert.IsFalse(AiReader.ReadTactics(j.ToString()).IsSuccess, "unknown key");
            j = JObject.Parse(json);
            j["modifiers"]["mentality"]["pressedOutletValue"] = new JArray(-0.1, 0.0, 0.0, 0.0, 0.0);
            Assert.IsFalse(AiReader.ReadTactics(j.ToString()).IsSuccess, "negative outlet value");
            j = JObject.Parse(json);
            j["modifiers"]["mentality"]["boxAttackersOffset"] = new JArray(0, 1);
            Assert.IsFalse(AiReader.ReadTactics(j.ToString()).IsSuccess, "5 mentality levels");
        }

        [Test]
        public void TheDefaultTactic_IsTheDefaultBlock()
        {
            var d = Db().Tactics;
            var r = new TacticsRuntime(d, Db().MatchRules.Fatigue, new TacticSetup("4-4-2", 3, 2, 2));
            for (int i = 0; i < TeamPhaseInfo.Count; i++)
            {
                var p = (TeamPhase)i;
                Assert.AreEqual(d.Phases[i].Depth, r.Depth(p), 1e-4f, p.ToString());
                Assert.AreEqual(d.Phases[i].LineBehindBall, r.LineBehindBall(p), 1e-4f);
                Assert.AreEqual(d.Phases[i].MaxLine, r.MaxLine(p), 1e-4f);
            }
            Assert.AreEqual(d.MinLine, r.MinLine, 1e-4f);
            Assert.AreEqual(d.Pressers, r.Pressers);
            Assert.AreEqual(d.PressTriggerDistance, r.PressTriggerDistance, 1e-4f);
            Assert.AreEqual(1f, r.FatigueIntensity, 1e-4f);
        }

        private struct Shape { public double MeanU, DefLine, Energy; }

        /// <summary>The home side's average shape over a few headless matches against the default tactic.</summary>
        private static Shape Measure(TacticSetup tactic)
        {
            var d = R()["direction"];
            int matches = d["matches"].Value<int>();
            double meanU = 0, defLine = 0, energy = 0;
            int samples = 0, defSamples = 0;
            for (int s = 1; s <= matches; s++)
            {
                var m = new AiMatch(Db(), Setup((ulong)s, tactic, d["attributes"].Value<int>(), d["minutes"].Value<int>()), Dt);
                var f = m.Home.Frame;
                var outfield = m.Home.Players.Where(p => !p.IsGoalkeeper).ToArray();
                var us = new float[outfield.Length];
                int step = 0;
                while (!m.Finished)
                {
                    m.Step();
                    if (++step % 25 != 0) continue;
                    for (int i = 0; i < outfield.Length; i++) us[i] = f.U(outfield[i].Body.Position);
                    meanU += us.Average();
                    samples++;
                    if (m.Home.HasPossession) continue;
                    Array.Sort(us);
                    defLine += us[1]; // the second-deepest outfield player: the defensive line
                    defSamples++;
                }
                energy += m.Home.Players.Average(p => p.Body.Energy);
            }
            return new Shape { MeanU = meanU / samples, DefLine = defLine / defSamples, Energy = energy / matches };
        }

        [Test, Category("Slow")]
        public void AHigherMentality_PushesTheWholeTeamUp()
        {
            var low = Measure(new TacticSetup("4-4-2", 1, 2, 2));
            var high = Measure(new TacticSetup("4-4-2", 5, 2, 2));
            TestContext.WriteLine($"mean u: mentality 1 {low.MeanU:F1}, mentality 5 {high.MeanU:F1}");
            Assert.Greater(high.MeanU - low.MeanU, R()["direction"]["mentalityMeanUMin"].Value<double>());
        }

        [Test, Category("Slow")]
        public void AHigherLine_DefendsFurtherUp()
        {
            var low = Measure(new TacticSetup("4-4-2", 3, 1, 2));
            var high = Measure(new TacticSetup("4-4-2", 3, 3, 2));
            TestContext.WriteLine($"defensive line: line 1 {low.DefLine:F1}, line 3 {high.DefLine:F1}");
            Assert.Greater(high.DefLine - low.DefLine, R()["direction"]["lineDefensiveLineMin"].Value<double>());
        }

        [Test, Category("Slow")]
        public void HighPressure_CostsMoreEnergy_AndUsesMorePressers()
        {
            var low = Measure(new TacticSetup("4-4-2", 3, 2, 1));
            var high = Measure(new TacticSetup("4-4-2", 3, 2, 3));
            TestContext.WriteLine($"energy at the end: pressure 1 {low.Energy:F1}, pressure 3 {high.Energy:F1}");
            Assert.Greater(low.Energy - high.Energy, R()["direction"]["pressureEnergyMin"].Value<double>());
            var m = new AiMatch(Db(), Setup(1, new TacticSetup("4-4-2", 3, 2, 3)), Dt);
            Assert.AreEqual(Db().Tactics.Pressers + 1, m.Home.Runtime.Pressers);
            Assert.Greater(m.Home.Runtime.PressTriggerDistance, Db().Tactics.PressTriggerDistance);
        }

        [Test]
        public void AnAttackingMentality_SendsMorePlayersIntoTheBox_ADefensiveOneFewer()
        {
            var offset = Db().Tactics.Modifiers.MentalityBoxAttackersOffset;
            Assert.Greater(offset[4], offset[2], "D-37: the offensive side commits more players forward");
            Assert.Less(offset[0], offset[2], "the defensive one fewer");
            foreach (int mentality in new[] { 1, 3, 5 })
            {
                var m = new AiMatch(Db(), Setup(1, new TacticSetup("4-4-2", mentality, 2, 2)), Dt);
                m.CancelRestart();
                foreach (var o in m.Away.Players) o.Body.Position = m.Away.Frame.World(4f, 30f);
                var carrier = m.Home.Players[5];
                carrier.Body.Position = m.Home.Frame.World(80f, 0f);
                m.PlaceBall(carrier);
                for (int i = 0; i < 25; i++) m.Step();
                Assert.IsTrue(m.Home.HasPossession);
                Assert.AreEqual(Db().Ai.BoxAttackers + offset[mentality - 1], m.Home.Players.Count(p => m.Home.AttacksTheBox(p.Local)), $"mentality {mentality}");
            }
        }

        [Test]
        public void PressedInTheOwnThird_ADefensiveMentality_GetsItLongMoreOften()
        {
            var outlet = Db().Tactics.Modifiers.MentalityPressedOutletValue;
            Assert.Greater(outlet[0], outlet[2], "D-37: the defensive side prefers the long outlet when pressed in its own third");
            int LongBalls(int mentality, float carrierU)
            {
                var m = new AiMatch(Db(), Setup(1, new TacticSetup("4-4-2", mentality, 2, 2)), Dt);
                m.Step(); // the team layer has set the offside line
                var team = m.Home;
                var carrier = team.Players[5];
                carrier.Body.Position = team.Frame.World(carrierU, 0f);
                foreach (var p in team.Players) if (p != carrier) p.Body.Position = team.Frame.World(5f, 33f);
                team.Players[6].Body.Position = team.Frame.World(carrierU + 9f, 8f);  // short, open-ish
                team.Players[9].Body.Position = team.Frame.World(carrierU + 37f, 0f); // far up: a lançamento
                foreach (var o in m.Away.Players) o.Body.Position = team.Frame.World(100f, -33f);
                m.Away.Players[4].Body.Position = team.Frame.World(carrierU + 1.5f, 0.5f); // the presser
                int high = 0;
                for (int i = 0; i < 300; i++)
                    if (OnBallDecision.Decide(carrier, m.Away, m.Pitch, Db().Balance, Db().Ai, Db().Aerial, Db().Duels).Choice == OnBallChoice.High) high++;
                return high;
            }
            Assert.Greater(LongBalls(1, 15f), LongBalls(3, 15f), "in the own third (the defensive side values progress less, so this is the outlet)");
        }

        [Test]
        public void ATacticChangedDuringTheMatch_EasesIn()
        {
            var m = new AiMatch(Db(), Setup(1, new TacticSetup("4-4-2", 3, 2, 2)), Dt);
            for (int i = 0; i < 100; i++) m.Step();
            float before = m.Home.Runtime.MaxLine(TeamPhase.Defend);
            Assert.IsTrue(m.SetTactic(MatchSide.Home, new TacticSetup("4-4-2", 5, 3, 2)));
            var target = new TacticsRuntime(Db().Tactics, Db().MatchRules.Fatigue, new TacticSetup("4-4-2", 5, 3, 2)).MaxLine(TeamPhase.Defend);
            Assert.Greater(target, before);
            m.Step();
            Assert.Less(m.Home.Runtime.MaxLine(TeamPhase.Defend), before + 0.5f * (target - before), "eased, not a jump");
            var s = R()["smoothing"];
            int steps = (int)(s["settledSeconds"].Value<float>() / Dt);
            for (int i = 0; i < steps; i++) m.Step();
            Assert.AreEqual(target, m.Home.Runtime.MaxLine(TeamPhase.Defend), s["toleranceMeters"].Value<float>(), "settled");
            Assert.AreEqual(5, m.TacticOf(MatchSide.Home).Mentality);
        }

        [Test]
        public void AFormationChange_RemapsEverySlotOnce_KeeperStays_AndPlayersMoveLittle()
        {
            var m = new AiMatch(Db(), Setup(2, new TacticSetup("4-4-2", 3, 2, 2)), Dt);
            var oldSlots = m.Home.Players.ToDictionary(p => p, p => p.Slot);
            var keeper = m.Home.Players.Single(p => p.IsGoalkeeper);
            Assert.IsTrue(m.SetTactic(MatchSide.Home, new TacticSetup("4-3-3", 3, 2, 2)));
            var f433 = Db().Formation("4-3-3");
            Assert.AreEqual("4-3-3", m.Home.Formation.Id);
            CollectionAssert.AreEquivalent(f433.Slots, m.Home.Players.Select(p => p.Slot), "every slot taken exactly once");
            Assert.AreEqual(FormationRole.GK, keeper.Slot.Role);
            double moved = m.Home.Players.Average(p => Math.Sqrt(Math.Pow(p.Slot.X - oldSlots[p].X, 2) + Math.Pow(p.Slot.Y - oldSlots[p].Y, 2)));
            Assert.Less(moved, 0.2, "least displacement");
            for (int i = 0; i < 200; i++) m.Step(); // it keeps playing
            Assert.IsFalse(m.SetTactic(MatchSide.Home, new TacticSetup("9-0-1", 3, 2, 2)), "unknown formation");
            Assert.AreEqual("4-3-3", m.Home.Formation.Id);
        }

        [Test]
        public void ARestoredMatch_KeepsTheChangedTactic_AndPlaysOnIdentically()
        {
            var setup = Setup(3, new TacticSetup("4-4-2", 3, 2, 2));
            var a = new AiMatch(Db(), setup, Dt);
            for (int i = 0; i < 150; i++) a.Step();
            a.SetTactic(MatchSide.Home, new TacticSetup("4-2-3-1", 4, 3, 3));
            a.SetTactic(MatchSide.Away, new TacticSetup("4-3-3", 2, 1, 1));
            for (int i = 0; i < 30; i++) a.Step();
            var b = AiMatch.Restore(Db(), setup, Dt, a.Snapshot());
            Assert.AreEqual("4-2-3-1", b.Home.Formation.Id);
            Assert.AreEqual(2, b.TacticOf(MatchSide.Away).Mentality);
            for (int i = 0; i < 400; i++) { a.Step(); b.Step(); }
            CollectionAssert.AreEqual(a.Snapshot(), b.Snapshot(), "same state after playing on");
        }
    }
}
