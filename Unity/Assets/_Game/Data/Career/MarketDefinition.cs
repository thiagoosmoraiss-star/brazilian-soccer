using System.Collections.Generic;
using Game.Data.Effects;
using Game.Data.Loading;

namespace Game.Data.Career
{
    public sealed class ValueParameters
    {
        /// <summary>Base value by OVR, before age/potential/contract/form/division adjustments.</summary>
        public PiecewiseLinearCurve OvrCurve { get; internal set; }
        public PiecewiseLinearCurve AgeMultiplier { get; internal set; }
        public float PotentialBonusPerPoint { get; internal set; }
        public PiecewiseLinearCurve ContractYearsLeftMultiplier { get; internal set; }
        public PiecewiseLinearCurve FormMultiplier { get; internal set; }
        /// <summary>Multiplier by division index (0 = top division).</summary>
        public IReadOnlyList<float> DivisionScale { get; internal set; }
    }

    public sealed class WageParameters
    {
        public PiecewiseLinearCurve OvrCurve { get; internal set; }
        public PiecewiseLinearCurve ReputationMultiplier { get; internal set; }
        public IReadOnlyList<float> DivisionScale { get; internal set; }
    }

    public sealed class InterestParameters
    {
        public float ReputationWeight { get; internal set; }
        public float DivisionWeight { get; internal set; }
        public float WageWeight { get; internal set; }
        public float MinutesWeight { get; internal set; }
        /// <summary>Minimum interest score (0-1) for the player to accept the move.</summary>
        public float AcceptThreshold { get; internal set; }
    }

    public sealed class SellerParameters
    {
        public float ImportanceWeight { get; internal set; }
        public float ContractYearsLeftWeight { get; internal set; }
        public float FinancialWeight { get; internal set; }
    }

    public sealed class NegotiationParameters
    {
        public int MaxRounds { get; internal set; }
        public float ClubRaisePerRound { get; internal set; }
        public float WageRaisePerRound { get; internal set; }
    }

    public sealed class ContractParameters
    {
        public int MinYears { get; internal set; }
        public int MaxYears { get; internal set; }
        public IntRange NewSigningYears { get; internal set; }
        public IntRange RenewalYears { get; internal set; }
        /// <summary>A player ranked at or above this position by squad OVR is offered a renewal.</summary>
        public int RenewalImportanceRank { get; internal set; }
    }

    public sealed class SquadSizeParameters
    {
        public int Min { get; internal set; }
        public int Max { get; internal set; }
    }

    public sealed class MarketBudgetParameters
    {
        /// <summary>Share of the club budget available as transfer funds per window (B6 replaces this proxy).</summary>
        public float TransferFundShare { get; internal set; }
        /// <summary>Share of the club budget the total wage bill must not exceed.</summary>
        public float WageShareOfBudget { get; internal set; }
    }

    public sealed class MarketAiParameters
    {
        public int MaxSigningsPerClubPerWindow { get; internal set; }
        public int MaxReleasesPerClubPerWindow { get; internal set; }
        public int MaxProposalsToManagedClubPerWindow { get; internal set; }
    }

    public sealed class YouthForeignSaleParameters
    {
        public int MinPotential { get; internal set; }
        public int MaxAge { get; internal set; }
        public float ChancePerWindow { get; internal set; }
        public float ValueMultiplier { get; internal set; }
    }

    /// <summary>
    /// Transfer market definition (Data/Career/market.json, B5, baseline v1, X-43): market value, reference wage,
    /// player interest, seller acceptance, negotiation, contracts, squad size band and market AI budget proxy.
    /// </summary>
    public sealed class MarketDefinition
    {
        public ValueParameters Value { get; internal set; }
        public WageParameters Wage { get; internal set; }
        public InterestParameters Interest { get; internal set; }
        public SellerParameters Seller { get; internal set; }
        public NegotiationParameters Negotiation { get; internal set; }
        public ContractParameters Contracts { get; internal set; }
        public SquadSizeParameters Squad { get; internal set; }
        public MarketBudgetParameters Budget { get; internal set; }
        public MarketAiParameters Ai { get; internal set; }
        public YouthForeignSaleParameters YouthForeignSale { get; internal set; }
    }
}
