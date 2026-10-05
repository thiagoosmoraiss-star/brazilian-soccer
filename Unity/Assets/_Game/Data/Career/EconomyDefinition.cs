using System.Collections.Generic;

namespace Game.Data.Career
{
    public sealed class TvParameters
    {
        /// <summary>Fictional R$ per season from broadcasting, by division index (0 = top division); spread evenly
        /// across the 12 months so a club's income does not depend on which months it happens to play in.</summary>
        public IReadOnlyList<long> PerSeasonByDivision { get; internal set; }
    }

    public sealed class TicketingParameters
    {
        /// <summary>Share of the fan base that attends a home match.</summary>
        public float AttendanceShare { get; internal set; }
        public IReadOnlyList<long> PriceByDivision { get; internal set; }
        /// <summary>Extra share of gate revenue on a win (GAME_DESIGN §11: public reacts to results).</summary>
        public float WinBonusShare { get; internal set; }
    }

    public sealed class SponsorshipParameters
    {
        /// <summary>Fictional R$ per season from the single MVP sponsor, by division.</summary>
        public IReadOnlyList<long> PerSeasonByDivision { get; internal set; }
    }

    public sealed class MaintenanceParameters
    {
        /// <summary>Fictional R$ per season, indexed by Stadium.Level - 1.</summary>
        public IReadOnlyList<long> PerSeasonByStadiumLevel { get; internal set; }
    }

    public sealed class PrizeParameters
    {
        public long Base { get; internal set; }
        /// <summary>Relative to Base, by division index (GAME_DESIGN §11: D=1, C=3, B=8, A=40).</summary>
        public IReadOnlyList<float> LeagueChampionMultiplier { get; internal set; }
        /// <summary>Relative to Base (GAME_DESIGN §11: Copa=25).</summary>
        public float CupWinnerMultiplier { get; internal set; }
        public float CupRunnerUpMultiplier { get; internal set; }
    }

    public sealed class CashAlertParameters
    {
        /// <summary>Consecutive months with a negative balance before the club is flagged (GAME_DESIGN §11).</summary>
        public int AlertMonths { get; internal set; }
        /// <summary>Consecutive months before the market stops signing for the club.</summary>
        public int TransferLockoutMonths { get; internal set; }
    }

    /// <summary>Club economy (Data/Career/economy.json, B6, baseline v1, X-44): TV, gate, sponsorship, prizes,
    /// maintenance and the negative-cash thresholds.</summary>
    public sealed class EconomyDefinition
    {
        public TvParameters Tv { get; internal set; }
        public TicketingParameters Ticketing { get; internal set; }
        public SponsorshipParameters Sponsorship { get; internal set; }
        public MaintenanceParameters Maintenance { get; internal set; }
        public PrizeParameters Prizes { get; internal set; }
        public CashAlertParameters CashAlert { get; internal set; }
    }
}
