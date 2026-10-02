using System;
using System.Collections.Generic;
using Game.Core.Ids;

namespace Game.Core.Contracts.Match
{
    /// <summary>
    /// Single output of MatchEngine and QuickSim (D-19): score, events, team and player statistics,
    /// ratings, final energy, injuries, cards and substitutions. Immutable.
    /// </summary>
    public sealed class MatchResult
    {
        public int HomeGoals { get; }
        public int AwayGoals { get; }
        public IReadOnlyList<MatchEvent> Events { get; }
        public TeamMatchStats HomeStats { get; }
        public TeamMatchStats AwayStats { get; }
        public IReadOnlyList<PlayerMatchStats> PlayerStats { get; }
        /// <summary>Penalty shootout score when a knockout match ended level; null otherwise.</summary>
        public int? HomePenalties { get; }
        public int? AwayPenalties { get; }

        public MatchResult(int homeGoals, int awayGoals, IReadOnlyList<MatchEvent> events,
            TeamMatchStats homeStats, TeamMatchStats awayStats, IReadOnlyList<PlayerMatchStats> playerStats,
            int? homePenalties = null, int? awayPenalties = null)
        {
            if (homePenalties.HasValue != awayPenalties.HasValue) throw new ArgumentException("Both shootout scores are required.");
            HomePenalties = homePenalties;
            AwayPenalties = awayPenalties;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
            Events = Copy(events);
            HomeStats = homeStats ?? throw new ArgumentNullException(nameof(homeStats));
            AwayStats = awayStats ?? throw new ArgumentNullException(nameof(awayStats));
            PlayerStats = Copy(playerStats);
        }

        public int Goals(MatchSide side) => side == MatchSide.Home ? HomeGoals : AwayGoals;

        /// <summary>Winner including the shootout; null for a draw without shootout.</summary>
        public MatchSide? Winner =>
            HomeGoals != AwayGoals ? (HomeGoals > AwayGoals ? MatchSide.Home : MatchSide.Away)
            : HomePenalties.HasValue ? (HomePenalties > AwayPenalties ? MatchSide.Home : MatchSide.Away) : (MatchSide?)null;
        public TeamMatchStats Stats(MatchSide side) => side == MatchSide.Home ? HomeStats : AwayStats;

        private static T[] Copy<T>(IReadOnlyList<T> source)
        {
            var a = new T[source?.Count ?? 0];
            for (int i = 0; i < a.Length; i++) a[i] = source[i];
            return a;
        }
    }

    public enum MatchEventType
    {
        Goal = 0,
        Shot = 1,
        ShotOnTarget = 2,
        Foul = 3,
        YellowCard = 4,
        RedCard = 5,
        Corner = 6,
        Penalty = 7,
        Injury = 8,
        Substitution = 9,
    }

    public enum InjurySeverity
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Severe = 3,
    }

    /// <summary>
    /// One match event. <see cref="PlayerId"/> is the main actor (scorer, shooter, fouler, booked, injured,
    /// player coming off); <see cref="OtherPlayerId"/> is the assistant or the player coming on.
    /// </summary>
    public readonly struct MatchEvent
    {
        public readonly int Minute;
        public readonly MatchEventType Type;
        public readonly MatchSide Side;
        public readonly Id PlayerId;
        public readonly Id OtherPlayerId;

        public MatchEvent(int minute, MatchEventType type, MatchSide side, Id playerId, Id otherPlayerId)
        {
            Minute = minute; Type = type; Side = side; PlayerId = playerId; OtherPlayerId = otherPlayerId;
        }

        public override string ToString() => $"{Minute}' {Type} {Side} {PlayerId}{(OtherPlayerId.IsNone ? "" : " / " + OtherPlayerId)}";
    }

    public sealed class TeamMatchStats
    {
        public int Shots { get; }
        public int ShotsOnTarget { get; }
        /// <summary>Possession share, 0-100.</summary>
        public float Possession { get; }
        public int Fouls { get; }
        public int YellowCards { get; }
        public int RedCards { get; }
        public int Corners { get; }
        public int Substitutions { get; }

        public TeamMatchStats(int shots, int shotsOnTarget, float possession, int fouls, int yellowCards, int redCards,
            int corners, int substitutions)
        {
            Shots = shots; ShotsOnTarget = shotsOnTarget; Possession = possession; Fouls = fouls;
            YellowCards = yellowCards; RedCards = redCards; Corners = corners; Substitutions = substitutions;
        }
    }

    public sealed class PlayerMatchStats
    {
        public Id PlayerId { get; }
        public MatchSide Side { get; }
        public bool Started { get; }
        public int MinutesPlayed { get; }
        public int Goals { get; }
        public int Assists { get; }
        public int Shots { get; }
        public int ShotsOnTarget { get; }
        public int FoulsCommitted { get; }
        public int YellowCards { get; }
        public bool RedCard { get; }
        /// <summary>Match rating 0-10 with one decimal; null for unused substitutes.</summary>
        public float? Rating { get; }
        /// <summary>Energy at the end of the match (or when leaving the pitch), 0-100.</summary>
        public float FinalEnergy { get; }
        public InjurySeverity Injury { get; }

        public PlayerMatchStats(Id playerId, MatchSide side, bool started, int minutesPlayed, int goals, int assists,
            int shots, int shotsOnTarget, int foulsCommitted, int yellowCards, bool redCard, float? rating,
            float finalEnergy, InjurySeverity injury)
        {
            PlayerId = playerId; Side = side; Started = started; MinutesPlayed = minutesPlayed; Goals = goals;
            Assists = assists; Shots = shots; ShotsOnTarget = shotsOnTarget; FoulsCommitted = foulsCommitted;
            YellowCards = yellowCards; RedCard = redCard; Rating = rating; FinalEnergy = finalEnergy; Injury = injury;
        }
    }
}
