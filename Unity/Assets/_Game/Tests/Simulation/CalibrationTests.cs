using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// C2 acceptance (ROADMAP; TEST_PLAN "Divergência QuickSim × Headless", blocks merge): the same matchups in the
    /// QuickSim and in the played match stay within the tolerances of Data/TestRanges/calibration.json. Each metric
    /// declares how it is compared ("ratio" played/QuickSim, "difference" played − QuickSim, or "played" / "quickSim"
    /// alone; "ratioAllQuickSim" against the QuickSim of all its matchups, for a rare event like injuries); the OVR curve and the attribute sensitivity must point the same way in both engines.
    /// </summary>
    [Category("Slow")]
    public class CalibrationTests
    {
        private static JObject _cfg;
        private static List<Calibration.Matchup> _matchups;
        private static MatchResult[] _played, _quick;

        private static JObject Cfg() =>
            _cfg ?? (_cfg = JObject.Parse(File.ReadAllText(Path.Combine(SimTestData.DataRoot(), "TestRanges", "calibration.json"))));

        private static string SeedInfo => $"(batchSeed={Cfg()["batch"]["batchSeed"]}, worlds {string.Join(",", Cfg()["batch"]["worldSeeds"])})";

        /// <summary>Matchups (QuickSim count); the first playedMatches of them also in the played match.</summary>
        private static void Batch()
        {
            if (_matchups != null) return;
            var b = Cfg()["batch"];
            var db = SimTestData.Db();
            var worlds = b["worldSeeds"].Values<ulong>().Select(SimTestData.World).ToList();
            int played = b["playedMatches"].Value<int>();
            int quick = Math.Max(played, b["quickSimMatches"].Value<int>());
            _matchups = Calibration.Matchups(db, worlds, quick, b["batchSeed"].Value<ulong>(), b["crossDivisionShare"].Value<double>(),
                b["neutralVenue"].Value<bool>(), b["durationMinutes"].Value<int>());
            _quick = Calibration.QuickSim(db, Calibration.Setups(_matchups));
            _played = Calibration.Played(db, Calibration.Setups(_matchups.Take(played)));
        }

        private static (double Width, int Count) Buckets() =>
            (Cfg()["ovrCurve"]["bucketWidth"].Value<double>(), Cfg()["ovrCurve"]["bucketCount"].Value<int>());

        /// <summary>Played metrics and QuickSim metrics on the same matchups.</summary>
        private static (Calibration.Metrics Played, Calibration.Metrics Quick) Metrics()
        {
            Batch();
            var (w, c) = Buckets();
            var same = _matchups.Take(_played.Length).ToList();
            return (Calibration.Measure(same, _played, w, c), Calibration.Measure(same, _quick.Take(_played.Length).ToList(), w, c));
        }

        public static IEnumerable<string> ToleranceNames() =>
            ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(SimTestData.DataRoot(), "TestRanges", "calibration.json")))["tolerances"])
            .Properties().Select(p => p.Name);

        [Test]
        public void EveryToleranceIsAKnownMetric()
        {
            foreach (var name in ToleranceNames())
                Assert.Contains(name, Calibration.MetricNames, $"calibration.json tolerance '{name}' is not a calibration metric.");
        }

        [TestCaseSource(nameof(ToleranceNames))]
        public void MetricWithinTolerance(string name)
        {
            var t = Cfg()["tolerances"][name];
            string compare = t["compare"].Value<string>();
            double min = t["range"][0].Value<double>(), max = t["range"][1].Value<double>();
            var (p, q) = Metrics();
            double value;
            switch (compare)
            {
                case "ratio": value = q[name] == 0 ? double.NaN : p[name] / q[name]; break;
                case "difference": value = p[name] - q[name]; break;
                case "played": value = p[name]; break;
                case "quickSim": value = q[name]; break;
                case "ratioAllQuickSim":
                    var (w, c) = Buckets();
                    var all = Calibration.Measure(_matchups, _quick, w, c);
                    value = all[name] == 0 ? double.NaN : p[name] / all[name];
                    q = all;
                    break;
                default: throw new ArgumentException($"Unknown compare '{compare}' for {name}.");
            }
            Assert.That(value, Is.InRange(min, max),
                $"{name} {compare} = {value:0.###} (played {p[name]:0.###}, QuickSim {q[name]:0.###}, n={p.Matches}) {SeedInfo}");
        }

        /// <summary>
        /// Curve |OVR difference| → stronger side's wins: rises in both engines (each bucket at most monotonicTolerancePct
        /// below the previous one, and the last bucket at least minRisePct above the first), and per bucket the wins among
        /// decided matches stay within maxDecidedWinPctGap of the QuickSim (all its matchups, for a steadier reference).
        /// </summary>
        [Test]
        public void OvrCurve_SameShapeInBothEngines()
        {
            var c = Cfg()["ovrCurve"];
            int minN = c["minMatchesPerBucket"].Value<int>();
            double drop = c["monotonicTolerancePct"].Value<double>();
            double rise = c["minRisePct"].Value<double>();
            double maxGap = c["maxDecidedWinPctGap"].Value<double>();
            var (p, _) = Metrics();
            var (w, n) = Buckets();
            var q = Calibration.Measure(_matchups, _quick, w, n);
            var used = new List<int>();
            for (int i = 0; i < p.Buckets.Count; i++)
                if (p.Buckets[i].N >= minN) used.Add(i);
            string rows = string.Join("; ", used.Select(i =>
                $"{p.Buckets[i].From:0}+: played {p.Buckets[i].WinPct:0}% (decided {p.Buckets[i].DecidedWinPct:0}%, n={p.Buckets[i].N}) "
                + $"QuickSim {q.Buckets[i].WinPct:0}% (decided {q.Buckets[i].DecidedWinPct:0}%, n={q.Buckets[i].N})"));
            TestContext.Progress.WriteLine("OVR curve: " + rows);
            Assert.GreaterOrEqual(used.Count, c["minBuckets"].Value<int>(), $"not enough buckets with {minN}+ matches: {rows} {SeedInfo}");
            for (int k = 0; k < used.Count; k++)
            {
                var bp = p.Buckets[used[k]];
                var bq = q.Buckets[used[k]];
                if (k > 0)
                {
                    Assert.GreaterOrEqual(bp.WinPct, p.Buckets[used[k - 1]].WinPct - drop, $"played curve not rising at {bp.From:0}+: {rows} {SeedInfo}");
                    Assert.GreaterOrEqual(bq.WinPct, q.Buckets[used[k - 1]].WinPct - drop, $"QuickSim curve not rising at {bp.From:0}+: {rows} {SeedInfo}");
                }
                Assert.LessOrEqual(Math.Abs(bp.DecidedWinPct - bq.DecidedWinPct), maxGap, $"decided-win gap at {bp.From:0}+: {rows} {SeedInfo}");
            }
            Assert.GreaterOrEqual(p.Buckets[used[used.Count - 1]].WinPct - p.Buckets[used[0]].WinPct, rise, $"played curve too flat: {rows} {SeedInfo}");
            Assert.GreaterOrEqual(q.Buckets[used[used.Count - 1]].WinPct - q.Buckets[used[0]].WinPct, rise, $"QuickSim curve too flat: {rows} {SeedInfo}");
        }

        /// <summary>
        /// Per attribute, QuickSim: the home side with +boost in it gains goal difference on the calibration matchups
        /// (z ≥ quickSimMinZ; Resistência by its starters' final energy, as quicksim.json). The played match's direction per attribute is SensitivityTests (X-81): one attribute alone
        /// moves a whole played match too little to measure in a few hundred matches (X-86).
        /// </summary>
        [TestCaseSource(nameof(Attributes))]
        public void Sensitivity_QuickSim_EveryAttribute(Attr attr)
        {
            Batch();
            var s = Cfg()["sensitivity"];
            int boost = s["boost"].Value<int>();
            int nq = Math.Min(_quick.Length, s["quickSimMatches"].Value<int>());
            var db = SimTestData.Db();
            string metric = s["metric"]?[attr.ToString()]?.Value<string>() ?? "goalDifference";
            Func<MatchSetup, MatchResult, double> f;
            switch (metric)
            {
                case "goalDifference": f = Calibration.GoalDifference; break;
                case "finalEnergy": f = Calibration.HomeStartersFinalEnergy; break;
                default: throw new ArgumentException($"Unknown sensitivity metric '{metric}' for {attr}.");
            }
            var quick = Calibration.Measure(x => Calibration.QuickSim(db, x), Calibration.Setups(_matchups.Take(nq)), _quick.Take(nq).ToList(),
                new[] { attr }, boost, f);
            string info = $"{attr} +{boost}: QuickSim {metric} {quick.Mean:+0.000;-0.000} (z {quick.Z:0.0}, n={nq}) {SeedInfo}";
            TestContext.Progress.WriteLine(info);
            Assert.GreaterOrEqual(quick.Z, s["quickSimMinZ"].Value<double>(), info);
        }

        public static IEnumerable<Attr> Attributes() => Enum.GetValues(typeof(Attr)).Cast<Attr>();

        /// <summary>
        /// Per attribute group, both engines, whole matches: the home side with +boost in the group gains goal difference,
        /// z ≥ quickSimMinZ in the QuickSim and z ≥ the group's playedMinZ in the played match.
        /// </summary>
        [TestCaseSource(nameof(Groups))]
        public void Sensitivity_Group_SameDirectionInBothEngines(string group)
        {
            Batch();
            var s = Cfg()["sensitivity"];
            var g = s["groups"][group];
            var attrs = g["attributes"].Values<string>().Select(a => (Attr)Enum.Parse(typeof(Attr), a)).ToArray();
            int boost = s["boost"].Value<int>();
            int np = Math.Min(_played.Length, s["playedMatches"].Value<int>());
            int nq = Math.Min(_quick.Length, s["quickSimMatches"].Value<int>());
            var db = SimTestData.Db();
            var played = Calibration.Measure(x => Calibration.Played(db, x), Calibration.Setups(_matchups.Take(np)), _played.Take(np).ToList(), attrs, boost);
            var quick = Calibration.Measure(x => Calibration.QuickSim(db, x), Calibration.Setups(_matchups.Take(nq)), _quick.Take(nq).ToList(), attrs, boost);
            string info = $"group {group} +{boost}: played {played.Mean:+0.000;-0.000} (z {played.Z:0.0}, n={np}), "
                          + $"QuickSim {quick.Mean:+0.000;-0.000} (z {quick.Z:0.0}, n={nq}) {SeedInfo}";
            TestContext.Progress.WriteLine(info);
            Assert.GreaterOrEqual(quick.Z, s["quickSimMinZ"].Value<double>(), info);
            Assert.GreaterOrEqual(played.Z, g["playedMinZ"].Value<double>(), info);
        }

        public static IEnumerable<string> Groups() =>
            ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(SimTestData.DataRoot(), "TestRanges", "calibration.json")))["sensitivity"]["groups"])
            .Properties().Select(p => p.Name);

        /// <summary>Every attribute is in at least one group, so each one is checked in the played match too.</summary>
        [Test]
        public void EveryAttribute_IsInAGroup()
        {
            var groups = (JObject)Cfg()["sensitivity"]["groups"];
            var covered = new HashSet<string>(groups.Properties().Where(p => p.Name != "all").SelectMany(p => p.Value["attributes"].Values<string>()));
            foreach (Attr a in Enum.GetValues(typeof(Attr)))
                Assert.IsTrue(covered.Contains(a.ToString()), $"{a} is in no attribute group of calibration.json");
        }

        /// <summary>The parallel batch is deterministic: the same setup gives the same result as a lone run.</summary>
        [Test]
        public void ParallelBatch_MatchesSequentialRuns()
        {
            Batch();
            var db = SimTestData.Db();
            for (int i = 0; i < 3; i++)
            {
                var alone = Calibration.Played(db, new[] { _matchups[i].Setup })[0];
                Assert.AreEqual((_played[i].HomeGoals, _played[i].AwayGoals, _played[i].HomeStats.Shots, _played[i].AwayStats.Fouls),
                    (alone.HomeGoals, alone.AwayGoals, alone.HomeStats.Shots, alone.AwayStats.Fouls), $"matchup {i} {SeedInfo}");
            }
        }
    }
}
