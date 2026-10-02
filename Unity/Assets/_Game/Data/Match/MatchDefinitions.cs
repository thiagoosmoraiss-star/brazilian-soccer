using System.Collections.Generic;
using Game.Data.Definitions;

namespace Game.Data.Match
{
    /// <summary>Formation (Data/Formations/formations.json): 11 slots with position, role and base position.</summary>
    public sealed class FormationDefinition
    {
        public string Id { get; internal set; }
        public IReadOnlyList<FormationSlot> Slots { get; internal set; }
    }

    public sealed class FormationSlot
    {
        public Position Position { get; internal set; }
        public FormationRole Role { get; internal set; }
        /// <summary>Normalized base position: X 0 = own goal line .. 1 = opponent goal line; Y 0 = left .. 1 = right.</summary>
        public float X { get; internal set; }
        public float Y { get; internal set; }
    }

    /// <summary>Rules shared by MatchEngine and QuickSim (Data/Balance/match_rules.json, baseline v1).</summary>
    public sealed class MatchRulesDefinition
    {
        /// <summary>[sector][attribute] weights, normalized to sum 1 per sector.</summary>
        public float[][] SectorAttributeWeights { get; internal set; }
        /// <summary>[role][sector] contribution of a slot role to a sector.</summary>
        public float[][] RoleSectorWeights { get; internal set; }
        /// <summary>[sector] divisor so a 4-4-2 of players rated R has sector strengths of R.</summary>
        public float[] SectorReferenceWeights { get; internal set; }
        public ConditionRules Condition { get; internal set; }
        public FatigueRules Fatigue { get; internal set; }
        public DisciplineRules Discipline { get; internal set; }
        public InjuryRules Injuries { get; internal set; }
        public RatingRules Ratings { get; internal set; }
        public SubstitutionRules Substitutions { get; internal set; }
        public PenaltyShootoutRules PenaltyShootout { get; internal set; }
    }

    /// <summary>Penalty shootout for knockout draws (X-41): 5 kicks each, then sudden death.</summary>
    public sealed class PenaltyShootoutRules
    {
        public float ScoreChance { get; internal set; }
        public float MaxScoreChance { get; internal set; }
        public float TakerExponent { get; internal set; }
        public float KeeperExponent { get; internal set; }
        public int KicksPerTeam { get; internal set; }
        public int MaxSuddenDeathRounds { get; internal set; }
    }

    public sealed class ConditionRules
    {
        /// <summary>Multiplier per morale level 1..5 (GAME_DESIGN §9: ±5%).</summary>
        public IReadOnlyList<float> MoraleFactors { get; internal set; }
        public float FormNeutral { get; internal set; }
        public float FormFactorPerPoint { get; internal set; }
        public float FormFactorMin { get; internal set; }
        public float FormFactorMax { get; internal set; }
        /// <summary>Energy (0-100) below which the LateMatchPenalty effect applies (GAME_DESIGN §18: &lt; 60%).</summary>
        public float LowEnergyThreshold { get; internal set; }
    }

    public sealed class FatigueRules
    {
        public float BaseDrainPerMinute { get; internal set; }
        public IReadOnlyList<float> MentalityIntensity { get; internal set; }
        public IReadOnlyList<float> PressureIntensity { get; internal set; }
        public float HalftimeRecovery { get; internal set; }
    }

    public sealed class DisciplineRules
    {
        public float YellowPerFoul { get; internal set; }
        public float StraightRedPerFoul { get; internal set; }
        /// <summary>Card probability multiplier for a player already on a yellow (players and referees adapt).</summary>
        public float BookedCardFactor { get; internal set; }
    }

    public sealed class InjuryRules
    {
        public float HazardPerPlayerMinute { get; internal set; }
        public float LowEnergyThreshold { get; internal set; }
        public float LowEnergyMultiplier { get; internal set; }
        public int LightWeight { get; internal set; }
        public int MediumWeight { get; internal set; }
        public int SevereWeight { get; internal set; }
    }

    public sealed class RatingRules
    {
        public float Base { get; internal set; }
        public float Goal { get; internal set; }
        public float Assist { get; internal set; }
        public float ShotOnTarget { get; internal set; }
        public float ShotOffTarget { get; internal set; }
        public float Foul { get; internal set; }
        public float Yellow { get; internal set; }
        public float Red { get; internal set; }
        public float Win { get; internal set; }
        public float Loss { get; internal set; }
        public float CleanSheet { get; internal set; }
        public float GoalConceded { get; internal set; }
        public IReadOnlyList<FormationRole> DefensiveRoles { get; internal set; }
        public float Noise { get; internal set; }
        public int FullImpactMinutes { get; internal set; }
        public float Min { get; internal set; }
        public float Max { get; internal set; }
    }

    public sealed class SubstitutionRules
    {
        public IReadOnlyList<int> Windows { get; internal set; }
        /// <summary>Stoppages allowed besides half-time (GAME_DESIGN §27: 5 subs in 3 stoppages + half-time).</summary>
        public int MaxWindows { get; internal set; }
        public int HalftimeMinute { get; internal set; }
        public float EnergyThreshold { get; internal set; }
        public int MaxTiredSubstitutions { get; internal set; }
        /// <summary>A tired player is replaced only if the best bench option is at most this much worse (effective OVR).</summary>
        public float BenchOvrMargin { get; internal set; }
    }

    public enum PlayType
    {
        OpenPlay = 0,
        Cross = 1,
        LongShot = 2,
    }

    public sealed class PlayTypeParameters
    {
        public PlayType Type { get; internal set; }
        public float Weight { get; internal set; }
        public float OnTarget { get; internal set; }
        public float GoalGivenOnTarget { get; internal set; }
        public float AssistChance { get; internal set; }
    }

    public sealed class CornerParameters
    {
        public float FromBlock { get; internal set; }
        public float FromSave { get; internal set; }
        public float FromAttackWithoutShot { get; internal set; }
        public float ShotChance { get; internal set; }
        public float OnTarget { get; internal set; }
        public float GoalGivenOnTarget { get; internal set; }
        public float AssistChance { get; internal set; }
    }

    public sealed class PenaltyParameters
    {
        public float PerDefensiveFoul { get; internal set; }
        public float OnTarget { get; internal set; }
        public float GoalGivenOnTarget { get; internal set; }
    }

    /// <summary>QuickSim coefficients (Data/Balance/quicksim.json, baseline v1 calibrated by the B2 tests).</summary>
    public sealed class QuickSimDefinition
    {
        public int Minutes { get; internal set; }
        public float HomeAdvantage { get; internal set; }
        public float PossessionExponent { get; internal set; }
        public float AttackChancePerMinute { get; internal set; }
        public float AttackRatioExponent { get; internal set; }
        /// <summary>Indexed by <see cref="PlayType"/>.</summary>
        public IReadOnlyList<PlayTypeParameters> PlayTypes { get; internal set; }
        /// <summary>Exponent of the attacking/defending aerial strength ratio applied to headed shots (duel won).</summary>
        public float AerialDuelExponent { get; internal set; }
        public float BlockChance { get; internal set; }
        public CornerParameters Corners { get; internal set; }
        public PenaltyParameters Penalties { get; internal set; }
        public float AccuracyExponent { get; internal set; }
        public float KeeperExponent { get; internal set; }
        public float ComposureExponent { get; internal set; }
        public float FoulsPerMinute { get; internal set; }
        public float DefendingTeamFoulShare { get; internal set; }
        public IReadOnlyList<float> PressureFoulMult { get; internal set; }
        public IReadOnlyList<float> MentalityAttack { get; internal set; }
        public IReadOnlyList<float> MentalityDefense { get; internal set; }
        public IReadOnlyList<float> LineDefense { get; internal set; }
        public IReadOnlyList<float> LinePossession { get; internal set; }
        public IReadOnlyList<float> PressurePossession { get; internal set; }
        public IReadOnlyList<float> PressureDefense { get; internal set; }
        public int MaxGoalsPerTeam { get; internal set; }
        /// <summary>Goal chance multiplier when a team has no goalkeeper on the pitch.</summary>
        public float NoKeeperGoalMultiplier { get; internal set; }
        public float MaxAttackChance { get; internal set; }
        public float MaxShotProbability { get; internal set; }
        /// <summary>Per-position selection weights, indexed by <see cref="Position"/>.</summary>
        public IReadOnlyList<float> ShooterWeights { get; internal set; }
        public IReadOnlyList<float> HeaderWeights { get; internal set; }
        public IReadOnlyList<float> AssistWeights { get; internal set; }
        public IReadOnlyList<float> CrossWeights { get; internal set; }
        public IReadOnlyList<float> FoulWeights { get; internal set; }
    }
}
