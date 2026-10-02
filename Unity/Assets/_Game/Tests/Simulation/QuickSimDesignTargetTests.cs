using System;
using System.Linq;
using Game.Core.Contracts.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// B2 acceptance (ROADMAP): batches of league-like QuickSim matches hit the design / real-football ranges in
    /// Data/TestRanges/quicksim.json (sources listed there). Batch seed and world seeds are fixed (deterministic).
    /// </summary>
    [Category("Slow")]
    public class QuickSimDesignTargetTests
    {
        private static double PerMatch(Func<MatchResult, double> f) => SimTestData.League().Average(m => f(m.Result));
        private static double Pct(Func<MatchResult, bool> f) => 100.0 * SimTestData.League().Count(m => f(m.Result)) / SimTestData.League().Count;

        private static void AssertRange(string name, double value)
        {
            var (min, max) = SimTestData.Range(name);
            Assert.That(value, Is.InRange(min, max), $"{name} = {value:0.###} (batchSeed={SimTestData.Ranges()["league"]["batchSeed"]})");
        }

        [Test] public void GoalsPerMatch() => AssertRange("goalsPerMatch", PerMatch(r => r.HomeGoals + r.AwayGoals));
        [Test] public void GoallessDraws() => AssertRange("zeroZeroPct", Pct(r => r.HomeGoals + r.AwayGoals == 0));
        [Test] public void HomeWins() => AssertRange("homeWinPct", Pct(r => r.HomeGoals > r.AwayGoals));
        [Test] public void Draws() => AssertRange("drawPct", Pct(r => r.HomeGoals == r.AwayGoals));
        [Test] public void AwayWins() => AssertRange("awayWinPct", Pct(r => r.HomeGoals < r.AwayGoals));
        [Test] public void Fouls() => AssertRange("foulsPerMatch", PerMatch(r => r.HomeStats.Fouls + r.AwayStats.Fouls));
        [Test] public void YellowCards() => AssertRange("yellowPerMatch", PerMatch(r => r.HomeStats.YellowCards + r.AwayStats.YellowCards));
        [Test] public void RedCards() => AssertRange("redPerMatch", PerMatch(r => r.HomeStats.RedCards + r.AwayStats.RedCards));
        [Test] public void Corners() => AssertRange("cornersPerMatch", PerMatch(r => r.HomeStats.Corners + r.AwayStats.Corners));
        [Test] public void Injuries() => AssertRange("injuriesPerMatch", PerMatch(r => r.PlayerStats.Count(p => p.Injury != InjurySeverity.None)));

        [Test]
        public void OvrDifference_DrivesResults_WithUpsets()
        {
            var cfg = SimTestData.Ranges()["ovrCurve"];
            double width = cfg["bucketWidth"].Value<double>();
            int minN = cfg["minMatchesPerBucket"].Value<int>();
            double tolerance = cfg["monotonicTolerancePct"].Value<double>();
            double upsetMin = cfg["upsetMinPct"].Value<double>();
            var even = cfg["evenBucketStrongerWinPct"];

            // Stronger side's result per |OVR difference| bucket.
            var buckets = SimTestData.League()
                .Select(m =>
                {
                    double diff = m.HomeSquadOvr - m.AwaySquadOvr;
                    int res = Math.Sign(m.Result.HomeGoals - m.Result.AwayGoals) * (diff >= 0 ? 1 : -1);
                    return (bucket: (int)(Math.Abs(diff) / width), res);
                })
                .GroupBy(x => x.bucket).Where(g => g.Count() >= minN).OrderBy(g => g.Key)
                .Select(g => (g.Key, win: 100.0 * g.Count(x => x.res > 0) / g.Count(), loss: 100.0 * g.Count(x => x.res < 0) / g.Count()))
                .ToList();

            Assert.GreaterOrEqual(buckets.Count, 3, "Not enough buckets with data.");
            Assert.That(buckets[0].win, Is.InRange(even[0].Value<double>(), even[1].Value<double>()), "even matchups");
            for (int i = 1; i < buckets.Count; i++)
                Assert.GreaterOrEqual(buckets[i].win + tolerance, buckets[i - 1].win, $"stronger-side win% must not drop (bucket {buckets[i].Key})");
            Assert.Greater(buckets[buckets.Count - 1].win, buckets[0].win + 10, "a larger OVR gap must clearly help");
            foreach (var b in buckets) Assert.GreaterOrEqual(b.loss, upsetMin, $"upsets must exist (bucket {b.Key})");
        }
    }
}
