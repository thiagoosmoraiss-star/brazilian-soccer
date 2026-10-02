using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private static readonly Dictionary<ulong, List<SeasonRecord>> Careers = new Dictionary<ulong, List<SeasonRecord>>();

        private static IEnumerable<ulong> Seeds() => CareerTestData.Ints("careerSeeds").Select(x => (ulong)x);

        private static List<SeasonRecord> Career(ulong seed)
        {
            if (Careers.TryGetValue(seed, out var list)) return list;
            var db = CareerTestData.Db();
            var quickSim = new Game.Simulation.QuickSim.QuickSim(db);
            var career = CareerSimulator.Start(db, seed, quickSim.Simulate);
            list = new List<SeasonRecord>();
            for (int s = 0; s < CareerTestData.Int("careerSeasons"); s++)
            {
                var record = new SeasonRecord
                {
                    Season = career.State.Season,
                    DivisionAtStart = career.State.World.Clubs.ToDictionary(c => c.Id, c => c.DivisionIndex),
                };
                record.Summary = career.PlaySeason();
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
            var records = Career(seed);
            for (int s = 0; s + 1 < records.Count; s++)
            {
                var tables = records[s].Summary.FinalTables;
                var next = records[s + 1].DivisionAtStart;
                string ctx = $"seed={seed} year={records[s].Season.Year}";
                for (int d = 0; d < tables.Count; d++)
                {
                    var t = tables[d];
                    for (int pos = 0; pos < t.Count; pos++)
                    {
                        int expected = d;
                        if (d > 0 && pos < league.Promoted) expected = d - 1;
                        if (d < tables.Count - 1 && pos >= t.Count - league.Relegated) expected = d + 1;
                        Assert.AreEqual(expected, next[t[pos]], $"{ctx} division={d} position={pos + 1}");
                    }
                }
                Assert.AreEqual(league.Promoted * (tables.Count - 1), records[s].Summary.Promoted.Count, ctx);
                CollectionAssert.AreEquivalent(records[s].Summary.Promoted, tables.Skip(1).SelectMany(t => t.Take(league.Promoted)), ctx);
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

        [Test]
        public void SameSeed_ProducesTheSameCareer()
        {
            var db = CareerTestData.Db();
            string Run()
            {
                var career = CareerSimulator.Start(db, 99, new Game.Simulation.QuickSim.QuickSim(db).Simulate);
                var a = career.PlaySeason();
                var b = career.PlaySeason();
                return string.Join("|", new[] { a, b }.Select(x =>
                    string.Join(";", x.FinalTables.Select(t => string.Join(",", t))) + "#" + x.CupWinner));
            }
            Assert.AreEqual(Run(), Run(), "seed=99");
        }
    }
}
