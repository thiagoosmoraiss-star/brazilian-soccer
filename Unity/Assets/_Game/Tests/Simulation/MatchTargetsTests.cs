using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// F1a (X-111, D-22b): Data/TestRanges/match_targets.json has an experimental goal and shot target for every
    /// played-match duration the user can pick. Only the file's shape is checked: the targets are tuning references for
    /// the attacking AI (F1b), not acceptance criteria, so no measurement is asserted against them.
    /// </summary>
    public class MatchTargetsTests
    {
        private static JObject Durations() =>
            (JObject)JObject.Parse(File.ReadAllText(Path.Combine(SimTestData.DataRoot(), "TestRanges", "match_targets.json")))["durations"];

        [Test]
        public void EveryDurationOption_HasATarget_AndNothingElse()
        {
            var options = SimTestData.Db().Competitions.MatchRules.DurationOptions;
            CollectionAssert.AreEquivalent(options.Select(m => m.ToString()), Durations().Properties().Select(p => p.Name));
        }

        [TestCase("totalGoals")]
        [TestCase("userShots")]
        public void Targets_AreRanges_ThatGrowWithTheDuration(string metric)
        {
            double lastLow = 0, lastHigh = 0;
            foreach (var p in Durations().Properties().OrderBy(p => int.Parse(p.Name)))
            {
                var range = p.Value[metric].Values<double>().ToArray();
                Assert.AreEqual(2, range.Length, $"{p.Name} min {metric}");
                Assert.Greater(range[0], 0, $"{p.Name} min {metric}");
                Assert.Less(range[0], range[1], $"{p.Name} min {metric}");
                Assert.Greater(range[0], lastLow, $"{p.Name} min {metric} low");
                Assert.Greater(range[1], lastHigh, $"{p.Name} min {metric} high");
                lastLow = range[0];
                lastHigh = range[1];
            }
        }
    }
}
