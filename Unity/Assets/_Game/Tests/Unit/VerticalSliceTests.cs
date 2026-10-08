using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Match.AI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Game.Tests.Unit.KickTestSupport;

namespace Game.Tests.Unit
{
    /// <summary>A7a: the vertical slice's teams and headless report (TECHNICAL_SPEC §19). Ranges in
    /// Data/TestRanges/vertical_slice.json.</summary>
    public class VerticalSliceTests
    {
        private static JObject _ranges;
        private static JObject R() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "TestRanges", "vertical_slice.json"))));

        private static VerticalSliceDefinition Teams()
        {
            var r = GameDataLoader.LoadVerticalSlice(new DirectoryDataSource(TestPaths.DataRoot()), Db());
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        [Test]
        public void Teams_AreStrongAndWeak_AsSpecified()
        {
            var vs = Teams();
            foreach (var key in new[] { "strong", "weak" })
            {
                var team = vs.Team(key);
                Assert.IsNotNull(team, key);
                Assert.AreEqual(11, team.Players.Count);
                double outfield = team.Players.Where(p => p.Role != FormationRole.GK).Average(p => p.Attributes.Take((int)Attr.GkReflexes).Average());
                double keeper = team.Players.Single(p => p.Role == FormationRole.GK).Attributes.Skip((int)Attr.GkReflexes).Average();
                var range = R()["teams"][key];
                Assert.That(outfield, Is.InRange(range[0].Value<double>(), range[1].Value<double>()), $"{key} outfield level");
                Assert.That(keeper, Is.InRange(range[0].Value<double>(), range[1].Value<double>()), $"{key} keeper level");
            }
        }

        [Test]
        public void TeamsFile_RejectsARoleThatDoesNotMatchTheFormation()
        {
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "VerticalSlice", "teams.json"));
            int first = json.IndexOf("\"role\": \"GK\"");
            var broken = json.Substring(0, first) + "\"role\": \"ST\"" + json.Substring(first + "\"role\": \"GK\"".Length);
            Assert.IsFalse(VerticalSliceReader.Read(broken, Db()).IsSuccess);
        }

        [Test, Category("Slow")]
        public void Headless_StrongIsClearlyAhead_WithSomeUpsets()
        {
            var b = R()["batch"];
            var vs = Teams();
            var r = HeadlessReport.Run(Db(), vs.Team("strong").ToSetup(), vs.Team("weak").ToSetup(), b["matches"].Value<int>(),
                b["minutes"].Value<int>(), b["seed"].Value<ulong>(), Dt);
            double strongPoints = (3.0 * r.AWins + r.Draws) / r.Matches;
            TestContext.WriteLine($"strong {r.AWins} / draws {r.Draws} / weak {r.BWins}; goals {r.PerMatch(r.AGoals):F2} x {r.PerMatch(r.BGoals):F2}");
            Assert.GreaterOrEqual(strongPoints - r.BPointsPerMatch, b["minPointsGap"].Value<double>(), "points per match gap");
            Assert.GreaterOrEqual(r.AWins, b["minWinRatio"].Value<double>() * r.BWins, "the strong side wins far more often");
            Assert.GreaterOrEqual(r.BWins, b["minUpsets"].Value<int>(), "TECHNICAL_SPEC §19: algumas zebras.");
        }

        [Test, Category("Slow")]
        public void Headless_FasterWeakSide_DoesBetter()
        {
            var s = R()["sensitivity"];
            var b = R()["batch"];
            var vs = Teams();
            int n = s["matches"].Value<int>(), delta = s["delta"].Value<int>();
            var strong = vs.Team("strong").ToSetup();
            var baseline = HeadlessReport.Run(Db(), strong, vs.Team("weak").ToSetup(), n, b["minutes"].Value<int>(), b["seed"].Value<ulong>(), Dt);
            var faster = HeadlessReport.Run(Db(), strong, vs.Team("weak").ToSetup(new[] { (Attr.Speed, delta), (Attr.Agility, delta) }), n,
                b["minutes"].Value<int>(), b["seed"].Value<ulong>(), Dt);
            TestContext.WriteLine($"weak points per match {baseline.BPointsPerMatch:F2} → {faster.BPointsPerMatch:F2}");
            Assert.GreaterOrEqual(faster.BPointsPerMatch - baseline.BPointsPerMatch, s["minPointsGain"].Value<double>(),
                "TECHNICAL_SPEC §19: mudar Velocidade move o relatório na direção esperada.");
        }
    }
}
