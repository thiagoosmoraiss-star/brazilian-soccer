using System.Collections.Generic;

namespace Game.UI.Presenters
{
    /// <summary>Diretoria (F2a, D-32a): the season's main objective, the confidence and the sponsorship, read-only.</summary>
    public sealed class BoardModel
    {
        public HeaderModel Header { get; internal set; }
        public string Title { get; internal set; }
        public string ObjectiveLabel { get; internal set; }
        public string Objective { get; internal set; }
        public string ObjectiveHint { get; internal set; }
        public string ConfidenceLabel { get; internal set; }
        public string Confidence { get; internal set; }
        /// <summary>0-100, for a bar.</summary>
        public int ConfidenceValue { get; internal set; }
        public bool ConfidenceAtRisk { get; internal set; }
        public string ConfidenceHint { get; internal set; }
        public string SponsorshipLabel { get; internal set; }
        public string Sponsorship { get; internal set; }
        public string SponsorshipHint { get; internal set; }
        public ButtonModel Club { get; internal set; }
        public ButtonModel Finances { get; internal set; }
    }

    /// <summary>Clube (F2a, D-32b, D-32c): the facilities and the staff, with the user's commands.</summary>
    public sealed class ClubModel
    {
        public HeaderModel Header { get; internal set; }
        public ButtonModel Back { get; internal set; }
        public string Title { get; internal set; }
        public string FacilitiesTitle { get; internal set; }
        public List<FacilityCardModel> Facilities { get; } = new List<FacilityCardModel>();
        public string StaffTitle { get; internal set; }
        public string StaffHint { get; internal set; }
        public List<StaffCardModel> Staff { get; } = new List<StaffCardModel>();
        /// <summary>The outcome of the last command (done, or why it was refused), or null.</summary>
        public string Message { get; internal set; }
        public bool MessageIsError { get; internal set; }
        /// <summary>The command waiting for the user's confirmation, or null.</summary>
        public ConfirmModel Confirm { get; internal set; }
    }

    public sealed class FacilityCardModel
    {
        /// <summary>The facility type (Game.Career.World.FacilityType as int).</summary>
        public int Type { get; internal set; }
        public string Name { get; internal set; }
        public string Level { get; internal set; }
        public string Detail { get; internal set; }
        /// <summary>D-32b: below the division's minimum stadium level (a warning only), or null.</summary>
        public string Warning { get; internal set; }
        public string Requirement { get; internal set; }
        /// <summary>The work in progress, or null.</summary>
        public string Work { get; internal set; }
        public ButtonModel Upgrade { get; internal set; }
        /// <summary>Why the upgrade is not possible now, or null.</summary>
        public string Reason { get; internal set; }
    }

    public sealed class StaffCardModel
    {
        /// <summary>The role (Game.Career.World.StaffRole as int).</summary>
        public int Role { get; internal set; }
        public string Name { get; internal set; }
        public string Level { get; internal set; }
        public string Wage { get; internal set; }
        public string Effect { get; internal set; }
        public List<StaffLevelOptionModel> Levels { get; } = new List<StaffLevelOptionModel>();
    }

    public sealed class StaffLevelOptionModel
    {
        public int Level { get; internal set; }
        public string Label { get; internal set; }
        /// <summary>The current level.</summary>
        public bool Current { get; internal set; }
        public bool Enabled { get; internal set; }
    }

    public sealed class ConfirmModel
    {
        public string Text { get; internal set; }
        public ButtonModel Confirm { get; internal set; }
        public ButtonModel Cancel { get; internal set; }
    }

    /// <summary>Finanças (F2a, TECHNICAL_SPEC §15 "Resumo + 12 meses"): cash, the season by category and month by month.</summary>
    public sealed class FinancesModel
    {
        public HeaderModel Header { get; internal set; }
        public ButtonModel Back { get; internal set; }
        public string Title { get; internal set; }
        public string Cash { get; internal set; }
        public bool CashNegative { get; internal set; }
        /// <summary>Negative-cash alert or transfer lockout, or null.</summary>
        public string Alert { get; internal set; }
        public string SeasonTitle { get; internal set; }
        public string Revenue { get; internal set; }
        public string Expenses { get; internal set; }
        public string Net { get; internal set; }
        public bool NetNegative { get; internal set; }
        public List<FinanceRowModel> Categories { get; } = new List<FinanceRowModel>();
        public string MonthsTitle { get; internal set; }
        public string MonthHeader { get; internal set; }
        public string RevenueHeader { get; internal set; }
        public string ExpensesHeader { get; internal set; }
        public string NetHeader { get; internal set; }
        public List<FinanceMonthModel> Months { get; } = new List<FinanceMonthModel>();
    }

    public sealed class FinanceRowModel
    {
        public string Label { get; internal set; }
        public string Amount { get; internal set; }
        public bool Negative { get; internal set; }
    }

    public sealed class FinanceMonthModel
    {
        public string Month { get; internal set; }
        public string Revenue { get; internal set; }
        public string Expenses { get; internal set; }
        public string Net { get; internal set; }
        public bool NetNegative { get; internal set; }
        /// <summary>A month still to come this season.</summary>
        public bool Future { get; internal set; }
    }
}
