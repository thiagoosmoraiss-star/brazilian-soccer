using System.IO;
using Game.Save;
using Game.Save.Serialization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Save
{
    /// <summary>
    /// B8 acceptance (TEST_PLAN "Save"): the reader accepts a hand-authored save body, not only one produced by
    /// <see cref="CareerStateWriter"/> itself. When schemaVersion 2 exists, a <c>v1_to_v2_*.json</c> fixture plus a
    /// migration test joins this one (<see cref="SaveMigrations"/>).
    /// </summary>
    public class SaveFixtureTests
    {
        [Test]
        public void V1Minimal_ReadsIntoTheExpectedCareerState()
        {
            var json = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            var result = CareerStateReader.Read(json);
            Assert.IsTrue(result.IsSuccess, result.ToString());

            var state = result.Value;
            Assert.AreEqual(123UL, state.Seed);
            Assert.AreEqual(1, state.World.Clubs.Count);
            Assert.AreEqual("Clube Teste", state.World.Clubs[0].Name);
            Assert.AreEqual(1, state.World.Players.Count);
            Assert.AreEqual(Game.Data.Definitions.Position.GOL, state.World.Players[0].MainPosition);
            Assert.AreEqual(2026, state.Season.Year);
            Assert.IsTrue(SaveValidator.Validate(state).IsSuccess, "the fixture must also satisfy every save invariant.");
        }
    }
}
