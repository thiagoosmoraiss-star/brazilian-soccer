using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    public class QuickSimBehaviourTests
    {
        private static TacticSetup Neutral(string formation = "4-4-2") => new TacticSetup(formation, 3, 2, 2);

        private static (WorldState World, Club Club) ClubOf(int division, int rankFromTop)
        {
            var w = SimTestData.World(1);
            var club = w.Clubs.Where(c => c.DivisionIndex == division)
                .OrderByDescending(c => WorldValidator.SquadAverageOvr(w, SimTestData.Db(), c.Id)).ThenBy(c => c.Id).ElementAt(rankFromTop);
            return (w, club);
        }

        // ---------------- Impossible results / invariants ----------------

        [Test, Category("Slow")]
        public void LeagueBatch_HasNoImpossibleResults()
        {
            int maxGoals = SimTestData.Db().QuickSim.MaxGoalsPerTeam;
            foreach (var m in SimTestData.League())
            {
                var r = m.Result;
                string ctx = $"seed={m.Setup.Seed}";
                foreach (var side in new[] { MatchSide.Home, MatchSide.Away })
                {
                    var s = r.Stats(side);
                    int goals = r.Goals(side);
                    var events = r.Events.Where(e => e.Side == side).ToList();
                    Assert.That(goals, Is.InRange(0, maxGoals), ctx);
                    Assert.AreEqual(goals, events.Count(e => e.Type == MatchEventType.Goal), ctx + " goals = goal events");
                    Assert.AreEqual(s.Shots, events.Count(e => e.Type == MatchEventType.Shot), ctx + " shots = shot events");
                    Assert.AreEqual(s.ShotsOnTarget, events.Count(e => e.Type == MatchEventType.ShotOnTarget), ctx);
                    Assert.AreEqual(s.Fouls, events.Count(e => e.Type == MatchEventType.Foul), ctx);
                    Assert.AreEqual(s.YellowCards, events.Count(e => e.Type == MatchEventType.YellowCard), ctx);
                    Assert.AreEqual(s.RedCards, events.Count(e => e.Type == MatchEventType.RedCard), ctx);
                    Assert.AreEqual(s.Corners, events.Count(e => e.Type == MatchEventType.Corner), ctx);
                    Assert.AreEqual(s.Substitutions, events.Count(e => e.Type == MatchEventType.Substitution), ctx);
                    Assert.That(s.ShotsOnTarget, Is.InRange(goals, s.Shots), ctx + " goals <= on target <= shots");
                    Assert.LessOrEqual(s.Substitutions, m.Setup.MaxSubstitutions, ctx);
                    Assert.LessOrEqual(s.RedCards, 4, ctx + " a team keeps at least 7 players");

                    var players = r.PlayerStats.Where(p => p.Side == side).ToList();
                    var cameOn = new HashSet<Id>(events.Where(e => e.Type == MatchEventType.Substitution).Select(e => e.OtherPlayerId));
                    Assert.AreEqual(goals, players.Sum(p => p.Goals), ctx);
                    Assert.AreEqual(s.YellowCards, players.Sum(p => p.YellowCards), ctx);
                    Assert.AreEqual(s.RedCards, players.Count(p => p.RedCard), ctx);
                    Assert.LessOrEqual(players.Sum(p => p.Assists), goals, ctx);
                    Assert.AreEqual(11, players.Count(p => p.Started), ctx);
                    Assert.LessOrEqual(players.Sum(p => p.MinutesPlayed), 11 * 90, ctx);
                    foreach (var p in players)
                    {
                        Assert.That(p.MinutesPlayed, Is.InRange(0, 90), ctx);
                        Assert.That(p.FinalEnergy, Is.InRange(0f, 100f), ctx);
                        Assert.LessOrEqual(p.YellowCards, 2, ctx);
                        bool entered = p.Started || cameOn.Contains(p.PlayerId);
                        if (entered) Assert.That(p.Rating, Is.Not.Null.And.InRange(0f, 10f), ctx);
                        else Assert.IsNull(p.Rating, ctx + " unused substitutes have no rating");
                        if (!entered) Assert.AreEqual(0, p.MinutesPlayed, ctx);
                    }
                }
                Assert.AreEqual(100f, r.HomeStats.Possession + r.AwayStats.Possession, 0.11f, ctx);
                CollectionAssert.AllItemsAreUnique(r.PlayerStats.Select(p => p.PlayerId), ctx);
                Assert.IsTrue(r.Events.All(e => e.Minute >= 1 && e.Minute <= 90), ctx);
                for (int i = 1; i < r.Events.Count; i++) Assert.GreaterOrEqual(r.Events[i].Minute, r.Events[i - 1].Minute, ctx + " events in order");
            }
        }

        // ---------------- Determinism ----------------

        [Test]
        public void SameSetup_ProducesTheSameResult()
        {
            foreach (var m in SimTestData.League().Take(50))
                Assert.AreEqual(Fingerprint(m.Result), Fingerprint(SimTestData.Sim().Simulate(m.Setup)), $"seed={m.Setup.Seed}");
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentMatches()
        {
            var (w, home) = ClubOf(1, 0);
            var (_, away) = ClubOf(1, 1);
            var results = Enumerable.Range(1, 20).Select(s => Fingerprint(SimTestData.Sim().Simulate(
                new MatchSetup(SimTestData.Team(w, home, Neutral()), SimTestData.Team(w, away, Neutral()), false, 6, 5, (ulong)s)))).ToList();
            Assert.Greater(results.Distinct().Count(), 15);
        }

        // ---------------- Strength drives results ----------------

        [Test, Category("Slow")]
        public void TopDivisionClub_BeatsBottomDivisionClub_MostOfTheTime_WithRareUpsets()
        {
            var cfg = SimTestData.Ranges()["crossDivision"];
            var (w, strong) = ClubOf(0, 7);   // mid-table A
            var (_, weak) = ClubOf(3, 7);     // mid-table D
            int n = cfg["matches"].Value<int>(), wins = 0, losses = 0;
            var strongTeam = SimTestData.Team(w, strong, Neutral());
            var weakTeam = SimTestData.Team(w, weak, Neutral());
            for (int i = 0; i < n; i++)
            {
                var r = SimTestData.Sim().Simulate(new MatchSetup(strongTeam, weakTeam, true, 6, 5, (ulong)(i + 1)));
                if (r.HomeGoals > r.AwayGoals) wins++; else if (r.HomeGoals < r.AwayGoals) losses++;
            }
            Assert.GreaterOrEqual(100.0 * wins / n, cfg["strongWinMinPct"].Value<double>(), "strong side wins");
            Assert.That(100.0 * losses / n, Is.InRange(cfg["weakWinMinPct"].Value<double>(), cfg["weakWinMaxPct"].Value<double>()), $"upsets exist but are rare (strong W {100.0 * wins / n:0.0}%, weak W {100.0 * losses / n:0.0}%)");
        }

        // ---------------- Attribute sensitivity (TEST_PLAN: +15 vs clone) ----------------

        private static IEnumerable<Attr> AllAttributes() => Enum.GetValues(typeof(Attr)).Cast<Attr>();

        [TestCaseSource(nameof(AllAttributes)), Category("Slow")]
        public void Plus15InAnAttribute_ChangesItsMetric_InTheExpectedDirection(Attr attr)
        {
            var cfg = SimTestData.Ranges()["sensitivity"];
            int boost = cfg["boost"].Value<int>(), n = cfg["matches"].Value<int>();
            double minZ = cfg["minZ"].Value<double>();
            string metric = cfg["metric"][attr.ToString()]?.Value<string>() ?? cfg["metric"]["default"].Value<string>();

            var (w, club) = ClubOf(1, 7);
            var clone = SimTestData.Team(w, club, Neutral());
            var boosted = Boost(clone, attr, boost, idOffset: 1_000_000);
            var opponent = Boost(clone, attr, 0, idOffset: 2_000_000); // identical players, distinct Ids

            // Paired design: identical seeds for boosted-vs-clone and clone-vs-clone (common random numbers).
            var deltas = new double[n];
            for (int i = 0; i < n; i++)
            {
                ulong seed = (ulong)(i + 1);
                var a = SimTestData.Sim().Simulate(new MatchSetup(boosted, opponent, true, 6, 5, seed));
                var b = SimTestData.Sim().Simulate(new MatchSetup(clone, opponent, true, 6, 5, seed));
                deltas[i] = Metric(metric, a, boosted) - Metric(metric, b, clone);
            }
            double mean = deltas.Average();
            double sd = Math.Sqrt(deltas.Sum(d => (d - mean) * (d - mean)) / (n - 1));
            double z = sd == 0 ? (mean > 0 ? double.PositiveInfinity : 0) : mean / (sd / Math.Sqrt(n));
            Assert.GreaterOrEqual(z, minZ, $"{attr}: metric {metric} mean delta {mean:0.000} (z={z:0.0}, n={n}, seeds 1..{n})");
        }

        private static double Metric(string metric, MatchResult r, MatchTeamSetup home)
        {
            switch (metric)
            {
                case "goalDifference": return r.HomeGoals - r.AwayGoals;
                case "finalEnergy":
                    var ids = new HashSet<Id>(home.Starters.Select(p => p.PlayerId));
                    return r.PlayerStats.Where(p => p.Side == MatchSide.Home && ids.Contains(p.PlayerId)).Average(p => p.FinalEnergy);
                default: throw new ArgumentException("Unknown metric " + metric);
            }
        }

        /// <summary>Clone of a team with +boost (capped at 99) in one attribute for every player, with shifted Ids.</summary>
        private static MatchTeamSetup Boost(MatchTeamSetup team, Attr attr, int boost, int idOffset)
        {
            MatchPlayerSetup B(MatchPlayerSetup p)
            {
                var attrs = p.Attributes.ToArray();
                attrs[(int)attr] = Math.Min(AttrInfo.MaxValue, attrs[(int)attr] + boost);
                return new MatchPlayerSetup(new Id(p.PlayerId.Value + idOffset), attrs, p.MainPosition, p.SecondaryPositions.ToArray(), p.Energy, p.Morale, p.Form);
            }
            return new MatchTeamSetup(new Id(team.ClubId.Value + idOffset), team.Tactic, team.Starters.Select(B).ToList(), team.Bench.Select(B).ToList());
        }

        // ---------------- Tactics ----------------

        [Test, Category("Slow")]
        public void Tactics_MoveStatisticsInTheExpectedDirection()
        {
            var (w, club) = ClubOf(1, 7);
            var (_, opp) = ClubOf(1, 8);
            var opponent = SimTestData.Team(w, opp, Neutral());
            const int n = 1500;
            (double shots, double conceded, double fouls, double energy) Run(TacticSetup t)
            {
                var team = SimTestData.Team(w, club, t);
                double shots = 0, conceded = 0, fouls = 0, energy = 0;
                for (int i = 0; i < n; i++)
                {
                    var r = SimTestData.Sim().Simulate(new MatchSetup(team, opponent, true, 6, 0, (ulong)(i + 1)));
                    shots += r.HomeStats.Shots; conceded += r.AwayGoals; fouls += r.HomeStats.Fouls;
                    energy += r.PlayerStats.Where(p => p.Side == MatchSide.Home).Average(p => p.FinalEnergy);
                }
                return (shots / n, conceded / n, fouls / n, energy / n);
            }
            var defensive = Run(new TacticSetup("4-4-2", 1, 2, 2));
            var attacking = Run(new TacticSetup("4-4-2", 5, 2, 2));
            Assert.Greater(attacking.shots, defensive.shots, "attacking mentality creates more shots");
            Assert.Greater(attacking.conceded, defensive.conceded, "attacking mentality concedes more");

            var lowPress = Run(new TacticSetup("4-4-2", 3, 2, 1));
            var highPress = Run(new TacticSetup("4-4-2", 3, 2, 3));
            Assert.Greater(highPress.fouls, lowPress.fouls, "high pressure commits more fouls");
            Assert.Less(highPress.energy, lowPress.energy, "high pressure tires the team more");
        }

        // ---------------- Performance ----------------

        [Test]
        public void Simulation_RunsInMilliseconds()
        {
            var setups = SimTestData.League().Take(500).Select(m => m.Setup).ToList();
            foreach (var s in setups.Take(20)) SimTestData.Sim().Simulate(s); // warm-up (JIT)
            var sw = Stopwatch.StartNew();
            foreach (var s in setups) SimTestData.Sim().Simulate(s);
            sw.Stop();
            double perMatch = sw.Elapsed.TotalMilliseconds / setups.Count;
            Assert.LessOrEqual(perMatch, SimTestData.Ranges()["maxMsPerMatch"].Value<double>(), $"{perMatch:0.000} ms per match");
        }

        // ---------------- Contract validation ----------------

        [Test]
        public void UnknownFormation_IsRejected()
        {
            var (w, club) = ClubOf(1, 0);
            var good = SimTestData.Team(w, club, Neutral());
            var bad = new MatchTeamSetup(good.ClubId, new TacticSetup("3-5-2", 3, 2, 2), good.Starters, good.Bench);
            Assert.Throws<ArgumentException>(() => SimTestData.Sim().Simulate(new MatchSetup(bad, good, false, 6, 5, 1)));
        }

        [Test]
        public void SamePlayerOnBothTeams_IsRejected()
        {
            var (w, club) = ClubOf(1, 0);
            var team = SimTestData.Team(w, club, Neutral());
            Assert.Throws<ArgumentException>(() => SimTestData.Sim().Simulate(new MatchSetup(team, team, false, 6, 5, 1)));
        }

        private static string Fingerprint(MatchResult r)
        {
            var sb = new StringBuilder();
            sb.Append(r.HomeGoals).Append('-').Append(r.AwayGoals).Append('|');
            foreach (var e in r.Events) sb.Append(e).Append(';');
            foreach (var p in r.PlayerStats) sb.Append(p.PlayerId).Append(':').Append(p.MinutesPlayed).Append(':').Append(p.Rating).Append(':').Append(p.FinalEnergy).Append(';');
            return sb.ToString();
        }
    }
}
