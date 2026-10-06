using System;
using System.Collections.Generic;
using Game.Data.Career;

namespace Game.Rules.Board
{
    /// <summary>
    /// Pure facilities and staff math (GAME_DESIGN §7-8, B7, X-45): upgrade costs, the stadium's divisional
    /// requirement, and how the Training Center and technical staff modify development, energy and scouting.
    /// Coefficients live in Data/Career/facilities.json and Data/Career/staff.json.
    /// </summary>
    public static class BoardRules
    {
        public static long StadiumUpgradeCost(FacilitiesDefinition f, int currentLevel) => AtIndex(f.Stadium.UpgradeCostPerLevel, currentLevel - 1);

        public static int StadiumMinimumLevel(FacilitiesDefinition f, int divisionIndex) => AtIndexInt(f.Stadium.MinimumLevelByDivision, divisionIndex);

        public static long TrainingCenterUpgradeCost(FacilitiesDefinition f, int currentLevel) =>
            AtIndex(f.TrainingCenter.UpgradeCostPerLevel, currentLevel - 1);

        public static float TrainingCenterFactor(FacilitiesDefinition f, int level) => AtIndexF(f.TrainingCenter.DevelopmentFactorByLevel, level - 1);

        public static long StaffWage(StaffDefinition s, int level) => AtIndex(s.WagePerLevel, level - 1);

        public static long StaffUpgradeCost(StaffDefinition s, int currentLevel) => AtIndex(s.UpgradeCostPerLevel, currentLevel - 1);

        public static float PhysioEnergyMultiplier(StaffDefinition s, int level) => AtIndexF(s.Physio.EnergyRecoveryMultiplierByLevel, level - 1);

        public static float AssistantDevelopmentFactor(StaffDefinition s, int level) => AtIndexF(s.Assistant.DevelopmentFactorByLevel, level - 1);

        public static float ScoutNarrowing(StaffDefinition s, int level) => AtIndexF(s.Scout.PotentialRangeNarrowingByLevel, level - 1);

        /// <summary>A level-by-division array (initial Training Center/staff level), clamped to the array's bounds.</summary>
        public static int LevelForDivision(IReadOnlyList<int> levelsByDivision, int divisionIndex) => AtIndexInt(levelsByDivision, divisionIndex);

        private static long AtIndex(IReadOnlyList<long> values, int index)
        {
            if (values == null || values.Count == 0) return 0L;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        private static float AtIndexF(IReadOnlyList<float> values, int index)
        {
            if (values == null || values.Count == 0) return 1f;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        private static int AtIndexInt(IReadOnlyList<int> values, int index)
        {
            if (values == null || values.Count == 0) return 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }
    }
}
