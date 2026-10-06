using Game.Career.Economy;
using Game.Career.Market;
using Game.Career.Season;
using Game.Save;
using Game.Save.IO;
using Game.Save.Serialization;
using NUnit.Framework;

namespace Game.Tests.Save
{
    /// <summary>
    /// B8 acceptance (ROADMAP, TEST_PLAN "Save"): a save round-trips to the exact same <see cref="CareerState"/>,
    /// and the resumed career keeps simulating normally afterwards.
    /// </summary>
    public class SaveRoundTripTests
    {
        private static readonly ulong[] Seeds = { 11, 97 };

        [TestCaseSource(nameof(Seeds))]
        public void SaveThenLoad_IsByteForByteTheSameCareerState(ulong seed)
        {
            var db = SaveTestData.Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate,
                new TransferWindow(), new EconomySystem());
            for (int s = 0; s < 5; s++) career.PlaySeason();

            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            var saveResult = SaveService.Save(store, "career", career.State, new System.DateTime(2026, 1, 1), isSeasonStart: false);
            Assert.IsTrue(saveResult.IsSuccess, saveResult.ToString());

            var loadResult = SaveService.Load(store, "career");
            Assert.IsTrue(loadResult.IsSuccess, loadResult.ToString());

            var originalJson = CareerStateWriter.Write(career.State);
            var loadedJson = CareerStateWriter.Write(loadResult.Value);
            Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(originalJson, loadedJson),
                $"seed={seed}: the loaded state must serialize to exactly the same JSON as the original.\nOriginal: {originalJson}\nLoaded: {loadedJson}");
        }

        [TestCaseSource(nameof(Seeds))]
        public void ResumedCareer_KeepsSimulatingNormally(ulong seed)
        {
            var db = SaveTestData.Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate,
                new TransferWindow(), new EconomySystem());
            for (int s = 0; s < 3; s++) career.PlaySeason();

            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            Assert.IsTrue(SaveService.Save(store, "career", career.State, new System.DateTime(2026, 1, 1), isSeasonStart: false).IsSuccess);
            var loaded = SaveService.Load(store, "career");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());

            var resumed = CareerSimulator.Resume(db, loaded.Value, new Game.Simulation.QuickSim.QuickSim(db).Simulate,
                new TransferWindow(), new EconomySystem());
            int yearBefore = resumed.State.Season.Year;
            for (int s = 0; s < 2; s++) resumed.PlaySeason();
            Assert.Greater(resumed.State.Season.Year, yearBefore, $"seed={seed}: the resumed career must keep advancing.");
        }
    }
}
