using System;
using System.Collections.Generic;
using Game.Career.World;
using Game.Core.Ids;

namespace Game.Career.Season
{
    public enum CompetitionKind
    {
        League = 0,
        Cup = 1,
    }

    /// <summary>A scheduled match (TECHNICAL_SPEC §4.1). References by Id; result summary filled when played.</summary>
    public sealed class Fixture
    {
        public Id Id { get; internal set; }
        public CompetitionKind Kind { get; internal set; }
        /// <summary>League: division index. Cup: -1.</summary>
        public int DivisionIndex { get; internal set; }
        /// <summary>League round (0-based) or cup round (0 = first round).</summary>
        public int Round { get; internal set; }
        public DateTime Date { get; internal set; }
        public Id HomeClubId { get; internal set; }
        public Id AwayClubId { get; internal set; }
        public bool NeutralVenue { get; internal set; }
        public ulong Seed { get; internal set; }

        public bool Played { get; internal set; }
        public int HomeGoals { get; internal set; }
        public int AwayGoals { get; internal set; }
        public int? HomePenalties { get; internal set; }
        public int? AwayPenalties { get; internal set; }
        public int HomeYellows { get; internal set; }
        public int HomeReds { get; internal set; }
        public int AwayYellows { get; internal set; }
        public int AwayReds { get; internal set; }

        /// <summary>Winner club including shootout; Id.None for a draw.</summary>
        public Id Winner
        {
            get
            {
                if (!Played) return Id.None;
                if (HomeGoals != AwayGoals) return HomeGoals > AwayGoals ? HomeClubId : AwayClubId;
                if (HomePenalties.HasValue) return HomePenalties > AwayPenalties ? HomeClubId : AwayClubId;
                return Id.None;
            }
        }

        public bool Involves(Id clubId) => HomeClubId == clubId || AwayClubId == clubId;
    }

    public sealed class StandingRow
    {
        public Id ClubId { get; internal set; }
        public int Played { get; internal set; }
        public int Won { get; internal set; }
        public int Drawn { get; internal set; }
        public int Lost { get; internal set; }
        public int GoalsFor { get; internal set; }
        public int GoalsAgainst { get; internal set; }
        public int Yellows { get; internal set; }
        public int Reds { get; internal set; }
        public int Points { get; internal set; }
        public int GoalDifference => GoalsFor - GoalsAgainst;
    }

    /// <summary>One division's league edition in a season.</summary>
    public sealed class LeagueEdition
    {
        public int DivisionIndex { get; internal set; }
        public string DivisionName { get; internal set; }
        public IReadOnlyList<Id> ClubIds { get; internal set; }
        public List<Fixture> Fixtures { get; } = new List<Fixture>();
        /// <summary>Seed of the random "Draw" tiebreaker for this edition.</summary>
        public ulong TiebreakSeed { get; internal set; }
    }

    public sealed class CupEdition
    {
        public IReadOnlyList<Id> Qualified { get; internal set; }
        /// <summary>Fixtures per round; a round is created (drawn) when its date is reached.</summary>
        public List<List<Fixture>> Rounds { get; } = new List<List<Fixture>>();
        public int RoundCount { get; internal set; }
        public Id Winner { get; internal set; }
        public Id RunnerUp { get; internal set; }
    }

    public enum CalendarEntryKind
    {
        TransferWindowOpen = 0,
        TransferWindowClose = 1,
        CupRound = 2,
        LeagueRound = 3,
        MonthEnd = 4,
        SeasonEnd = 5,
        YouthIntake = 6,
        WeeklyDevelopment = 7,
    }

    public sealed class CalendarEntry
    {
        public DateTime Date { get; internal set; }
        public CalendarEntryKind Kind { get; internal set; }
        /// <summary>League round or cup round index; -1 otherwise.</summary>
        public int Round { get; internal set; }
    }

    /// <summary>A season (calendar year, GAME_DESIGN §4): calendar, league editions per division and the cup.</summary>
    public sealed class Season
    {
        public int Year { get; internal set; }
        public IReadOnlyList<CalendarEntry> Calendar { get; internal set; }
        public int NextEntry { get; internal set; }
        public IReadOnlyList<LeagueEdition> Leagues { get; internal set; }
        public CupEdition Cup { get; internal set; }
        /// <summary>Formation chosen by the AI for each club this season (X-41).</summary>
        public IReadOnlyDictionary<Id, string> ClubFormations { get; internal set; }
        public bool Finished => NextEntry >= Calendar.Count;
        /// <summary>Matches played by each club this season (minutes share for development).</summary>
        public Dictionary<Id, int> ClubMatches { get; } = new Dictionary<Id, int>();
        /// <summary>This season's cash movements (B6, X-44); archived as a net total per club at season end.</summary>
        public List<LedgerEntry> Ledger { get; } = new List<LedgerEntry>();

        public IEnumerable<Fixture> AllFixtures()
        {
            foreach (var l in Leagues) foreach (var f in l.Fixtures) yield return f;
            foreach (var r in Cup.Rounds) foreach (var f in r) yield return f;
        }
    }

    /// <summary>Archived summary of a finished season (history: champions, final tables, movements).</summary>
    public sealed class SeasonSummary
    {
        public int Year { get; internal set; }
        /// <summary>Final table order (club Ids) per division index.</summary>
        public IReadOnlyList<IReadOnlyList<Id>> FinalTables { get; internal set; }
        public IReadOnlyList<Id> Promoted { get; internal set; }
        public IReadOnlyList<Id> Relegated { get; internal set; }
        public Id CupWinner { get; internal set; }
        public Id CupRunnerUp { get; internal set; }
        public IReadOnlyList<Id> CupQualified { get; internal set; }
        public IReadOnlyList<Id> Retired { get; internal set; }
        /// <summary>Net cash movement per club this season (B6, X-44): the sum of its ledger entries.</summary>
        public IReadOnlyDictionary<Id, long> SeasonNetByClub { get; internal set; }
    }
}
