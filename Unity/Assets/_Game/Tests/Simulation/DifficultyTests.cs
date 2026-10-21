using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Contracts.Match;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// F1c acceptance (IMPLEMENTATION_PLAN): with the same matchups and the same simulated player (the user's side, home, is
    /// the reference AI), the user's opponent does better the higher the level, in the played match and in the QuickSim;
    /// neighbouring levels differ significantly (paired z of the opponent's goal difference, Data/TestRanges/difficulty.json).
    /// </summary>
    [Category("Slow")]
    public class DifficultyTests
    {
        private static JObject _cfg;
        private static List<MatchSetup> _setups;
        private static readonly Dictionary<string, MatchResult[]> Played = new Dictionary<string, MatchResult[]>();
        private static readonly Dictionary<string, MatchResult[]> Quick = new Dictionary<string, MatchResult[]>();

        private static JObject Cfg() =>
            _cfg ?? (_cfg = JObject.Parse(File.ReadAllText(Path.Combine(SimTestData.DataRoot(), "TestRanges", "difficulty.json"))));

        private static string SeedInfo => $"(batchSeed={Cfg()["batch"]["batchSeed"]}, worlds {string.Join(",", Cfg()["batch"]["worldSeeds"])})";

        private static List<MatchSetup> Setups()
        {
            if (_setups != null) return _setups;
            var b = Cfg()["batch"];
            var worlds = b["worldSeeds"].Values<ulong>().Select(SimTestData.World).ToList();
            int count = Math.Max(b["playedMatches"].Value<int>(), b["quickSimMatches"].Value<int>());
            return _setups = Calibration.Setups(Calibration.Matchups(SimTestData.Db(), worlds, count, b["batchSeed"].Value<ulong>(),
                b["crossDivisionShare"].Value<double>(), b["neutralVenue"].Value<bool>(), b["durationMinutes"].Value<int>()));
        }

        private static List<MatchSetup> AtLevel(IEnumerable<MatchSetup> setups, string level) =>
            setups.Select(s => s.WithDifficulty(new MatchDifficulty(level, MatchSide.Home))).ToList();

        private static MatchResult[] Results(bool played, string level)
        {
            var cache = played ? Played : Quick;
            if (cache.TryGetValue(level, out var r)) return r;
            int n = Cfg()["batch"][played ? "playedMatches" : "quickSimMatches"].Value<int>();
            var setups = AtLevel(Setups().Take(n), level);
            return cache[level] = played ? Calibration.Played(SimTestData.Db(), setups) : Calibration.QuickSim(SimTestData.Db(), setups);
        }

        private static double OpponentGoalDifference(MatchResult r) => r.AwayGoals - r.HomeGoals;

        private static void AssertMonotonic(bool played)
        {
            var levels = SimTestData.Db().Difficulty.Levels.Select(l => l.Id).ToList();
            double minZ = Cfg()["minZ"].Value<double>();
            for (int k = 0; k + 1 < levels.Count; k++)
            {
                var lower = Results(played, levels[k]);
                var higher = Results(played, levels[k + 1]);
                int n = lower.Length;
                var d = new double[n];
                for (int i = 0; i < n; i++) d[i] = OpponentGoalDifference(higher[i]) - OpponentGoalDifference(lower[i]);
                double mean = d.Average();
                double sd = Math.Sqrt(d.Sum(x => (x - mean) * (x - mean)) / (n - 1));
                double z = mean / (sd / Math.Sqrt(n));
                TestContext.WriteLine($"{(played ? "played" : "QuickSim")} {levels[k]} -> {levels[k + 1]}: opponent goal difference "
                                      + $"{lower.Average(OpponentGoalDifference):+0.000;-0.000} -> {higher.Average(OpponentGoalDifference):+0.000;-0.000}, z {z:0.00}");
                Assert.That(z, Is.GreaterThanOrEqualTo(minZ),
                    $"{levels[k]} -> {levels[k + 1]}: the opponent must do significantly better at the higher level {SeedInfo}");
            }
        }

        [Test]
        public void Played_TheOpponentDoesBetter_TheHigherTheLevel() => AssertMonotonic(true);

        [Test]
        public void QuickSim_TheOpponentDoesBetter_TheHigherTheLevel() => AssertMonotonic(false);

        [Test]
        public void QuickSim_AnUnknownLevel_IsRejected() =>
            Assert.Throws<ArgumentException>(() => SimTestData.Sim().Simulate(AtLevel(Setups().Take(1), "nightmare")[0]));

        [Test]
        public void QuickSim_TheReferenceLevel_ReproducesTheBatchWithoutDifficulty()
        {
            int n = Cfg()["batch"]["quickSimMatches"].Value<int>();
            var none = Calibration.QuickSim(SimTestData.Db(), Setups().Take(n).ToList());
            var reference = Results(false, SimTestData.Db().Difficulty.ReferenceLevel);
            for (int i = 0; i < n; i++)
            {
                Assert.AreEqual(none[i].HomeGoals, reference[i].HomeGoals, $"match {i} {SeedInfo}");
                Assert.AreEqual(none[i].AwayGoals, reference[i].AwayGoals, $"match {i} {SeedInfo}");
                Assert.AreEqual(none[i].HomeStats.Shots, reference[i].HomeStats.Shots, $"match {i} {SeedInfo}");
                Assert.AreEqual(none[i].AwayStats.Shots, reference[i].AwayStats.Shots, $"match {i} {SeedInfo}");
            }
        }
    }
}
