using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/Career/development.json: real file loads; broken documents are rejected.</summary>
    public class DevelopmentDataTests
    {
        private static string Read() => File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Career", "development.json"));

        [Test]
        public void RealFile_Loads()
        {
            var r = GameDataLoader.LoadDevelopment(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(3, r.Value.YouthPerClubPerYear, "MVP_SCOPE: 3 youths per year");
            Assert.AreEqual(3, r.Value.YellowsPerSuspension, "GAME_DESIGN §9: 3 yellows = 1 match");
        }

        [Test]
        public void RetirementNotCertainAtTheLastAge_IsRejected()
        {
            var text = Read().Replace("\"chance\": 1.0", "\"chance\": 0.9");
            Assert.AreNotEqual(Read(), text, "fixture edit must apply");
            Assert.IsFalse(DevelopmentReader.Read(text).IsSuccess);
        }

        [Test]
        public void SquadFloorBelowEleven_IsRejected() =>
            Assert.IsFalse(DevelopmentReader.Read(Read().Replace("\"minSquadSize\": 18", "\"minSquadSize\": 9")).IsSuccess);
    }
}
