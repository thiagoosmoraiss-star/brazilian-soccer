using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Tests.Simulation;
using Newtonsoft.Json.Linq;

namespace Game.Tools.MatchReport
{
    /// <summary>
    /// --calibration (C2): the same matchups in the QuickSim and in the played match (Data/TestRanges/calibration.json),
    /// side by side; --sensitivity adds the +boost per attribute in both engines (slow: 18 extra batches each).
    /// </summary>
    internal static class CalibrationReport
    {
        /// <summary><paramref name="minutesOverride"/> (--calibration-minutes): measure at another duration than the batch's
        /// (D-34: the batch stays at 6 min until the end of F1b; F1b measures the game default, 4 min, with it).</summary>
        public static int Run(GameDatabase db, string dataRoot, int? playedOverride, int? quickOverride, bool sensitivity, bool onlyGroups = false,
            int? minutesOverride = null)
        {
            var cfg = JObject.Parse(File.ReadAllText(Path.Combine(dataRoot, "TestRanges", "calibration.json")));
            var batch = cfg["batch"];
            int played = playedOverride ?? batch["playedMatches"].Value<int>();
            int quick = Math.Max(played, quickOverride ?? batch["quickSimMatches"].Value<int>());
            double width = cfg["ovrCurve"]["bucketWidth"].Value<double>();
            int buckets = cfg["ovrCurve"]["bucketCount"].Value<int>();
            int minutes = minutesOverride ?? batch["durationMinutes"].Value<int>();

            var sw = Stopwatch.StartNew();
            var worlds = batch["worldSeeds"].Values<ulong>().Select(s => WorldGenerator.Generate(db, s)).ToList();
            var matchups = Calibration.Matchups(db, worlds, quick, batch["batchSeed"].Value<ulong>(), batch["crossDivisionShare"].Value<double>(),
                batch["neutralVenue"].Value<bool>(), minutes);
            var same = matchups.Take(played).ToList();
            Console.WriteLine($"Calibration (C2): {played} matchups in both engines (+{quick - played} more in the QuickSim), "
                              + $"{minutes} min played (game default {db.Competitions.MatchRules.DurationMinutes}); setup {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            var quickAll = Calibration.QuickSim(db, Calibration.Setups(matchups));
            long quickMs = sw.ElapsedMilliseconds;
            sw.Restart();
            var playedResults = Calibration.Played(db, Calibration.Setups(same));
            long playedMs = sw.ElapsedMilliseconds;
            Console.WriteLine($"  time: QuickSim {quick} in {quickMs / 1000.0:F1} s, played {played} in {playedMs / 1000.0:F1} s "
                              + $"({playedMs / (double)played:F0} ms/match wall, parallel)");

            var p = Calibration.Measure(same, playedResults, width, buckets);
            var qs = Calibration.Measure(same, quickAll.Take(played).ToList(), width, buckets);
            var qa = Calibration.Measure(matchups, quickAll, width, buckets);
            Console.WriteLine();
            Console.WriteLine($"  {"metric",-24} {"played",9} {"QuickSim",9} {"ratio",7} {"QS all",9}");
            foreach (var name in Calibration.MetricNames)
            {
                double ratio = qs[name] == 0 ? 0 : p[name] / qs[name];
                Console.WriteLine($"  {name,-24} {p[name],9:0.00} {qs[name],9:0.00} {ratio,7:0.00} {qa[name],9:0.00}");
            }
            var target = JObject.Parse(File.ReadAllText(Path.Combine(dataRoot, "TestRanges", "match_targets.json")))["durations"]
                ?[minutes.ToString(CultureInfo.InvariantCulture)];
            if (target != null)
            {
                // X-111 (D-22b): experimental references, not tolerances; the user's shots are one side's (half the match's).
                string Range(string key) => $"{target[key][0].Value<double>():0.0}-{target[key][1].Value<double>():0.0}";
                Console.WriteLine($"  experimental target (match_targets.json): total goals {Range("totalGoals")} (played {p["goalsPerMatch"]:0.00}), "
                                  + $"user shots {Range("userShots")} (played {p["shotsPerMatch"] / 2:0.00} per side)");
            }
            Console.WriteLine();
            Console.WriteLine("  |OVR diff| -> stronger side W/D/L %, wins among decided, possession (played | QuickSim same | QuickSim all)");
            for (int b = 0; b < buckets; b++)
            {
                string Row(Calibration.Bucket x) => x.N == 0 ? "                  -                   " : $"n={x.N,4} {x.WinPct,3:0}/{x.DrawPct,3:0}/{x.LossPct,3:0} dec {x.DecidedWinPct,3:0}% pos {x.Possession,3:0}%";
                var pb = p.Buckets[b];
                string range = double.IsPositiveInfinity(pb.To) ? $"{pb.From:0}+" : $"{pb.From:0}-{pb.To:0}";
                Console.WriteLine($"  {range,-6} {Row(pb)} | {Row(qs.Buckets[b])} | {Row(qa.Buckets[b])}");
            }

            if (!sensitivity) return 0;
            var sc = cfg["sensitivity"];
            int boost = sc["boost"].Value<int>();
            int sp = Math.Min(played, sc["playedMatches"].Value<int>());
            int sq = Math.Min(quick, sc["quickSimMatches"].Value<int>());
            var pSetups = Calibration.Setups(same.Take(sp));
            var qSetups = Calibration.Setups(matchups.Take(sq));
            var pBase = playedResults.Take(sp).ToList();
            var qBase = quickAll.Take(sq).ToList();
            Console.WriteLine();
            Console.WriteLine($"  Sensitivity: home side +{boost}, goal difference change (played n={sp} | QuickSim n={sq})");
            sw.Restart();
            foreach (Attr attr in Enum.GetValues(typeof(Attr)))
            {
                if (onlyGroups) break;
                var a = Calibration.Measure(s => Calibration.Played(db, s), pSetups, pBase, attr, boost);
                var b = Calibration.Measure(s => Calibration.QuickSim(db, s), qSetups, qBase, attr, boost);
                Console.WriteLine($"  {attr,-20} played {a.Mean,6:+0.000;-0.000} (z {a.Z,5:0.0})   QuickSim {b.Mean,6:+0.000;-0.000} (z {b.Z,5:0.0})");
            }
            var all = (Attr[])Enum.GetValues(typeof(Attr));
            var groups = new List<(string, Attr[], int)> { ("(+0, control)", all, 0) };
            foreach (var g in ((JObject)sc["groups"]).Properties())
                groups.Add(("group " + g.Name, g.Value["attributes"].Values<string>().Select(x => (Attr)Enum.Parse(typeof(Attr), x)).ToArray(), boost));
            foreach (var (label, attrs, k) in groups)
            {
                var a = Calibration.Measure(s => Calibration.Played(db, s), pSetups, pBase, attrs, k);
                var b = Calibration.Measure(s => Calibration.QuickSim(db, s), qSetups, qBase, attrs, k);
                Console.WriteLine($"  {label,-20} played {a.Mean,6:+0.000;-0.000} (z {a.Z,5:0.0})   QuickSim {b.Mean,6:+0.000;-0.000} (z {b.Z,5:0.0})");
            }
            Console.WriteLine($"  sensitivity time: {sw.ElapsedMilliseconds / 1000.0:F0} s");
            return 0;
        }
    }
}
