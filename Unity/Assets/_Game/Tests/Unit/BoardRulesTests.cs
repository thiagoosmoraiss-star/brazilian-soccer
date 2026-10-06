using Game.Data.Loading;
using Game.Rules.Board;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Pure facilities/staff math (GAME_DESIGN §7-8, B7, X-45) against the real data files.</summary>
    public class BoardRulesTests
    {
        private static Data.Career.FacilitiesDefinition Facilities()
        {
            var r = GameDataLoader.LoadFacilities(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        private static Data.Career.StaffDefinition Staff()
        {
            var r = GameDataLoader.LoadStaff(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return r.Value;
        }

        [Test]
        public void StadiumMinimumLevel_IsHigherForTopDivisions()
        {
            var f = Facilities();
            Assert.Greater(BoardRules.StadiumMinimumLevel(f, 0), BoardRules.StadiumMinimumLevel(f, 3));
        }

        [Test]
        public void TrainingCenterFactor_IncreasesWithLevel()
        {
            var f = Facilities();
            Assert.Greater(BoardRules.TrainingCenterFactor(f, 5), BoardRules.TrainingCenterFactor(f, 1));
        }

        [Test]
        public void UpgradeCosts_IncreaseWithLevel()
        {
            var f = Facilities();
            Assert.Greater(BoardRules.StadiumUpgradeCost(f, 5), BoardRules.StadiumUpgradeCost(f, 1));
            Assert.Greater(BoardRules.TrainingCenterUpgradeCost(f, 4), BoardRules.TrainingCenterUpgradeCost(f, 1));
        }

        [Test]
        public void StaffEffects_IncreaseWithLevel()
        {
            var s = Staff();
            Assert.Greater(BoardRules.StaffWage(s, 5), BoardRules.StaffWage(s, 1));
            Assert.Greater(BoardRules.PhysioEnergyMultiplier(s, 5), BoardRules.PhysioEnergyMultiplier(s, 1));
            Assert.Greater(BoardRules.AssistantDevelopmentFactor(s, 5), BoardRules.AssistantDevelopmentFactor(s, 1));
            Assert.Less(BoardRules.ScoutNarrowing(s, 5), BoardRules.ScoutNarrowing(s, 1), "a better scout narrows the range more.");
        }
    }
}
