using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Career/facilities.json: real file loads; broken documents are rejected.</summary>
    public class FacilitiesDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Career", "facilities.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadFacilities(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(4, r.Value.Stadium.MinimumLevelByDivision.Count, "one requirement per division");
            Assert.AreEqual(5, r.Value.TrainingCenter.DevelopmentFactorByLevel.Count, "GAME_DESIGN §8: CT levels 1-5");
        }

        [Test]
        public void WrongTrainingCenterCurveLength_IsRejected() =>
            Assert.IsFalse(FacilitiesReader.Read(Read().Replace(
                "\"developmentFactorByLevel\": [0.85, 0.95, 1.0, 1.1, 1.25]", "\"developmentFactorByLevel\": [1.0]")).IsSuccess);

        [Test]
        public void EmptyUpgradeCost_IsRejected() =>
            Assert.IsFalse(FacilitiesReader.Read(Read().Replace(
                "\"upgradeCostPerLevel\": [500000, 900000, 1500000, 2500000, 4000000, 6000000, 9000000, 13000000, 18000000]",
                "\"upgradeCostPerLevel\": []")).IsSuccess);
    }
}
