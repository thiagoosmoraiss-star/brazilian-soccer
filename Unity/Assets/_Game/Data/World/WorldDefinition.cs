using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Loading;

namespace Game.Data.World
{
    /// <summary>Immutable world-generation definitions loaded from Data/World/*.json (baseline v1).</summary>
    public sealed class WorldDefinition
    {
        public NamePools Names { get; internal set; }
        public IReadOnlyList<CityDefinition> Cities { get; internal set; }
        public ClubTemplates ClubTemplates { get; internal set; }
        public CrestTemplates CrestTemplates { get; internal set; }
        public GenerationParameters Generation { get; internal set; }
    }

    public sealed class NamePools
    {
        public IReadOnlyList<string> FirstNames { get; internal set; }
        public IReadOnlyList<string> LastNames { get; internal set; }
        public IReadOnlyList<WeightedCode> Nationalities { get; internal set; }
    }

    public readonly struct WeightedCode
    {
        public readonly string Code;
        public readonly int Weight;
        public WeightedCode(string code, int weight) { Code = code; Weight = weight; }
    }

    public sealed class CityDefinition
    {
        public string Name { get; internal set; }
        public string Uf { get; internal set; }
    }

    public sealed class ColorDefinition
    {
        public string Id { get; internal set; }
        public string Hex { get; internal set; }
    }

    public sealed class ClubTemplates
    {
        /// <summary>Club name patterns containing the "{city}" placeholder.</summary>
        public IReadOnlyList<string> NamePatterns { get; internal set; }
        public IReadOnlyList<ColorDefinition> Colors { get; internal set; }
    }

    public sealed class CrestTemplates
    {
        public IReadOnlyList<string> Shapes { get; internal set; }
        public IReadOnlyList<string> Symbols { get; internal set; }
    }

    /// <summary>Per-division generation ranges. Divisions are listed from the top (A) to the bottom.</summary>
    public sealed class DivisionParameters
    {
        public string Name { get; internal set; }
        public IntRange SquadOvrMean { get; internal set; }
        public IntRange Reputation { get; internal set; }
        public LongRange Budget { get; internal set; }
        public IntRange StadiumLevel { get; internal set; }
        public IntRange Fans { get; internal set; }
    }

    public readonly struct LongRange
    {
        public readonly long Min;
        public readonly long Max;
        public LongRange(long min, long max) { Min = min; Max = max; }
        public bool IsValid => Min <= Max;
    }

    public sealed class SquadSlot
    {
        public Position Position { get; internal set; }
        public bool Starter { get; internal set; }
    }

    public sealed class SquadParameters
    {
        public IReadOnlyList<SquadSlot> Slots { get; internal set; }
        public int StarterOvrOffset { get; internal set; }
        public int ReserveOvrOffset { get; internal set; }
        public int YouthOvrOffset { get; internal set; }
        public int VeteranOvrOffset { get; internal set; }
        public int PlayerOvrNoise { get; internal set; }
        public IntRange YouthCount { get; internal set; }
        public IntRange YouthAge { get; internal set; }
        public IntRange VeteranCount { get; internal set; }
        public IntRange VeteranAge { get; internal set; }
        public IntRange RegularAge { get; internal set; }
    }

    public sealed class AttributeParameters
    {
        public int RoleNoise { get; internal set; }
        public int OffRoleGap { get; internal set; }
        public int OffRoleNoise { get; internal set; }
        public IntRange GoalkeepingAttributesForOutfield { get; internal set; }
        public int OutfieldGapForGoalkeepers { get; internal set; }
        public int Min { get; internal set; }
        public int Max { get; internal set; }
        public int OvrCorrectionPasses { get; internal set; }
    }

    public sealed class AgeBonus
    {
        public int MaxAge { get; internal set; }
        public IntRange Bonus { get; internal set; }
    }

    public sealed class PotentialParameters
    {
        public int Min { get; internal set; }
        public int Max { get; internal set; }
        public IntRange YouthBonus { get; internal set; }
        /// <summary>Ordered by MaxAge ascending; the first entry whose MaxAge &gt;= age applies.</summary>
        public IReadOnlyList<AgeBonus> BonusByAge { get; internal set; }
    }

    public sealed class GenerationParameters
    {
        public int StartYear { get; internal set; }
        public IReadOnlyList<DivisionParameters> Divisions { get; internal set; }
        public int ClubsPerDivision { get; internal set; }
        public IReadOnlyList<int> ReputationStarThresholds { get; internal set; }
        public IReadOnlyList<int> StadiumCapacityByLevel { get; internal set; }
        public float ClubProfileNoise { get; internal set; }
        public SquadParameters Squad { get; internal set; }
        public AttributeParameters Attributes { get; internal set; }
        public PotentialParameters Potential { get; internal set; }
        /// <summary>Relative weights for 0, 1, 2 secondary positions.</summary>
        public IReadOnlyList<int> SecondaryCountWeights { get; internal set; }
        /// <summary>Candidate secondary positions, indexed by <see cref="Position"/>.</summary>
        public IReadOnlyList<IReadOnlyList<Position>> SecondaryCandidates { get; internal set; }
        public IReadOnlyList<IntRange> HeightCm { get; internal set; }
        public IReadOnlyList<float> LeftFootedChance { get; internal set; }
        /// <summary>Relative weights for weak foot 1..5.</summary>
        public IReadOnlyList<int> WeakFootWeights { get; internal set; }
    }
}
