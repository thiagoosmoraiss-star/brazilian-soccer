using System.Collections.Generic;

namespace Game.Data.Career
{
    public sealed class StadiumFacilityParameters
    {
        /// <summary>Cost to go from level N to N+1, indexed by N-1 (9 entries for levels 1-10, GAME_DESIGN §8).</summary>
        public IReadOnlyList<long> UpgradeCostPerLevel { get; internal set; }
        public int UpgradeDurationMonths { get; internal set; }
        /// <summary>Minimum stadium level to play in a division (0 = top); MVP_SCOPE's access requirement.</summary>
        public IReadOnlyList<int> MinimumLevelByDivision { get; internal set; }
    }

    public sealed class TrainingCenterFacilityParameters
    {
        /// <summary>Starting level by division (0 = top), since world generation (B1) predates the CT (B7).</summary>
        public IReadOnlyList<int> InitialLevelByDivision { get; internal set; }
        /// <summary>Cost to go from level N to N+1, indexed by N-1 (4 entries for levels 1-5, GAME_DESIGN §8).</summary>
        public IReadOnlyList<long> UpgradeCostPerLevel { get; internal set; }
        public int UpgradeDurationMonths { get; internal set; }
        /// <summary>Development multiplier by level (replaces development.json's neutral TrainingCenterFactor, B7).</summary>
        public IReadOnlyList<float> DevelopmentFactorByLevel { get; internal set; }
    }

    /// <summary>Club facilities (Data/Career/facilities.json, B7, baseline v1, X-45): Stadium and Training Center.</summary>
    public sealed class FacilitiesDefinition
    {
        public StadiumFacilityParameters Stadium { get; internal set; }
        public TrainingCenterFacilityParameters TrainingCenter { get; internal set; }
    }
}
