using System.Collections.Generic;
using Game.Data.Effects;
using Game.Data.Loading;

namespace Game.Data.Career
{
    public sealed class AgeRate
    {
        public int MaxAge { get; internal set; }
        /// <summary>OVR points per year at full potential gap and full minutes (negative = decline).</summary>
        public float OvrPerYear { get; internal set; }
    }

    public sealed class RetirementChance
    {
        public int Age { get; internal set; }
        public float Chance { get; internal set; }
    }

    /// <summary>Player development, condition, injuries, suspensions, retirement and youth (Data/Career/development.json, X-42).</summary>
    public sealed class DevelopmentDefinition
    {
        public int WeeksPerYear { get; internal set; }
        /// <summary>Ordered by MaxAge; the first entry with MaxAge &gt;= age applies.</summary>
        public IReadOnlyList<AgeRate> AgeCurve { get; internal set; }
        public float PotentialGapForFullGrowth { get; internal set; }
        public float NoMinutesFactor { get; internal set; }
        public float FullMinutesShare { get; internal set; }
        public float TrainingCenterFactor { get; internal set; }
        public float AssistantCoachFactor { get; internal set; }
        public IReadOnlyList<Attr> PhysicalAttributes { get; internal set; }
        public float OtherAttributeDeclineChance { get; internal set; }
        public int AnnualMinAppearances { get; internal set; }
        public float AnnualGoodRating { get; internal set; }
        public float AnnualGoodRatingBonus { get; internal set; }
        public float EnergyRecoveryPerDay { get; internal set; }
        public float MoraleUpOnWin { get; internal set; }
        public float MoraleDownOnLoss { get; internal set; }
        public float MoraleToNeutralOnDraw { get; internal set; }
        public int FormMatches { get; internal set; }
        public IntRange LightInjuryMatches { get; internal set; }
        public IntRange MediumInjuryMatches { get; internal set; }
        public IntRange SevereInjuryDays { get; internal set; }
        public int YellowsPerSuspension { get; internal set; }
        public int SecondYellowMatches { get; internal set; }
        /// <summary>Relative weights for 1, 2, 3... matches after a straight red card.</summary>
        public IReadOnlyList<int> StraightRedMatchWeights { get; internal set; }
        public bool ResetSuspensionsEachSeason { get; internal set; }
        public IReadOnlyList<RetirementChance> RetirementByAge { get; internal set; }
        public int YouthPerClubPerYear { get; internal set; }
        public IntRange YouthAge { get; internal set; }
        public IntRange YouthOvrOffsetFromSquadMean { get; internal set; }
        public IntRange YouthPotentialAboveOvr { get; internal set; }
        public int MinSquadSize { get; internal set; }
        public int MinGoalkeepers { get; internal set; }
    }
}
