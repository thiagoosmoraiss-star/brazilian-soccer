using System.Collections.Generic;

namespace Game.UI.Presenters
{
    // Screen models (C3a): everything a view shows, already formatted. Views only lay them out (TECHNICAL_SPEC §15
    // "presenter por tela lendo serviços; tela só exibe").

    public sealed class ButtonModel
    {
        public string Label { get; }
        public bool Enabled { get; }
        public ButtonModel(string label, bool enabled = true) { Label = label; Enabled = enabled; }
    }

    public sealed class HeaderModel
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }
        public string Cash { get; internal set; }
        public bool CashNegative { get; internal set; }
    }

    public sealed class MainMenuModel
    {
        public string Title { get; internal set; }
        public string Tagline { get; internal set; }
        /// <summary>The saved career in one line, or null.</summary>
        public string CareerLine { get; internal set; }
        public ButtonModel Continue { get; internal set; }
        public ButtonModel NewCareer { get; internal set; }
        public ButtonModel Settings { get; internal set; }
    }

    public sealed class ClubOptionModel
    {
        public int ClubId { get; internal set; }
        public string Name { get; internal set; }
        public string Place { get; internal set; }
        public string PrimaryHex { get; internal set; }
        public string SecondaryHex { get; internal set; }
        public string Stars { get; internal set; }
        public string Strength { get; internal set; }
        public string Budget { get; internal set; }
        public string Stadium { get; internal set; }
        public string Objective { get; internal set; }
    }

    public sealed class ClubChoiceModel
    {
        public string Title { get; internal set; }
        public string Intro { get; internal set; }
        /// <summary>Shown when a saved career will be replaced, else null.</summary>
        public string Warning { get; internal set; }
        public List<ClubOptionModel> Options { get; } = new List<ClubOptionModel>();
        public string ChooseLabel { get; internal set; }
        public ButtonModel Back { get; internal set; }
    }

    public sealed class HubModel
    {
        public HeaderModel Header { get; internal set; }
        public string NextTitle { get; internal set; }
        /// <summary>Competition and round of the next match, or the end-of-season text.</summary>
        public string NextCompetition { get; internal set; }
        public string NextOpponent { get; internal set; }
        public string NextDetail { get; internal set; }
        public ButtonModel Primary { get; internal set; }
        public string LastResult { get; internal set; }
        public string SeasonNote { get; internal set; }
        public string Position { get; internal set; }
        public string Objective { get; internal set; }
        public string Confidence { get; internal set; }
        public ButtonModel Calendar { get; internal set; }
        public ButtonModel Settings { get; internal set; }
        public ButtonModel MainMenu { get; internal set; }
    }

    public sealed class LineupRowModel
    {
        public string Position { get; internal set; }
        public string Name { get; internal set; }
        public string Ovr { get; internal set; }
        public string Energy { get; internal set; }
        public bool Tired { get; internal set; }
    }

    public sealed class PreMatchModel
    {
        public string Title { get; internal set; }
        public string Competition { get; internal set; }
        public string Date { get; internal set; }
        public string HomeName { get; internal set; }
        public string AwayName { get; internal set; }
        public string HomeHex { get; internal set; }
        public string AwayHex { get; internal set; }
        public string HomeStrength { get; internal set; }
        public string AwayStrength { get; internal set; }
        public bool UserIsHome { get; internal set; }
        public string Formation { get; internal set; }
        public string LineupNote { get; internal set; }
        public List<LineupRowModel> Lineup { get; } = new List<LineupRowModel>();
        public ButtonModel Play { get; internal set; }
        public ButtonModel Simulate { get; internal set; }
        public ButtonModel Back { get; internal set; }
    }

    public sealed class StatRowModel
    {
        public string Label { get; internal set; }
        public string Home { get; internal set; }
        public string Away { get; internal set; }
    }

    public sealed class PostMatchModel
    {
        public string Outcome { get; internal set; }
        /// <summary>-1 loss, 0 draw, 1 win (for the outcome colour).</summary>
        public int OutcomeSign { get; internal set; }
        public string Competition { get; internal set; }
        public string HomeName { get; internal set; }
        public string AwayName { get; internal set; }
        public string Score { get; internal set; }
        public string Penalties { get; internal set; }
        public string How { get; internal set; }
        public List<string> HomeEvents { get; } = new List<string>();
        public List<string> AwayEvents { get; } = new List<string>();
        public List<StatRowModel> Stats { get; } = new List<StatRowModel>();
        public string BestPlayer { get; internal set; }
        public List<StatRowModel> Ratings { get; } = new List<StatRowModel>();
        public string RatingsTitle { get; internal set; }
        public string PositionChange { get; internal set; }
        public ButtonModel Continue { get; internal set; }
    }

    public enum TableZone { None, Promotion, Relegation }

    public sealed class TableRowModel
    {
        public string Pos { get; internal set; }
        public string Name { get; internal set; }
        public string Played { get; internal set; }
        public string Won { get; internal set; }
        public string Drawn { get; internal set; }
        public string Lost { get; internal set; }
        public string GoalDiff { get; internal set; }
        public string Points { get; internal set; }
        public TableZone Zone { get; internal set; }
        public bool IsUser { get; internal set; }
    }

    public enum CompetitionsView { Table, Cup, Scorers }

    public sealed class CupRoundModel
    {
        public string Title { get; internal set; }
        public List<string> Lines { get; } = new List<string>();
        public List<bool> UserLine { get; } = new List<bool>();
    }

    public sealed class ScorerRowModel
    {
        public string Pos { get; internal set; }
        public string Name { get; internal set; }
        public string Club { get; internal set; }
        public string Goals { get; internal set; }
        public bool IsUser { get; internal set; }
    }

    public sealed class CompetitionsModel
    {
        public HeaderModel Header { get; internal set; }
        public CompetitionsView View { get; internal set; }
        public List<ButtonModel> ViewTabs { get; } = new List<ButtonModel>();
        public int Division { get; internal set; }
        public List<ButtonModel> Divisions { get; } = new List<ButtonModel>();
        public string Title { get; internal set; }
        public string TableHeader { get; internal set; }
        public string Legend { get; internal set; }
        public List<TableRowModel> Table { get; } = new List<TableRowModel>();
        public List<CupRoundModel> Cup { get; } = new List<CupRoundModel>();
        public List<ScorerRowModel> Scorers { get; } = new List<ScorerRowModel>();
        public string Empty { get; internal set; }
    }

    public sealed class CalendarRowModel
    {
        public string Date { get; internal set; }
        public string Competition { get; internal set; }
        public string Opponent { get; internal set; }
        public string Result { get; internal set; }
        /// <summary>-1 loss, 0 draw, 1 win, null not played.</summary>
        public int? ResultSign { get; internal set; }
        public bool IsNext { get; internal set; }
    }

    public sealed class CalendarMonthModel
    {
        public string Title { get; internal set; }
        public List<CalendarRowModel> Rows { get; } = new List<CalendarRowModel>();
    }

    public sealed class CalendarModel
    {
        public HeaderModel Header { get; internal set; }
        public string Title { get; internal set; }
        public List<CalendarMonthModel> Months { get; } = new List<CalendarMonthModel>();
        public ButtonModel Back { get; internal set; }
    }

    public sealed class OptionModel
    {
        public int Value { get; internal set; }
        public string Label { get; internal set; }
        public bool Selected { get; internal set; }
    }

    public sealed class SettingsModel
    {
        public string Title { get; internal set; }
        public string DurationLabel { get; internal set; }
        public string DurationHint { get; internal set; }
        public List<OptionModel> Durations { get; } = new List<OptionModel>();
        public string Language { get; internal set; }
        public ButtonModel Close { get; internal set; }
    }

    /// <summary>Tabs whose screens arrive in later sub-stages (X-95).</summary>
    public sealed class PlaceholderModel
    {
        public HeaderModel Header { get; internal set; }
        public string Title { get; internal set; }
        public string Text { get; internal set; }
    }

    public sealed class TabBarModel
    {
        public List<ButtonModel> Tabs { get; } = new List<ButtonModel>();
        public int Selected { get; internal set; }
    }
}
