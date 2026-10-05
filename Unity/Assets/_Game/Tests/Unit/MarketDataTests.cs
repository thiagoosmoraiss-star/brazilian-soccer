using System.IO;
using System.Text.RegularExpressions;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Career/market.json: real file loads; broken documents are rejected.</summary>
    public class MarketDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Career", "market.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadMarket(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(20, r.Value.Squad.Min, "MVP_SCOPE/TEST_PLAN: squads between 20-26");
            Assert.AreEqual(26, r.Value.Squad.Max, "MVP_SCOPE/TEST_PLAN: squads between 20-26");
        }

        [Test]
        public void EmptyCurve_IsRejected()
        {
            string text = Read();
            string original = Regex.Match(text, "\"ovrCurve\": \\[\\[.*?\\]\\],\\n    \"ageMultiplier\"").Value;
            Assert.IsNotEmpty(original, "fixture must contain value.ovrCurve");
            string edited = text.Replace(original, "\"ovrCurve\": [],\n    \"ageMultiplier\"");
            Assert.AreNotEqual(text, edited, "fixture edit must apply");
            Assert.IsFalse(MarketReader.Read(edited).IsSuccess);
        }

        [Test]
        public void SquadMaxBelowMin_IsRejected() =>
            Assert.IsFalse(MarketReader.Read(Read().Replace("\"min\": 20,\n    \"max\": 26", "\"min\": 20,\n    \"max\": 10")).IsSuccess);

        [Test]
        public void NegativeBudgetShare_IsRejected() =>
            Assert.IsFalse(MarketReader.Read(Read().Replace("\"transferFundShare\": 0.35", "\"transferFundShare\": 1.5")).IsSuccess);
    }
}
