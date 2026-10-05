using Game.Data.Loading;
using Game.Rules.Market;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Pure transfer-market math (GAME_DESIGN §10, B5, X-43) against the real Data/Career/market.json.</summary>
    public class MarketRulesTests
    {
        private static Data.Career.MarketDefinition Db()
        {
            var r = GameDataLoader.LoadMarket(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        [Test]
        public void Value_IncreasesWithOvr_AndWithDivision()
        {
            var m = Db();
            long low = MarketRules.Value(m, 0, 50, 26, 50, 2, 6f);
            long high = MarketRules.Value(m, 0, 85, 26, 85, 2, 6f);
            Assert.Greater(high, low);

            long topDivision = MarketRules.Value(m, 0, 70, 26, 70, 2, 6f);
            long bottomDivision = MarketRules.Value(m, 3, 70, 26, 70, 2, 6f);
            Assert.Greater(topDivision, bottomDivision, "GAME_DESIGN §10: value scales with division.");
        }

        [Test]
        public void Value_IsHigherForHighPotentialProspects()
        {
            var m = Db();
            long modestPotential = MarketRules.Value(m, 0, 60, 19, 62, 3, 6f);
            long highPotential = MarketRules.Value(m, 0, 60, 19, 90, 3, 6f);
            Assert.Greater(highPotential, modestPotential, "a young prospect far from potential is worth more.");
        }

        [Test]
        public void WageReference_IncreasesWithOvr_AndWithReputation()
        {
            var m = Db();
            Assert.Greater(MarketRules.WageReference(m, 0, 500, 85), MarketRules.WageReference(m, 0, 500, 50));
            Assert.Greater(MarketRules.WageReference(m, 0, 900, 70), MarketRules.WageReference(m, 0, 100, 70));
        }

        [Test]
        public void Interest_IsWithinUnitRange_AndRewardsBetterWages()
        {
            var m = Db();
            float lowWage = MarketRules.Interest(m, 500, 500, 0, 0, offeredWage: 1000, referenceWage: 10000, promisedMinutesShare: 0.5f);
            float highWage = MarketRules.Interest(m, 500, 500, 0, 0, offeredWage: 20000, referenceWage: 10000, promisedMinutesShare: 0.5f);
            Assert.That(lowWage, Is.InRange(0f, 1f));
            Assert.That(highWage, Is.InRange(0f, 1f));
            Assert.Greater(highWage, lowWage);
        }

        [Test]
        public void SellerAcceptFactor_IsHigherForMoreImportantPlayers()
        {
            var m = Db();
            float fringe = MarketRules.SellerAcceptFactor(m, importance01: 0.1f, contractYearsLeft: 1, financialHealth01: 0.5f);
            float starter = MarketRules.SellerAcceptFactor(m, importance01: 1f, contractYearsLeft: 4, financialHealth01: 0.5f);
            Assert.Greater(starter, fringe);
            Assert.GreaterOrEqual(fringe, 0.5f, "never asks for less than half the market value.");
        }

        [Test]
        public void ScoutPotentialRange_AlwaysContainsTheTruePotential()
        {
            foreach (int reputation in new[] { 0, 300, 600, 950 })
            {
                var (min, max) = MarketRules.ScoutPotentialRange(reputation, truePotential: 80);
                Assert.That(80, Is.InRange(min, max), $"reputation={reputation}");
            }
            var narrow = MarketRules.ScoutPotentialRange(950, 80);
            var wide = MarketRules.ScoutPotentialRange(0, 80);
            Assert.Less(narrow.Max - narrow.Min, wide.Max - wide.Min, "a more reputable club scouts a narrower range.");
        }
    }
}
