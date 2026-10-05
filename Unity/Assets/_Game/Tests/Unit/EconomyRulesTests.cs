using Game.Data.Loading;
using Game.Rules.Economy;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Pure club-economy math (GAME_DESIGN §11, B6, X-44) against the real Data/Career/economy.json.</summary>
    public class EconomyRulesTests
    {
        private static Data.Career.EconomyDefinition Db()
        {
            var r = GameDataLoader.LoadEconomy(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        [Test]
        public void TvRevenuePerMonth_IsHigherForTopDivisions() =>
            Assert.Greater(EconomyRules.TvRevenuePerMonth(Db(), 0), EconomyRules.TvRevenuePerMonth(Db(), 3));

        [Test]
        public void GateRevenue_IsCappedByStadiumCapacity_AndHigherOnAWin()
        {
            var e = Db();
            long uncapped = EconomyRules.GateRevenue(e, 0, fans: 50000, stadiumCapacity: 1_000_000, won: false);
            long capped = EconomyRules.GateRevenue(e, 0, fans: 5_000_000, stadiumCapacity: 40000, won: false);
            Assert.AreEqual(50000 * e.Ticketing.AttendanceShare * e.Ticketing.PriceByDivision[0], uncapped, 1.0,
                "below capacity: revenue follows the fan base.");
            Assert.LessOrEqual(capped, 40000L * e.Ticketing.PriceByDivision[0] * 2,
                "above capacity: attendance never exceeds the stadium (plus the win bonus).");

            long lost = EconomyRules.GateRevenue(e, 0, 50000, 1_000_000, won: false);
            long won = EconomyRules.GateRevenue(e, 0, 50000, 1_000_000, won: true);
            Assert.Greater(won, lost);
        }

        [Test]
        public void SponsorshipAndMaintenance_ScaleWithDivisionAndStadiumLevel()
        {
            var e = Db();
            Assert.Greater(EconomyRules.SponsorshipPerMonth(e, 0), EconomyRules.SponsorshipPerMonth(e, 3));
            Assert.Greater(EconomyRules.MaintenancePerMonth(e, 10), EconomyRules.MaintenancePerMonth(e, 1));
        }

        [Test]
        public void Prizes_FollowTheGameDesignRatios()
        {
            var e = Db();
            // GAME_DESIGN §11: D=1, C=3, B=8, A=40, Copa=25.
            Assert.AreEqual(40.0, (double)e.Prizes.LeagueChampionMultiplier[0] / e.Prizes.LeagueChampionMultiplier[3], 0.01);
            Assert.AreEqual(8.0, (double)e.Prizes.LeagueChampionMultiplier[1] / e.Prizes.LeagueChampionMultiplier[3], 0.01);
            Assert.AreEqual(3.0, (double)e.Prizes.LeagueChampionMultiplier[2] / e.Prizes.LeagueChampionMultiplier[3], 0.01);
            Assert.AreEqual(25.0, e.Prizes.CupWinnerMultiplier / e.Prizes.LeagueChampionMultiplier[3], 0.01);
            Assert.Greater(EconomyRules.CupWinnerPrize(e), EconomyRules.CupRunnerUpPrize(e));
        }
    }
}
