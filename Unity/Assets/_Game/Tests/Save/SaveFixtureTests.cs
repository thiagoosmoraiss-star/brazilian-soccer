using System.IO;
using Game.Save;
using Game.Save.Serialization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Save
{
    /// <summary>
    /// B8 acceptance (TEST_PLAN "Save"): the reader accepts a hand-authored save body, not only one produced by
    /// <see cref="CareerStateWriter"/> itself, through the migration chain (<see cref="SaveMigrations"/>: v1 → v2 adds
    /// <c>matchInProgress</c>, C1).
    /// </summary>
    public class SaveFixtureTests
    {
        [Test]
        public void V1Minimal_ReadsIntoTheExpectedCareerState()
        {
            var json = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            var migrated = SaveMigrations.MigrateToCurrent(json, 1);
            Assert.IsTrue(migrated.IsSuccess, migrated.ToString());
            var result = CareerStateReader.Read(migrated.Value);
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

        [Test]
        public void V1ToV2_AddsAnEmptyMatchInProgress_AndAV1BodyWithoutItIsRejectedUnmigrated()
        {
            var json = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            Assert.IsFalse(CareerStateReader.Read((JObject)json.DeepClone()).IsSuccess, "a v1 body lacks matchInProgress (strict reader)");
            var migrated = SaveMigrations.MigrateToCurrent(json, 1);
            Assert.IsTrue(migrated.IsSuccess, migrated.ToString());
            Assert.AreEqual(2, migrated.Value["schemaVersion"].Value<int>());
            Assert.AreEqual(JTokenType.Null, migrated.Value["matchInProgress"].Type);
            Assert.IsNull(CareerStateReader.Read(migrated.Value).Value.MatchInProgress);
        }
    }
}
