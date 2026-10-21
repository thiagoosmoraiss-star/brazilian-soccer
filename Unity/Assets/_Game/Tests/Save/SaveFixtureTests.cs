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
    /// <c>matchInProgress</c>, C1; v2 → v3 adds <c>userPlan</c>, C3b; v3 → v4 adds <c>difficulty</c>, F1c).
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
        public void V1ToCurrent_AddsAnEmptyMatchInProgressAndUserPlan_AndAV1BodyIsRejectedUnmigrated()
        {
            var json = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            Assert.IsFalse(CareerStateReader.Read((JObject)json.DeepClone()).IsSuccess, "a v1 body lacks matchInProgress (strict reader)");
            var migrated = SaveMigrations.MigrateToCurrent(json, 1);
            Assert.IsTrue(migrated.IsSuccess, migrated.ToString());
            Assert.AreEqual(SaveSchema.CurrentVersion, migrated.Value["schemaVersion"].Value<int>());
            Assert.AreEqual(JTokenType.Null, migrated.Value["matchInProgress"].Type);
            Assert.AreEqual(JTokenType.Null, migrated.Value["userPlan"].Type);
            var read = CareerStateReader.Read(migrated.Value);
            Assert.IsTrue(read.IsSuccess, read.ToString());
            Assert.IsNull(read.Value.MatchInProgress);
            Assert.IsNull(read.Value.UserPlan);
        }

        [Test]
        public void V2ToV3_AddsAnEmptyUserPlan()
        {
            var v1 = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            v1["matchInProgress"] = JValue.CreateNull();
            v1["schemaVersion"] = 2; // a v2 body: v1 plus matchInProgress
            Assert.IsFalse(CareerStateReader.Read((JObject)v1.DeepClone()).IsSuccess, "a v2 body lacks userPlan (strict reader)");
            var migrated = SaveMigrations.MigrateToCurrent(v1, 2);
            Assert.IsTrue(migrated.IsSuccess, migrated.ToString());
            Assert.AreEqual(SaveSchema.CurrentVersion, migrated.Value["schemaVersion"].Value<int>());
            Assert.IsNull(CareerStateReader.Read(migrated.Value).Value.UserPlan);
        }

        [Test]
        public void V3ToV4_AddsNoDifficulty_SoTheCareerPlaysTheDefaultLevel()
        {
            var v3 = SaveJson.Parse(File.ReadAllText(SaveTestData.FixturePath("v1_minimal.json")));
            v3["matchInProgress"] = JValue.CreateNull();
            v3["userPlan"] = JValue.CreateNull();
            v3["schemaVersion"] = 3; // a v3 body: v1 plus matchInProgress and userPlan
            Assert.IsFalse(CareerStateReader.Read((JObject)v3.DeepClone()).IsSuccess, "a v3 body lacks difficulty (strict reader)");
            var migrated = SaveMigrations.MigrateToCurrent(v3, 3);
            Assert.IsTrue(migrated.IsSuccess, migrated.ToString());
            Assert.AreEqual(4, migrated.Value["schemaVersion"].Value<int>());
            Assert.AreEqual(JTokenType.Null, migrated.Value["difficulty"].Type);
            var read = CareerStateReader.Read(migrated.Value);
            Assert.IsTrue(read.IsSuccess, read.ToString());
            Assert.IsNull(read.Value.Difficulty, "null = the data's default level (Normal)");
            Assert.IsTrue(SaveValidator.Validate(read.Value).IsSuccess);
        }
    }
}
