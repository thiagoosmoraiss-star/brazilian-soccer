using System;
using System.Collections.Generic;

namespace Game.Data.Competitions
{
    public enum Tiebreaker
    {
        Wins = 0,
        GoalDifference = 1,
        GoalsFor = 2,
        HeadToHead = 3,
        Cards = 4,
        Draw = 5,
    }

    public enum CupHomeRule
    {
        /// <summary>The club from the lower division hosts; same division: the first club drawn.</summary>
        LowerDivision = 0,
        /// <summary>The first club drawn hosts.</summary>
        FirstDrawn = 1,
    }

    public enum FirstSeasonQualification
    {
        /// <summary>Highest squad average OVR per division.</summary>
        SquadOvr = 0,
    }

    /// <summary>League format (MVP: one double round-robin per division, X-39 promotion/relegation).</summary>
    public sealed class LeagueDefinition
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public int PointsWin { get; internal set; }
        public int PointsDraw { get; internal set; }
        public IReadOnlyList<Tiebreaker> Tiebreakers { get; internal set; }
        public int YellowCardWeight { get; internal set; }
        public int RedCardWeight { get; internal set; }
        public int Promoted { get; internal set; }
        public int Relegated { get; internal set; }
    }

    /// <summary>National cup (MVP: single-match knockout, X-40 qualification).</summary>
    public sealed class CupDefinition
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public int Clubs { get; internal set; }
        public int QualifiersPerDivision { get; internal set; }
        public CupHomeRule Home { get; internal set; }
        public bool FinalNeutral { get; internal set; }
        public FirstSeasonQualification FirstSeasonQualification { get; internal set; }
    }

    public sealed class CompetitionMatchRules
    {
        public int BenchSize { get; internal set; }
        public int MaxSubstitutions { get; internal set; }
        public int DurationMinutes { get; internal set; }
        public int AiMentality { get; internal set; }
        public int AiDefensiveLine { get; internal set; }
        public int AiPressure { get; internal set; }
    }

    public sealed class CompetitionsDefinition
    {
        public LeagueDefinition League { get; internal set; }
        public CupDefinition Cup { get; internal set; }
        public CompetitionMatchRules MatchRules { get; internal set; }
    }

    /// <summary>First occurrence of a weekday within a month.</summary>
    public readonly struct MonthWeekday
    {
        public readonly int Month;
        public readonly DayOfWeek Weekday;
        public MonthWeekday(int month, DayOfWeek weekday) { Month = month; Weekday = weekday; }

        public DateTime In(int year)
        {
            var d = new DateTime(year, Month, 1);
            while (d.DayOfWeek != Weekday) d = d.AddDays(1);
            return d;
        }
    }

    public readonly struct MonthDayRange
    {
        public readonly int OpenMonth, OpenDay, CloseMonth, CloseDay;
        public MonthDayRange(int openMonth, int openDay, int closeMonth, int closeDay)
        {
            OpenMonth = openMonth; OpenDay = openDay; CloseMonth = closeMonth; CloseDay = closeDay;
        }
    }

    /// <summary>Season calendar rules (Data/Competitions/calendar.json, X-41). Season = calendar year (GAME_DESIGN §4).</summary>
    public sealed class CalendarDefinition
    {
        public int MinDaysBetweenMatches { get; internal set; }
        public MonthWeekday LeagueStart { get; internal set; }
        public int LeagueIntervalDays { get; internal set; }
        public IReadOnlyList<MonthWeekday> CupRounds { get; internal set; }
        public IReadOnlyList<MonthDayRange> TransferWindows { get; internal set; }
        public int SeasonEndMonth { get; internal set; }
        public int SeasonEndDay { get; internal set; }
    }
}
