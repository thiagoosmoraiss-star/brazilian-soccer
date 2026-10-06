using System;
using System.Text;
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
    /// B8 acceptance (TEST_PLAN "Save"): "corrupção simulada recupera backup" and "escrita interrompida não
    /// destrói o save válido".
    /// </summary>
    public class SaveServiceTests
    {
        private static CareerState NewState(ulong seed, int seasons)
        {
            var db = SaveTestData.Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate,
                new TransferWindow(), new EconomySystem());
            for (int s = 0; s < seasons; s++) career.PlaySeason();
            return career.State;
        }

        [Test]
        public void CorruptedCurrentSave_FallsBackToTheLastBackup()
        {
            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            var stateA = NewState(seed: 1, seasons: 2);
            var stateB = NewState(seed: 2, seasons: 2);

            Assert.IsTrue(SaveService.Save(store, "career", stateA, new DateTime(2026, 1, 1), false).IsSuccess);
            Assert.IsTrue(SaveService.Save(store, "career", stateB, new DateTime(2026, 2, 1), false).IsSuccess, "stateB becomes current; stateA rotates into the 'last' backup.");

            // Corrupt the current body directly (bypassing SaveService): simulates a damaged file on disk.
            store.WriteAllBytes(SaveSlotPaths.Body("career"), Encoding.UTF8.GetBytes("not a gzip stream"));

            var loaded = SaveService.Load(store, "career");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(CareerStateWriter.Write(stateA), CareerStateWriter.Write(loaded.Value)),
                "a corrupted current save must fall back to the 'last' backup (stateA), not stateB.");
        }

        [Test]
        public void InterruptedWrite_NeverReplacesTheValidCurrentSave()
        {
            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            var stateA = NewState(seed: 3, seasons: 2);
            Assert.IsTrue(SaveService.Save(store, "career", stateA, new DateTime(2026, 1, 1), false).IsSuccess);

            // Simulates a write that was interrupted before validation/rename: only the .tmp files are touched.
            store.WriteAllBytes(SaveSlotPaths.Body("career") + ".tmp", Encoding.UTF8.GetBytes("garbage"));
            store.WriteAllBytes(SaveSlotPaths.Meta("career") + ".tmp", Encoding.UTF8.GetBytes("garbage"));

            var loaded = SaveService.Load(store, "career");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(CareerStateWriter.Write(stateA), CareerStateWriter.Write(loaded.Value)));
        }

        [Test]
        public void SeasonStartBackup_IsOnlyReplacedOnASeasonStartSave()
        {
            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            var stateA = NewState(seed: 4, seasons: 1);
            Assert.IsTrue(SaveService.Save(store, "career", stateA, new DateTime(2026, 1, 1), isSeasonStart: true).IsSuccess);
            var stateB = NewState(seed: 5, seasons: 1);
            Assert.IsTrue(SaveService.Save(store, "career", stateB, new DateTime(2026, 2, 1), isSeasonStart: false).IsSuccess);

            var seasonBackup = store.ReadAllBytes(SaveSlotPaths.BodyBackup("career", "season"));
            var currentBody = store.ReadAllBytes(SaveSlotPaths.Body("career"));
            Assert.AreNotEqual(currentBody, seasonBackup, "a non-season-start save must not touch the season backup.");
        }

        [Test]
        public void MissingSave_FailsWithoutThrowing()
        {
            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            var loaded = SaveService.Load(store, "career");
            Assert.IsFalse(loaded.IsSuccess);
        }
    }
}
