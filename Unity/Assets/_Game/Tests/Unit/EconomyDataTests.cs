using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Career/economy.json: real file loads; broken documents are rejected.</summary>
    public class EconomyDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Career", "economy.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadEconomy(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(4, r.Value.Prizes.LeagueChampionMultiplier.Count, "GAME_DESIGN §11: one ratio per division");
            Assert.AreEqual(1, r.Value.CashAlert.AlertMonths, "GAME_DESIGN §11: 1st month alert");
            Assert.AreEqual(3, r.Value.CashAlert.TransferLockoutMonths, "GAME_DESIGN §11: 3 months blocks signings");
        }

        [Test]
        public void EmptyTvSchedule_IsRejected() =>
            Assert.IsFalse(EconomyReader.Read(Read().Replace(
                "\"perSeasonByDivision\": [2500000, 650000, 210000, 95000]", "\"perSeasonByDivision\": []")).IsSuccess);

        [Test]
        public void LockoutBeforeAlert_IsRejected() =>
            Assert.IsFalse(EconomyReader.Read(Read().Replace(
                "\"alertMonths\": 1,\n    \"transferLockoutMonths\": 3", "\"alertMonths\": 5,\n    \"transferLockoutMonths\": 3")).IsSuccess);

        [Test]
        public void NegativePrizeBase_IsRejected() =>
            Assert.IsFalse(EconomyReader.Read(Read().Replace("\"base\": 1500000", "\"base\": -1")).IsSuccess);
    }
}
