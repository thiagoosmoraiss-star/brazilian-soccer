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

        [Test]
        public void TheUsersTacticAndLineup_RoundTrip_AndABrokenPlanFailsValidation()
        {
            var db = SaveTestData.Db();
            var career = CareerSimulator.Start(db, 31, new Game.Simulation.QuickSim.QuickSim(db).Simulate, new TransferWindow(), new EconomySystem());
            var club = career.State.World.Clubs[career.State.World.Clubs.Count - 1];
            career.State.ManagedClubId = club.Id;
            var pending = career.AdvanceToUserMatch();
            var team = pending.Setup.Team(pending.UserSide);
            career.SetUserTactic(new Game.Core.Contracts.Match.TacticSetup(team.Tactic.FormationId, 2, 1, 3));
            var starters = new System.Collections.Generic.List<Game.Core.Ids.Id>();
            foreach (var p in team.Starters) starters.Add(p.PlayerId);
            career.SetUserLineup(starters, new[] { team.Bench[0].PlayerId });

            var store = new FileSystemFileStore(SaveTestData.NewTempDirectory());
            Assert.IsTrue(SaveService.Save(store, "career", career.State, new System.DateTime(2026, 1, 1), false).IsSuccess);
            var loaded = SaveService.Load(store, "career");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            var plan = loaded.Value.UserPlan;
            Assert.AreEqual(2, plan.Tactic.Mentality);
            Assert.AreEqual(1, plan.Tactic.DefensiveLine);
            Assert.AreEqual(3, plan.Tactic.Pressure);
            CollectionAssert.AreEqual(starters, plan.Starters);
            Assert.AreEqual(team.Bench[0].PlayerId, plan.Bench[0]);
            Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(CareerStateWriter.Write(career.State), CareerStateWriter.Write(loaded.Value)));

            var broken = loaded.Value;
            broken.UserPlan = new UserTeamPlan(plan.Tactic, System.Linq.Enumerable.ToList(System.Linq.Enumerable.Take(plan.Starters, 10)), null);
            Assert.IsFalse(SaveValidator.Validate(broken).IsSuccess, "10 starters");
            broken.UserPlan = new UserTeamPlan(plan.Tactic, plan.Starters, new[] { plan.Starters[0] });
            Assert.IsFalse(SaveValidator.Validate(broken).IsSuccess, "a starter on the bench too");
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
