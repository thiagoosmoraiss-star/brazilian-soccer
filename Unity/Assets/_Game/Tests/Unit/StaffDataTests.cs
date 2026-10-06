using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Career/staff.json: real file loads; broken documents are rejected.</summary>
    public class StaffDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Career", "staff.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadStaff(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(5, r.Value.WagePerLevel.Count, "GAME_DESIGN §7: staff levels 1-5");
        }

        [Test]
        public void WrongWageCurveLength_IsRejected() =>
            Assert.IsFalse(StaffReader.Read(Read().Replace(
                "\"wagePerLevel\": [15000, 35000, 70000, 130000, 220000]", "\"wagePerLevel\": [15000]")).IsSuccess);

        [Test]
        public void WrongPhysioCurveLength_IsRejected() =>
            Assert.IsFalse(StaffReader.Read(Read().Replace(
                "\"energyRecoveryMultiplierByLevel\": [0.85, 0.95, 1.0, 1.1, 1.2]", "\"energyRecoveryMultiplierByLevel\": [1.0, 1.0]")).IsSuccess);
    }
}
