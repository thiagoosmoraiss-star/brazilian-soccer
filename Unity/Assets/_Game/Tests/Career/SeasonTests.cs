using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Players;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>
    /// B3 acceptance (ROADMAP): 10 seasons headless with the QuickSim; clubs per division constant; promotion and
    /// relegation match the final tables (X-39); calendar valid (everyone plays everything, no two matches on one date,
    /// minimum interval); cup qualification (X-40) and knockout integrity.
    /// </summary>
    [Category("Slow")]
    public class SeasonTests
    {
        private sealed class SeasonRecord
        {
            public Season Season;
            public SeasonSummary Summary;
            public Dictionary<Id, int> DivisionAtStart;
            /// <summary>Stadium level at the moment promotion was decided (B7, X-45: the stadium requirement gate).</summary>
            public Dictionary<Id, int> StadiumLevelAtEnd;
            /// <summary>B4 snapshots at season end (after retirements, before the next youth intake).</summary>
            public int PlayersAbovePotential;
            public int OldestAge;
            public Dictionary<Id, int> SquadSize;
            public Dictionary<Id, int> Goalkeepers;
            public double[] Top22ByDivision;
        }

        /// <summary>Lineup violations seen by the MatchPlayed hook: unavailable players used while enough were available.</summary>
        private static readonly Dictionary<ulong, List<string>> Violations = new Dictionary<ulong, List<string>>();
        private static readonly Dictionary<ulong, List<string>> FloorBreaches = new Dictionary<ulong, List<string>>();

        private static readonly Dictionary<ulong, List<SeasonRecord>> Careers = new Dictionary<ulong, List<SeasonRecord>>();
        private static readonly Dictionary<ulong, Dictionary<(int Year, Id Club), int>> YouthIntakeCounts =
            new Dictionary<ulong, Dictionary<(int Year, Id Club), int>>();

        private static IEnumerable<ulong> Seeds() => CareerTestData.Ints("careerSeeds").Select(x => (ulong)x);

        private static List<SeasonRecord> Career(ulong seed)
        {
            if (Careers.TryGetValue(seed, out var list)) return list;
            var db = CareerTestData.Db();
            var quickSim = new Game.Simulation.QuickSim.QuickSim(db);
            var career = CareerSimulator.Start(db, seed, quickSim.Simulate, new Game.Career.Market.TransferWindow(), new Game.Career.Economy.EconomySystem());
            var violations = Violations[seed] = new List<string>();
            var floor = FloorBreaches[seed] = new List<string>();
            var youthCounts = YouthIntakeCounts[seed] = new Dictionary<(int Year, Id Club), int>();
            career.YouthIntakeHappened += created =>
            {
                int year = career.State.Season.Year;
                foreach (var p in created)
                {
                    var key = (year, career.State.World.ClubOf(p.Id));
                    youthCounts[key] = youthCounts.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            };
            var dev = db.Development;
            career.LineupsSelected += (f, setup) =>
            {
                var w = career.State.World;
                foreach (var team in new[] { setup.Home, setup.Away })
                {
                    var squad = w.SquadOf(team.ClubId);
                    var byId = squad.ToDictionary(p => p.Id);
                    // Independent of ConditionSystem.Available: read the condition fields directly.
                    bool Out(Player p) => p.Condition.InjuryMatchesLeft > 0
                                          || (p.Condition.InjuredUntil.HasValue && f.Date < p.Condition.InjuredUntil.Value)
                                          || p.Condition.SuspendedMatches((int)f.Kind) > 0;
                    int available = squad.Count(p => !Out(p));
                    foreach (var mp in team.Starters.Concat(team.Bench))
                        if (Out(byId[mp.PlayerId]) && available >= 11)
                            violations.Add($"{f.Date:d} club={team.ClubId} player={mp.PlayerId}");
                    if (squad.Count < dev.MinSquadSize || squad.Count(p => p.MainPosition == Game.Data.Definitions.Position.GOL) < dev.MinGoalkeepers)
                        floor.Add($"{f.Date:d} club={team.ClubId} size={squad.Count}");
                }
            };
            list = new List<SeasonRecord>();
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                var record = new SeasonRecord
                {
                    Season = career.State.Season,
                    DivisionAtStart = career.State.World.Clubs.ToDictionary(c => c.Id, c => c.DivisionIndex),
                };
                record.Summary = career.PlaySeason();
                var w = career.State.World;
                record.StadiumLevelAtEnd = w.Clubs.ToDictionary(c => c.Id, c => c.Stadium.Level);
                int endYear = record.Season.Year;
                record.PlayersAbovePotential = w.Players.Count(p => Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition) > p.Potential);
                record.OldestAge = w.Players.Max(p => p.BirthDate.AgeOn(endYear, 12, 31));
                record.SquadSize = w.Clubs.ToDictionary(c => c.Id, c => w.SquadOf(c.Id).Count);
                record.Goalkeepers = w.Clubs.ToDictionary(c => c.Id, c => w.SquadOf(c.Id).Count(p => p.MainPosition == Game.Data.Definitions.Position.GOL));
                record.Top22ByDivision = w.DivisionNames.Select((_, d) => w.Clubs.Where(c => c.DivisionIndex == d)
                    .Average(c => w.SquadOf(c.Id).Select(p => (double)Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition))
                        .OrderByDescending(x => x).Take(22).Average())).ToArray();
                list.Add(record);
            }
            Careers[seed] = list;
            return list;
        }

        [TestCaseSource(nameof(Seeds))]
        public void ClubsPerDivision_StayConstant(ulong seed)
        {
            var g = CareerTestData.Db().World.Generation;
            foreach (var r in Career(seed))
            {
                foreach (var l in r.Season.Leagues)
                    Assert.AreEqual(g.ClubsPerDivision, l.ClubIds.Count, $"seed={seed} year={r.Season.Year} division={l.DivisionName}");
                Assert.AreEqual(g.Divisions.Count * g.ClubsPerDivision, r.Season.Leagues.SelectMany(l => l.ClubIds).Distinct().Count(), $"seed={seed}");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void PromotionAndRelegation_MatchTheFinalTables(ulong seed)
        {
            var league = CareerTestData.Db().Competitions.League;
            var facilities = CareerTestData.Db().Facilities;
            var records = Career(seed);
            for (int s = 0; s + 1 < records.Count; s++)
            {
                var tables = records[s].Summary.FinalTables;
                var stadium = records[s].StadiumLevelAtEnd;
                var next = records[s + 1].DivisionAtStart;
                string ctx = $"seed={seed} year={records[s].Season.Year}";

                // Division d-1's promotion set: the top eligible (stadium requirement, B7 X-45) clubs of division d,
                // falling back to the best-ranked ineligible ones only if not enough are eligible (same cascade as
                // CareerSimulator.SeasonTransition; the number of clubs per division never changes).
                var expectedPromoted = new List<Id>[tables.Count];
                for (int d = 1; d < tables.Count; d++)
                {
                    int minStadium = Game.Rules.Board.BoardRules.StadiumMinimumLevel(facilities, d - 1);
                    // Never dip into tables[d]'s own relegation zone (mirrors CareerSimulator.SeasonTransition).
                    int candidatePoolSize = d < tables.Count - 1 ? tables[d].Count - league.Relegated : tables[d].Count;
                    var eligible = new List<Id>();
                    var ineligible = new List<Id>();
                    for (int i = 0; i < candidatePoolSize; i++)
                    {
                        var id = tables[d][i];
                        (stadium[id] >= minStadium ? eligible : ineligible).Add(id);
                    }
                    var chosen = new List<Id>();
                    chosen.AddRange(eligible.Take(league.Promoted));
                    if (chosen.Count < league.Promoted) chosen.AddRange(ineligible.Take(league.Promoted - chosen.Count));
                    expectedPromoted[d] = chosen;
                }

                for (int d = 0; d < tables.Count; d++)
                {
                    var t = tables[d];
                    for (int pos = 0; pos < t.Count; pos++)
                    {
                        int expected = d;
                        if (d > 0 && expectedPromoted[d].Contains(t[pos])) expected = d - 1;
                        if (d < tables.Count - 1 && pos >= t.Count - league.Relegated) expected = d + 1;
                        Assert.AreEqual(expected, next[t[pos]], $"{ctx} division={d} position={pos + 1}");
                    }
                }
                Assert.AreEqual(league.Promoted * (tables.Count - 1), records[s].Summary.Promoted.Count, ctx);
                var allExpectedPromoted = new List<Id>();
                for (int d = 1; d < tables.Count; d++) allExpectedPromoted.AddRange(expectedPromoted[d]);
                CollectionAssert.AreEquivalent(records[s].Summary.Promoted, allExpectedPromoted, ctx);
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void FinalTable_IsOrderedByPoints_AndChampionHasMostPoints(ulong seed)
        {
            var league = CareerTestData.Db().Competitions.League;
            foreach (var r in Career(seed))
                foreach (var l in r.Season.Leagues)
                {
                    var table = Standings.Table(league, l);
                    for (int i = 1; i < table.Count; i++) Assert.GreaterOrEqual(table[i - 1].Points, table[i].Points, $"seed={seed}");
                    CollectionAssert.AreEqual(r.Summary.FinalTables[l.DivisionIndex], table.Select(x => x.ClubId), $"seed={seed}");
                    Assert.AreEqual(table.Sum(x => x.GoalsFor), table.Sum(x => x.GoalsAgainst), $"seed={seed}");
                }
        }

        [TestCaseSource(nameof(Seeds))]
        public void LeagueCalendar_EveryClubPlaysEveryOpponent_HomeAndAway(ulong seed)
        {
            foreach (var r in Career(seed))
                foreach (var l in r.Season.Leagues)
                {
                    string ctx = $"seed={seed} year={r.Season.Year} division={l.DivisionName}";
                    int n = l.ClubIds.Count;
                    Assert.AreEqual(n * (n - 1), l.Fixtures.Count, ctx);
                    Assert.IsTrue(l.Fixtures.All(f => f.Played), ctx + " all played");
                    var pairs = new HashSet<(Id, Id)>(l.Fixtures.Select(f => (f.HomeClubId, f.AwayClubId)));
                    Assert.AreEqual(n * (n - 1), pairs.Count, ctx + " every ordered pair exactly once");
                    foreach (var c in l.ClubIds)
                    {
                        Assert.AreEqual(n - 1, l.Fixtures.Count(f => f.HomeClubId == c), ctx);
                        Assert.AreEqual(n - 1, l.Fixtures.Count(f => f.AwayClubId == c), ctx);
                    }
                    Assert.IsTrue(l.Fixtures.All(f => f.Date.Year == r.Season.Year), ctx);
                }
        }

        [TestCaseSource(nameof(Seeds))]
        public void Calendar_NoClubPlaysTwiceOnADate_AndMinimumIntervalHolds(ulong seed)
        {
            int minDays = CareerTestData.Db().Calendar.MinDaysBetweenMatches;
            foreach (var r in Career(seed))
            {
                var byClub = new Dictionary<Id, List<DateTime>>();
                foreach (var f in r.Season.AllFixtures())
                    foreach (var c in new[] { f.HomeClubId, f.AwayClubId })
                    {
                        if (!byClub.TryGetValue(c, out var dates)) byClub[c] = dates = new List<DateTime>();
                        dates.Add(f.Date);
                    }
                foreach (var kv in byClub)
                {
                    var dates = kv.Value.OrderBy(d => d).ToList();
                    for (int i = 1; i < dates.Count; i++)
                        Assert.GreaterOrEqual((dates[i] - dates[i - 1]).TotalDays, minDays, $"seed={seed} year={r.Season.Year} club={kv.Key} {dates[i - 1]:d}/{dates[i]:d}");
                }
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void Calendar_HasWindowsInJanuaryAndJuly_AndEndsWithSeasonEnd(ulong seed)
        {
            foreach (var r in Career(seed))
            {
                var cal = r.Season.Calendar;
                Assert.AreEqual(CalendarEntryKind.SeasonEnd, cal[cal.Count - 1].Kind, $"seed={seed}");
                for (int i = 1; i < cal.Count; i++) Assert.GreaterOrEqual(cal[i].Date, cal[i - 1].Date, $"seed={seed} calendar in date order");
                var opens = cal.Where(e => e.Kind == CalendarEntryKind.TransferWindowOpen).Select(e => e.Date.Month).ToList();
                CollectionAssert.AreEquivalent(new[] { 1, 7 }, opens, $"seed={seed} (GAME_DESIGN §4)");
                Assert.IsTrue(r.Season.Finished, $"seed={seed}");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void Cup_QualifiesEightPerDivision_AndProducesOneChampion(ulong seed)
        {
            var db = CareerTestData.Db();
            var cupDef = db.Competitions.Cup;
            var records = Career(seed);
            for (int s = 0; s < records.Count; s++)
            {
                var r = records[s];
                var cup = r.Season.Cup;
                string ctx = $"seed={seed} year={r.Season.Year}";
                Assert.AreEqual(cupDef.Clubs, cup.Qualified.Count, ctx);
                CollectionAssert.AllItemsAreUnique(cup.Qualified, ctx);

                // X-40: previous season's top 8 per division (first season: squad OVR is checked in the simulator).
                if (s > 0)
                    CollectionAssert.AreEquivalent(records[s - 1].Summary.FinalTables.SelectMany(t => t.Take(cupDef.QualifiersPerDivision)), cup.Qualified, ctx);
                else
                    foreach (var g in cup.Qualified.GroupBy(id => r.DivisionAtStart[id]))
                        Assert.AreEqual(cupDef.QualifiersPerDivision, g.Count(), ctx);

                Assert.AreEqual(cup.RoundCount, cup.Rounds.Count, ctx);
                var inRound = new HashSet<Id>(cup.Qualified);
                for (int k = 0; k < cup.Rounds.Count; k++)
                {
                    var round = cup.Rounds[k];
                    Assert.AreEqual(inRound.Count / 2, round.Count, ctx);
                    CollectionAssert.AreEquivalent(inRound, round.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }), ctx);
                    foreach (var f in round)
                    {
                        Assert.IsFalse(f.Winner.IsNone, ctx + " knockout always has a winner");
                        if (f.HomeGoals == f.AwayGoals) Assert.IsTrue(f.HomePenalties.HasValue, ctx + " draws go to penalties");
                        else Assert.IsFalse(f.HomePenalties.HasValue, ctx);
                        bool isFinal = k == cup.Rounds.Count - 1;
                        Assert.AreEqual(isFinal && cupDef.FinalNeutral, f.NeutralVenue, ctx);
                        if (!isFinal) Assert.LessOrEqual(r.DivisionAtStart[f.AwayClubId], r.DivisionAtStart[f.HomeClubId], ctx + " lower division hosts");
                    }
                    inRound = new HashSet<Id>(round.Select(f => f.Winner));
                }
                Assert.AreEqual(1, inRound.Count, ctx);
                Assert.AreEqual(inRound.Single(), r.Summary.CupWinner, ctx);
            }
        }

        // ---------------- B4: players in the career ----------------

        [TestCaseSource(nameof(Seeds))]
        public void NobodyEverPassesPotential_AndNobodyPlaysForever(ulong seed)
        {
            var last = CareerTestData.Db().Development.RetirementByAge.Last().Age;
            foreach (var r in Career(seed))
            {
                Assert.AreEqual(0, r.PlayersAbovePotential, $"seed={seed} year={r.Season.Year}");
                Assert.Less(r.OldestAge, last + 1, $"seed={seed} year={r.Season.Year}: retirement at {last} is certain");
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void Squads_StayAboveTheFloor_WithGoalkeepers_OnEveryMatchday(ulong seed)
        {
            Career(seed);
            Assert.IsEmpty(FloorBreaches[seed], $"seed={seed}: " + string.Join(", ", FloorBreaches[seed].Take(10)));
        }

        [TestCaseSource(nameof(Seeds))]
        public void EveryClub_ReceivesTheYearlyYouthIntake(ulong seed)
        {
            // Counted from the YouthIntakeHappened event, not from squad membership: a created youth can leave the
            // same year (e.g. sold abroad, B5 X-43), which the old squad-diff check would have missed as a non-intake.
            var d = CareerTestData.Db().Development;
            var records = Career(seed);
            var counts = YouthIntakeCounts[seed];
            var clubIds = records[0].DivisionAtStart.Keys;
            for (int s = 1; s < records.Count; s++)
            {
                int year = records[s].Season.Year;
                foreach (var clubId in clubIds)
                {
                    int n = counts.TryGetValue((year, clubId), out var c) ? c : 0;
                    Assert.GreaterOrEqual(n, d.YouthPerClubPerYear, $"seed={seed} year={year} club={clubId}");
                }
            }
        }

        [TestCaseSource(nameof(Seeds))]
        public void InjuredOrSuspendedPlayers_AreNotSelected(ulong seed)
        {
            Career(seed);
            Assert.IsEmpty(Violations[seed], $"seed={seed}: " + string.Join(", ", Violations[seed].Take(10)));
        }

        [Test]
        public void TenSeasons_OvrNeitherExplodesNorCollapses()
        {
            var cfg = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(CareerTestData.DataRoot(), "TestRanges", "development.json")))["careerStability"];
            ulong seed = cfg["seed"].Value<ulong>();
            var db = CareerTestData.Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate, new Game.Career.Market.TransferWindow(), new Game.Career.Economy.EconomySystem());
            var w = career.State.World;
            int top = cfg["top"].Value<int>();
            double[] Top() => w.DivisionNames.Select((_, d) => w.Clubs.Where(c => c.DivisionIndex == d)
                .Average(c => w.SquadOf(c.Id).Select(p => (double)Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition))
                    .OrderByDescending(x => x).Take(top).Average())).ToArray();
            var start = Top();
            double maxDrift = cfg["maxTop22DriftPerDivision"].Value<double>();
            for (int s = 0; s < cfg["seasons"].Value<int>(); s++)
            {
                career.PlaySeason();
                var now = Top();
                for (int d = 0; d < now.Length; d++)
                {
                    Assert.That(Math.Abs(now[d] - start[d]), Is.LessThanOrEqualTo(maxDrift), $"seed={seed} season={s + 1} division={d} start={start[d]:0.0} now={now[d]:0.0}");
                    if (d > 0) Assert.Greater(now[d - 1], now[d], $"seed={seed}: divisions stay ordered");
                }
            }
        }

        [Test]
        public void SameSeed_ProducesTheSameCareer()
        {
            var db = CareerTestData.Db();
            string Run()
            {
                var career = CareerSimulator.Start(db, 99, new Game.Simulation.QuickSim.QuickSim(db).Simulate, new Game.Career.Market.TransferWindow(), new Game.Career.Economy.EconomySystem());
                var a = career.PlaySeason();
                var b = career.PlaySeason();
                return string.Join("|", new[] { a, b }.Select(x =>
                    string.Join(";", x.FinalTables.Select(t => string.Join(",", t))) + "#" + x.CupWinner));
            }
            Assert.AreEqual(Run(), Run(), "seed=99");
        }
    }
}
