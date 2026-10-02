using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>formations.json, match_rules.json and quicksim.json: real files load; broken documents are rejected.</summary>
    public class MatchDataTests
    {
        [Test]
        public void RealFiles_Load()
        {
            var source = new DirectoryDataSource(TestPaths.DataRoot());
            var f = GameDataLoader.LoadFormations(source);
            Assert.IsTrue(f.IsSuccess, f.ToString());
            CollectionAssert.AreEquivalent(new[] { "4-4-2", "4-3-3", "4-2-3-1" }, System.Linq.Enumerable.Select(f.Value, x => x.Id));
            var m = GameDataLoader.LoadMatchRules(source);
            Assert.IsTrue(m.IsSuccess, m.ToString());
            var q = GameDataLoader.LoadQuickSim(source);
            Assert.IsTrue(q.IsSuccess, q.ToString());
        }

        [Test]
        public void FormationWithTenSlots_IsRejected()
        {
            const string json = "{\"schemaVersion\":1,\"formations\":[{\"id\":\"x\",\"slots\":[" +
                                "{\"position\":\"GOL\",\"role\":\"GK\",\"x\":0.1,\"y\":0.5}]}]}";
            var r = MatchDataReaders.ReadFormations(json);
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("11 slots", r.ToString());
        }

        [Test]
        public void UnknownRole_IsRejected()
        {
            const string json = "{\"schemaVersion\":1,\"formations\":[{\"id\":\"x\",\"slots\":[" +
                                "{\"position\":\"GOL\",\"role\":\"Libero\",\"x\":0.1,\"y\":0.5}]}]}";
            Assert.IsFalse(MatchDataReaders.ReadFormations(json).IsSuccess);
        }

        [Test]
        public void MatchRules_WithAnAttributeOutsideEverySector_IsRejected()
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.DataRoot(), "Balance", "match_rules.json"));
            var broken = text.Replace("\"attribute\": \"Vision\"", "\"attribute\": \"Passing\"");
            var r = MatchDataReaders.ReadMatchRules(broken);
            Assert.IsFalse(r.IsSuccess);
            StringAssert.Contains("Vision", r.ToString());
        }

        [Test]
        public void QuickSim_WithProbabilityAboveOne_IsRejected()
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.DataRoot(), "Balance", "quicksim.json"));
            var broken = text.Replace("\"blockChance\": 0.22", "\"blockChance\": 1.5");
            Assert.AreNotEqual(text, broken, "fixture edit must apply");
            Assert.IsFalse(MatchDataReaders.ReadQuickSim(broken).IsSuccess);
        }

        [Test]
        public void QuickSim_UnknownProperty_IsRejected()
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestPaths.DataRoot(), "Balance", "quicksim.json"));
            Assert.IsFalse(MatchDataReaders.ReadQuickSim(text.Replace("\"minutes\": 90", "\"minutes\": 90, \"typo\": 1")).IsSuccess);
        }
    }
}
