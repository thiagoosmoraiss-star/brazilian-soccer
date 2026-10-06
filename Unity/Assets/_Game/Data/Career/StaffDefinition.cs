using System.Collections.Generic;

namespace Game.Data.Career
{
    public sealed class PhysioParameters
    {
        /// <summary>Energy-recovery-per-day multiplier by level (GAME_DESIGN §7: "energia, lesões"; injury effect
        /// is out of B7's scope, D-19b).</summary>
        public IReadOnlyList<float> EnergyRecoveryMultiplierByLevel { get; internal set; }
    }

    public sealed class AssistantParameters
    {
        /// <summary>Development multiplier by level (replaces development.json's neutral AssistantCoachFactor, B7).</summary>
        public IReadOnlyList<float> DevelopmentFactorByLevel { get; internal set; }
    }

    public sealed class ScoutParameters
    {
        /// <summary>Multiplies the scouted potential range's half-width (MarketRules.ScoutPotentialRange, B5) by level;
        /// 1.0 = no narrowing beyond reputation, lower = tighter.</summary>
        public IReadOnlyList<float> PotentialRangeNarrowingByLevel { get; internal set; }
    }

    /// <summary>Technical staff (Data/Career/staff.json, B7, baseline v1, X-45): physio, assistant coach, scout.</summary>
    public sealed class StaffDefinition
    {
        /// <summary>Starting level (1-5) by division (0 = top), for all three roles.</summary>
        public IReadOnlyList<int> InitialLevelByDivision { get; internal set; }
        /// <summary>Season wage by level (1-5), the same scale for all three roles.</summary>
        public IReadOnlyList<long> WagePerLevel { get; internal set; }
        /// <summary>Cost to go from level N to N+1, indexed by N-1 (4 entries for levels 1-5).</summary>
        public IReadOnlyList<long> UpgradeCostPerLevel { get; internal set; }
        public PhysioParameters Physio { get; internal set; }
        public AssistantParameters Assistant { get; internal set; }
        public ScoutParameters Scout { get; internal set; }
    }
}
