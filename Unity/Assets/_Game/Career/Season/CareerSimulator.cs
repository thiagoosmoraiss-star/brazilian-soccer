using System;
using System.Collections.Generic;
using Game.Career.Match;
using Game.Career.Players;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Competitions;
using Game.Data.Loading;
using Game.Rules.Match;

namespace Game.Career.Season
{
    /// <summary>
    /// Advances a career date by date (TECHNICAL_SPEC §11): builds each season's calendar from data, resolves matchdays
    /// with the QuickSim, draws the cup, and runs the season transition (promotion/relegation X-39, cup qualification
    /// X-40, archive). Pure C#: 10+ seasons run in `dotnet test` without Unity.
    /// </summary>
    public sealed class CareerSimulator
    {
        private readonly GameDatabase _db;
        private readonly MatchRules _rules;
        private readonly Func<MatchSetup, MatchResult> _resolve;

        public CareerState State { get; }
        private LeagueDefinition League => _db.Competitions.League;
        private CupDefinition CupDef => _db.Competitions.Cup;
        private CompetitionMatchRules MatchRulesDef => _db.Competitions.MatchRules;

        /// <param name="resolve">Match resolver (QuickSim in the career; tests may inject another).</param>
        private CareerSimulator(GameDatabase db, CareerState state, Func<MatchSetup, MatchResult> resolve)
        {
            _db = db;
            _rules = new MatchRules(db);
            _resolve = resolve;
            State = state;
        }

        /// <summary>New career: generates the world and the first season.</summary>
        public static CareerSimulator Start(GameDatabase db, ulong seed, Func<MatchSetup, MatchResult> resolve)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (resolve == null) throw new ArgumentNullException(nameof(resolve));
            var world = WorldGenerator.Generate(db, seed);
            var state = new CareerState { Seed = seed, World = world, Ids = new IdAllocator(world.LastIssuedId) };
            var sim = new CareerSimulator(db, state, resolve);
            Youth.Intake(db, world, state.Ids, world.StartYear, regularIntake: false, sim.SeasonRng("Youth", world.StartYear));
            state.Season = sim.CreateSeason(world.StartYear, sim.FirstSeasonCupQualifiers());
            return sim;
        }

        // ---------------- Advancing ----------------

        /// <summary>Processes the next calendar entry. Returns it (a new season starts after SeasonEnd).</summary>
        public CalendarEntry Advance()
        {
            var season = State.Season;
            var entry = season.Calendar[season.NextEntry];
            switch (entry.Kind)
            {
                case CalendarEntryKind.LeagueRound: PlayLeagueRound(season, entry); break;
                case CalendarEntryKind.CupRound: PlayCupRound(season, entry); break;
                case CalendarEntryKind.WeeklyDevelopment: WeeklyDevelopment(season, entry.Date); break;
                case CalendarEntryKind.YouthIntake:
                    Youth.Intake(_db, State.World, State.Ids, season.Year, regularIntake: true, SeasonRng("Youth", season.Year));
                    break;
                case CalendarEntryKind.SeasonEnd: season.NextEntry++; SeasonTransition(); return entry;
            }
            season.NextEntry++;
            return entry;
        }

        /// <summary>Advances until the current season has ended and the next one has been created.</summary>
        public SeasonSummary PlaySeason()
        {
            int year = State.Season.Year;
            while (State.Season.Year == year) Advance();
            return State.History[State.History.Count - 1];
        }

        // ---------------- Season creation ----------------

        private Season CreateSeason(int year, IReadOnlyList<Id> cupQualified)
        {
            var cal = _db.Calendar;
            var divisions = State.World.DivisionNames;
            var formations = ChooseFormations();

            var leagues = new List<LeagueEdition>();
            DateTime leagueStart = cal.LeagueStart.In(year);
            int rounds = 0;
            for (int d = 0; d < divisions.Count; d++)
            {
                var clubs = new List<Id>();
                foreach (var c in State.World.Clubs) if (c.DivisionIndex == d) clubs.Add(c.Id);
                clubs.Sort();
                var edition = new LeagueEdition
                {
                    DivisionIndex = d,
                    DivisionName = divisions[d],
                    ClubIds = clubs,
                    TiebreakSeed = RngStreams.DeriveSeed(State.Seed, "Season.Tiebreak", (ulong)(year * 16 + d)),
                };
                var rng = new Rng(RngStreams.DeriveSeed(State.Seed, "Season.Schedule", (ulong)(year * 16 + d)));
                var pairs = ScheduleBuilder.DoubleRoundRobin(clubs, rng);
                rounds = pairs.Count;
                for (int r = 0; r < pairs.Count; r++)
                    foreach (var (home, away) in pairs[r])
                        edition.Fixtures.Add(NewFixture(CompetitionKind.League, d, r, leagueStart.AddDays(r * cal.LeagueIntervalDays), home, away, false));
                leagues.Add(edition);
            }

            int cupRounds = 0;
            for (int n = CupDef.Clubs; n > 1; n /= 2) cupRounds++;
            if (cal.CupRounds.Count < cupRounds)
                throw new InvalidOperationException($"calendar.json defines {cal.CupRounds.Count} cup dates; the cup needs {cupRounds}.");
            var cup = new CupEdition { Qualified = cupQualified, RoundCount = cupRounds };

            var entries = new List<CalendarEntry>();
            foreach (var w in cal.TransferWindows)
            {
                entries.Add(new CalendarEntry { Date = new DateTime(year, w.OpenMonth, w.OpenDay), Kind = CalendarEntryKind.TransferWindowOpen, Round = -1 });
                entries.Add(new CalendarEntry { Date = new DateTime(year, w.CloseMonth, w.CloseDay), Kind = CalendarEntryKind.TransferWindowClose, Round = -1 });
            }
            for (int r = 0; r < rounds; r++)
                entries.Add(new CalendarEntry { Date = leagueStart.AddDays(r * cal.LeagueIntervalDays), Kind = CalendarEntryKind.LeagueRound, Round = r });
            for (int r = 0; r < cupRounds; r++)
                entries.Add(new CalendarEntry { Date = cal.CupRounds[r].In(year), Kind = CalendarEntryKind.CupRound, Round = r });
            var seasonEnd = new DateTime(year, cal.SeasonEndMonth, cal.SeasonEndDay);
            for (int m = 1; m <= 12; m++)
            {
                var monthEnd = new DateTime(year, m, DateTime.DaysInMonth(year, m));
                if (monthEnd < seasonEnd) entries.Add(new CalendarEntry { Date = monthEnd, Kind = CalendarEntryKind.MonthEnd, Round = -1 });
            }
            entries.Add(new CalendarEntry { Date = seasonEnd, Kind = CalendarEntryKind.SeasonEnd, Round = -1 });
            // Youth intake at the start of the year (the first season starts with the generated squads).
            if (year > State.World.StartYear)
                entries.Add(new CalendarEntry { Date = new DateTime(year, 1, 1), Kind = CalendarEntryKind.YouthIntake, Round = -1 });
            // Weekly development (TECHNICAL_SPEC §11): every Monday before the season end.
            for (var day = new DateTime(year, 1, 1); day < seasonEnd; day = day.AddDays(1))
                if (day.DayOfWeek == DayOfWeek.Monday)
                    entries.Add(new CalendarEntry { Date = day, Kind = CalendarEntryKind.WeeklyDevelopment, Round = -1 });
            entries.Sort((a, b) => a.Date != b.Date ? a.Date.CompareTo(b.Date) : a.Kind.CompareTo(b.Kind));
            if (entries[entries.Count - 1].Kind != CalendarEntryKind.SeasonEnd)
                throw new InvalidOperationException("calendar.json: a match or window falls after the season end date.");

            return new Season { Year = year, Calendar = entries, Leagues = leagues, Cup = cup, ClubFormations = formations };
        }

        private Fixture NewFixture(CompetitionKind kind, int division, int round, DateTime date, Id home, Id away, bool neutral)
        {
            var id = State.Ids.Next();
            return new Fixture
            {
                Id = id, Kind = kind, DivisionIndex = division, Round = round, Date = date,
                HomeClubId = home, AwayClubId = away, NeutralVenue = neutral,
                Seed = RngStreams.DeriveSeed(State.Seed, "Fixture", (ulong)id.Value),
            };
        }

        /// <summary>AI tactic (X-41): the MVP formation whose automatic lineup has the highest total effective OVR.</summary>
        private Dictionary<Id, string> ChooseFormations()
        {
            var result = new Dictionary<Id, string>();
            foreach (var club in State.World.Clubs)
            {
                var candidates = new List<MatchPlayerSetup>();
                foreach (var p in State.World.SquadOf(club.Id)) candidates.Add(MatchSetupFactory.ToMatchPlayer(p));
                string best = null;
                float bestValue = float.MinValue;
                foreach (var f in _db.Formations)
                {
                    var (starters, _) = Lineups.Pick(_rules, f, candidates, 0);
                    float total = 0f;
                    for (int i = 0; i < starters.Length; i++) total += _rules.EffectiveOvr(starters[i], f.Slots[i].Position);
                    if (total > bestValue) { bestValue = total; best = f.Id; }
                }
                result[club.Id] = best;
            }
            return result;
        }

        // ---------------- Matchdays ----------------

        private void PlayLeagueRound(Season season, CalendarEntry entry)
        {
            foreach (var league in season.Leagues)
                foreach (var f in league.Fixtures)
                    if (f.Round == entry.Round && !f.Played) Play(season, f, requiresWinner: false);
        }

        private void PlayCupRound(Season season, CalendarEntry entry)
        {
            var cup = season.Cup;
            IReadOnlyList<Id> clubs;
            if (entry.Round == 0) clubs = cup.Qualified;
            else
            {
                var winners = new List<Id>();
                foreach (var f in cup.Rounds[entry.Round - 1]) winners.Add(f.Winner);
                clubs = winners;
            }

            var round = DrawCupRound(season, clubs, entry);
            cup.Rounds.Add(round);
            foreach (var f in round) Play(season, f, requiresWinner: true);

            if (entry.Round == cup.RoundCount - 1)
            {
                var final = round[0];
                cup.Winner = final.Winner;
                cup.RunnerUp = final.Winner == final.HomeClubId ? final.AwayClubId : final.HomeClubId;
            }
        }

        private List<Fixture> DrawCupRound(Season season, IReadOnlyList<Id> clubs, CalendarEntry entry)
        {
            var rng = new Rng(RngStreams.DeriveSeed(State.Seed, "Season.CupDraw", (ulong)(season.Year * 16 + entry.Round)));
            var pot = new List<Id>(clubs);
            pot.Sort();
            for (int i = pot.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (pot[i], pot[j]) = (pot[j], pot[i]);
            }
            bool final = pot.Count == 2;
            var fixtures = new List<Fixture>();
            for (int i = 0; i + 1 < pot.Count; i += 2)
            {
                Id home = pot[i], away = pot[i + 1];
                if (CupDef.Home == CupHomeRule.LowerDivision && State.Club(away).DivisionIndex > State.Club(home).DivisionIndex)
                    (home, away) = (away, home);
                fixtures.Add(NewFixture(CompetitionKind.Cup, -1, entry.Round, entry.Date, home, away, final && CupDef.FinalNeutral));
            }
            return fixtures;
        }

        private void Play(Season season, Fixture f, bool requiresWinner)
        {
            var mr = MatchRulesDef;
            MatchTeamSetup Team(Id club)
            {
                var tactic = new TacticSetup(season.ClubFormations[club], mr.AiMentality, mr.AiDefensiveLine, mr.AiPressure);
                return MatchSetupFactory.Team(_rules, _db, club, tactic, Candidates(club, f.Kind, f.Date), mr.BenchSize);
            }

            var setup = new MatchSetup(Team(f.HomeClubId), Team(f.AwayClubId), f.NeutralVenue, mr.DurationMinutes, mr.MaxSubstitutions,
                f.Seed, requiresWinner);
            LineupsSelected?.Invoke(f, setup);
            var r = _resolve(setup);
            f.Played = true;
            f.HomeGoals = r.HomeGoals;
            f.AwayGoals = r.AwayGoals;
            f.HomePenalties = r.HomePenalties;
            f.AwayPenalties = r.AwayPenalties;
            f.HomeYellows = r.HomeStats.YellowCards;
            f.HomeReds = r.HomeStats.RedCards;
            f.AwayYellows = r.AwayStats.YellowCards;
            f.AwayReds = r.AwayStats.RedCards;
            if (requiresWinner && f.Winner.IsNone) throw new InvalidOperationException($"Knockout fixture {f.Id} ended without a winner.");

            var rng = SeasonRng("Condition", season.Year, f.Id.Value);
            ConditionSystem.AfterMatch(_db, State.World.SquadOf(f.HomeClubId), MatchSide.Home, r, f.Kind, f.Date, rng);
            ConditionSystem.AfterMatch(_db, State.World.SquadOf(f.AwayClubId), MatchSide.Away, r, f.Kind, f.Date, rng);
            foreach (var club in new[] { f.HomeClubId, f.AwayClubId })
                season.ClubMatches[club] = season.ClubMatches.TryGetValue(club, out int n) ? n + 1 : 1;
            MatchPlayed?.Invoke(f, setup, r);
        }

        /// <summary>Raised before a match is resolved, with the condition still as it was before the match.</summary>
        public event Action<Fixture, MatchSetup> LineupsSelected;

        /// <summary>Raised after every match (tests and tools can observe lineups and results).</summary>
        public event Action<Fixture, MatchSetup, MatchResult> MatchPlayed;

        /// <summary>
        /// Available players (not injured, not suspended in this competition) with their condition. If fewer than 11 are
        /// available, the least-affected unavailable players fill the gap (a match always has 11 starters).
        /// </summary>
        public List<MatchPlayerSetup> Candidates(Id club, CompetitionKind kind, DateTime date)
        {
            var d = _db.Development;
            var available = new List<MatchPlayerSetup>();
            var others = new List<Player>();
            foreach (var p in State.World.SquadOf(club))
            {
                if (ConditionSystem.Available(p, kind, date))
                    available.Add(MatchSetupFactory.ToMatchPlayer(p, ConditionSystem.EnergyAt(d, p, date), p.Condition.Morale, p.Condition.Form));
                else others.Add(p);
            }
            if (available.Count < MatchTeamSetup.StarterCount)
            {
                others.Sort((a, b) =>
                {
                    int ka = a.Condition.InjuryMatchesLeft + a.Condition.SuspendedMatches((int)kind) + (a.Condition.InjuredUntil.HasValue ? 1000 : 0);
                    int kb = b.Condition.InjuryMatchesLeft + b.Condition.SuspendedMatches((int)kind) + (b.Condition.InjuredUntil.HasValue ? 1000 : 0);
                    return ka != kb ? ka.CompareTo(kb) : a.Id.CompareTo(b.Id);
                });
                for (int i = 0; i < others.Count && available.Count < MatchTeamSetup.StarterCount; i++)
                    available.Add(MatchSetupFactory.ToMatchPlayer(others[i], ConditionSystem.EnergyAt(d, others[i], date), others[i].Condition.Morale, others[i].Condition.Form));
            }
            return available;
        }

        private void WeeklyDevelopment(Season season, DateTime date)
        {
            var rng = SeasonRng("Development", season.Year, date.DayOfYear);
            foreach (var p in State.World.Players)
            {
                int age = p.BirthDate.AgeOn(date.Year, date.Month, date.Day);
                var club = State.World.ClubOf(p.Id);
                int clubMatches = season.ClubMatches.TryGetValue(club, out int n) ? n : 0;
                // Early in a season the previous season's share stands in for the minutes played.
                float share = clubMatches >= 5 ? Math.Min(1f, p.Condition.SeasonMinutes / (90f * clubMatches)) : p.Condition.PreviousMinutesShare;
                Development.Week(_db, p, age, share, rng);
            }
        }

        private Rng SeasonRng(string system, int year, int index = 0) =>
            new Rng(RngStreams.DeriveSeed(State.Seed, "Career." + system, (ulong)(year * 1000 + index)));

        // ---------------- Season transition ----------------

        private IReadOnlyList<Id> FirstSeasonCupQualifiers()
        {
            // X-40: first season, the highest squad average OVR per division (ties by Id).
            var result = new List<Id>();
            for (int d = 0; d < State.World.DivisionNames.Count; d++)
            {
                var clubs = new List<(Id id, double ovr)>();
                foreach (var c in State.World.Clubs)
                    if (c.DivisionIndex == d) clubs.Add((c.Id, WorldValidator.SquadAverageOvr(State.World, _db, c.Id)));
                clubs.Sort((a, b) => a.ovr != b.ovr ? b.ovr.CompareTo(a.ovr) : a.id.CompareTo(b.id));
                for (int i = 0; i < CupDef.QualifiersPerDivision && i < clubs.Count; i++) result.Add(clubs[i].id);
            }
            return result;
        }

        private void SeasonTransition()
        {
            var season = State.Season;
            var tables = new List<IReadOnlyList<Id>>();
            foreach (var l in season.Leagues)
            {
                var order = new List<Id>();
                foreach (var row in Standings.Table(League, l)) order.Add(row.ClubId);
                tables.Add(order);
            }

            // X-39: the top N of each lower division swap with the bottom N of the division above.
            var promoted = new List<Id>();
            var relegated = new List<Id>();
            for (int d = 1; d < tables.Count; d++)
            {
                var below = tables[d];
                var above = tables[d - 1];
                for (int i = 0; i < League.Promoted; i++)
                {
                    promoted.Add(below[i]);
                    relegated.Add(above[above.Count - 1 - i]);
                }
            }
            var divisionNames = State.World.DivisionNames;
            foreach (var id in promoted) { var c = State.Club(id); c.DivisionIndex--; c.DivisionName = divisionNames[c.DivisionIndex]; }
            foreach (var id in relegated) { var c = State.Club(id); c.DivisionIndex++; c.DivisionName = divisionNames[c.DivisionIndex]; }

            // B4: annual development, retirements, season counters (X-42).
            var devRng = SeasonRng("Development", season.Year, 999);
            foreach (var p in State.World.Players) Development.Annual(_db, p, devRng);
            var seasonEnd = season.Calendar[season.Calendar.Count - 1].Date;
            var retired = Retirement.Apply(_db, State.World, seasonEnd, SeasonRng("Retirement", season.Year));
            foreach (var p in State.World.Players)
            {
                var club = State.World.ClubOf(p.Id);
                ConditionSystem.NewSeason(_db.Development, p, season.ClubMatches.TryGetValue(club, out int n) ? n : 0);
            }

            // X-40: next cup = top N of each division's final table.
            var nextCup = new List<Id>();
            foreach (var t in tables)
                for (int i = 0; i < CupDef.QualifiersPerDivision && i < t.Count; i++) nextCup.Add(t[i]);

            State.History.Add(new SeasonSummary
            {
                Year = season.Year,
                FinalTables = tables,
                Promoted = promoted,
                Relegated = relegated,
                CupWinner = season.Cup.Winner,
                CupRunnerUp = season.Cup.RunnerUp,
                CupQualified = season.Cup.Qualified,
                Retired = retired,
            });
            State.Season = CreateSeason(season.Year + 1, nextCup);
        }
    }
}
