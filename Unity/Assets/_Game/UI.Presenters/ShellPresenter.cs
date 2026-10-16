using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Market;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Data.Definitions;
using Game.Data.Strings;
using Game.Rules.Ovr;

namespace Game.UI.Presenters
{
    /// <summary>What the view must do after a command, beyond showing <see cref="ShellNavigator.Current"/>.</summary>
    public enum ShellEffect
    {
        None,
        /// <summary>"Jogar": load the Match scene (the career's pending match, <see cref="CareerSession.PlaySetup"/>).</summary>
        LoadMatchScene,
    }

    /// <summary>
    /// The Shell's presenter (C3a, X-95): the commands of every screen (they change the career through
    /// <see cref="CareerSession"/> and move <see cref="ShellNavigator"/>) and the screen models the views lay out.
    /// "Fim de partida → próxima em ≤ 3 toques" (ROADMAP C3): Post-Match Continuar → Hub Avançar (straight to the
    /// Pre-Match) → Jogar/Simular.
    /// </summary>
    public sealed class ShellPresenter
    {
        private readonly Func<ulong> _newSeed;
        private CompetitionsView _competitionsView = CompetitionsView.Table;
        private int? _competitionsDivision;
        private Dictionary<Id, Player> _players;

        public CareerSession Session { get; }
        public ShellNavigator Nav { get; }
        private StringTable S => Session.Strings;

        public ShellPresenter(CareerSession session, ShellNavigator nav, Func<ulong> newSeed)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Nav = nav ?? throw new ArgumentNullException(nameof(nav));
            _newSeed = newSeed ?? throw new ArgumentNullException(nameof(newSeed));
        }

        /// <summary>First screen: the post-match report when coming back from the Match scene, else the main menu.</summary>
        public void Start(bool returningFromMatch)
        {
            if (returningFromMatch && Session.HasCareer && Session.LastMatch != null)
            {
                Nav.Reset(ScreenId.Hub);
                Nav.Push(ScreenId.PostMatch);
            }
            else Nav.Reset(ScreenId.MainMenu);
        }

        // ---------------------------------------------------------------- commands

        public void ContinueCareer()
        {
            if (Session.HasCareer) Nav.Reset(ScreenId.Hub);
        }

        public void NewCareer()
        {
            Session.PrepareNewCareer(_newSeed());
            _players = null;
            Nav.Push(ScreenId.ClubChoice);
        }

        public void ChooseClub(int clubId)
        {
            var r = Session.ChooseClub(new Id(clubId));
            if (!r.IsSuccess) throw new InvalidOperationException("The new career could not be saved: " + r);
            _players = null;
            _competitionsDivision = null;
            Nav.Reset(ScreenId.Hub);
        }

        public void CancelClubChoice()
        {
            Session.CancelNewCareer();
            _players = null;
            Nav.Back();
        }

        public void OpenSettings() => Nav.Push(ScreenId.Settings);

        public void SetMatchMinutes(int minutes)
        {
            Session.Settings.SetMatchMinutes(minutes, Session.Db);
            Session.SaveSettings();
            Nav.Replace(ScreenId.Settings);
        }

        public void OpenCalendar() => Nav.Push(ScreenId.Calendar);

        public void OpenMainMenu() => Nav.Reset(ScreenId.MainMenu);

        /// <summary>Hub's dominant button: the pending match's Pre-Match, or Avançar (to the next match, or the season end).</summary>
        public void HubPrimary()
        {
            if (Session.Pending == null)
            {
                Session.AdvanceToNextMatch();
                _players = null;
            }
            if (Session.Pending != null) Nav.Push(ScreenId.PreMatch);
            else Nav.Replace(ScreenId.Hub);
        }

        public ShellEffect Play() => Session.Pending == null ? ShellEffect.None : ShellEffect.LoadMatchScene;

        public void Simulate()
        {
            if (Session.Pending == null) return;
            Session.SimulateMatch();
            _players = null;
            Nav.Replace(ScreenId.PostMatch);
        }

        public void ContinueAfterMatch() => Nav.Reset(ScreenId.Hub);

        public void SelectTab(Tab tab) => Nav.SelectTab(tab);

        public void ShowCompetitions(CompetitionsView view)
        {
            _competitionsView = view;
            Nav.Replace(ScreenId.Competitions);
        }

        public void ShowDivision(int division)
        {
            _competitionsDivision = division;
            Nav.Replace(ScreenId.Competitions);
        }

        public bool Back() => Nav.Back();

        // ---------------------------------------------------------------- shared helpers

        private CareerState State => Session.Career.State;

        private Player PlayerById(Id id)
        {
            if (_players == null)
            {
                _players = new Dictionary<Id, Player>();
                foreach (var p in State.World.Players) _players[p.Id] = p;
            }
            return _players.TryGetValue(id, out var player) ? player : null;
        }

        private string PlayerName(Id id) => PlayerById(id)?.Name ?? "#" + id.Value;

        private string ClubName(Id id) => State.Club(id)?.Name ?? "#" + id.Value;

        private string ClubShort(Id id) => State.Club(id)?.ShortName ?? ClubName(id);

        private string Division(int index)
        {
            foreach (var l in State.Season.Leagues) if (l.DivisionIndex == index) return S.Format("division.name", l.DivisionName);
            return S.Format("division.name", index);
        }

        private DateTime Today
        {
            get
            {
                var season = State.Season;
                return season.NextEntry < season.Calendar.Count ? season.Calendar[season.NextEntry].Date : season.Calendar[season.Calendar.Count - 1].Date;
            }
        }

        public HeaderModel Header()
        {
            var club = Session.ManagedClub;
            return new HeaderModel
            {
                Title = club.Name,
                Subtitle = S.Format("header.subtitle", Division(club.DivisionIndex), State.Season.Year, Fmt.Date(Today)),
                Cash = Fmt.Money(S, club.Balance),
                CashNegative = club.Balance < 0,
            };
        }

        public TabBarModel TabBar()
        {
            var m = new TabBarModel { Selected = (int)Nav.Tab };
            m.Tabs.Add(new ButtonModel(S.Get("tab.club")));
            m.Tabs.Add(new ButtonModel(S.Get("tab.squad")));
            m.Tabs.Add(new ButtonModel(S.Get("tab.market")));
            m.Tabs.Add(new ButtonModel(S.Get("tab.competitions")));
            m.Tabs.Add(new ButtonModel(S.Get("tab.board")));
            return m;
        }

        private string CompetitionLine(Fixture f)
        {
            if (f.Kind == CompetitionKind.Cup) return S.Format("competition.cup_round", Session.Db.Competitions.Cup.Name, CupRoundName(f.Round, State.Season.Cup.RoundCount));
            return S.Format("competition.league_round", Division(f.DivisionIndex), f.Round + 1);
        }

        private string CupRoundName(int round, int roundCount)
        {
            int fromEnd = roundCount - 1 - round;
            switch (fromEnd)
            {
                case 0: return S.Get("cup.round_final");
                case 1: return S.Get("cup.round_semi");
                case 2: return S.Get("cup.round_quarter");
                case 3: return S.Get("cup.round_16");
                default: return S.Format("cup.round_n", round + 1);
            }
        }

        private int Ovr(Player p) => Negotiation.Ovr(Session.Db, p);

        /// <summary>Average OVR of the best eleven (the club's strength in the lists).</summary>
        private int SquadStrength(Id clubId)
        {
            var ovrs = State.World.SquadOf(clubId).Select(Ovr).OrderByDescending(x => x).Take(MatchTeamSetup.StarterCount).ToList();
            return ovrs.Count == 0 ? 0 : (int)Math.Round(ovrs.Average());
        }

        private int TeamStrength(MatchTeamSetup team)
        {
            if (team.Starters.Count == 0) return 0;
            double sum = 0;
            foreach (var p in team.Starters) sum += OvrCalculator.Rating(Session.Db.Ovr, p.Attributes, (Position)p.MainPosition);
            return (int)Math.Round(sum / team.Starters.Count);
        }

        private static int Sign(int mine, int theirs) => mine > theirs ? 1 : mine < theirs ? -1 : 0;

        // ---------------------------------------------------------------- screens

        public MainMenuModel MainMenu()
        {
            var m = new MainMenuModel
            {
                Title = S.Get("app.title"),
                Tagline = S.Get("menu.tagline"),
                Continue = new ButtonModel(S.Get("menu.continue"), Session.HasCareer),
                NewCareer = new ButtonModel(S.Get("menu.new_career")),
                Settings = new ButtonModel(S.Get("menu.settings")),
            };
            if (Session.HasCareer)
            {
                var club = Session.ManagedClub;
                m.CareerLine = S.Format("menu.career_line", club.Name, Division(club.DivisionIndex), State.Season.Year);
            }
            return m;
        }

        public ClubChoiceModel ClubChoice()
        {
            var m = new ClubChoiceModel
            {
                Title = S.Get("choice.title"),
                Intro = S.Format("choice.intro", Division(Session.LowestDivision)),
                ChooseLabel = S.Get("choice.choose"),
                Back = new ButtonModel(S.Get("common.back")),
            };
            if (Session.HasSavedCareer) m.Warning = S.Get("choice.warning_overwrite");
            var options = State.World.Clubs.Where(c => c.DivisionIndex == Session.LowestDivision)
                .Select(c => (Club: c, Strength: SquadStrength(c.Id))).OrderByDescending(x => x.Strength).ThenBy(x => x.Club.Id);
            foreach (var (club, strength) in options)
            {
                State.Season.Objectives.TryGetValue(club.Id, out var objective);
                m.Options.Add(new ClubOptionModel
                {
                    ClubId = club.Id.Value,
                    Name = club.Name,
                    Place = S.Format("choice.place", club.City, club.Uf),
                    PrimaryHex = club.Colors.PrimaryHex,
                    SecondaryHex = club.Colors.SecondaryHex,
                    Stars = Fmt.Stars(club.Stars),
                    Strength = S.Format("choice.strength", strength),
                    Budget = S.Format("choice.budget", Fmt.Money(S, club.Budget)),
                    Stadium = S.Format("choice.stadium", Fmt.Thousands(club.Stadium.Capacity)),
                    Objective = objective == null ? "" : S.Format("choice.objective", ObjectiveText(objective.Type)),
                });
            }
            return m;
        }

        private string ObjectiveText(ObjectiveType type)
        {
            switch (type)
            {
                case ObjectiveType.Promote: return S.Get("objective.promote");
                case ObjectiveType.AvoidRelegation: return S.Get("objective.avoid_relegation");
                default: return S.Get("objective.break_even");
            }
        }

        public HubModel Hub()
        {
            var club = Session.ManagedClub;
            var m = new HubModel
            {
                Header = Header(),
                NextTitle = S.Get("hub.next_title"),
                Calendar = new ButtonModel(S.Get("hub.calendar")),
                Settings = new ButtonModel(S.Get("hub.settings")),
                MainMenu = new ButtonModel(S.Get("hub.main_menu")),
            };
            var pending = Session.Pending;
            if (pending != null)
            {
                var f = pending.Fixture;
                bool home = pending.UserSide == MatchSide.Home;
                var opponent = home ? f.AwayClubId : f.HomeClubId;
                m.NextCompetition = CompetitionLine(f);
                m.NextOpponent = S.Format(home ? "hub.vs_home" : "hub.vs_away", ClubName(opponent));
                m.NextDetail = S.Format("hub.next_detail", Fmt.Date(f.Date), S.Format("choice.strength", SquadStrength(opponent)));
                m.Primary = new ButtonModel(S.Get("hub.go_to_match"));
            }
            else
            {
                var next = NextUserFixture();
                if (next != null)
                {
                    m.NextCompetition = CompetitionLine(next);
                    bool home = next.HomeClubId == club.Id;
                    m.NextOpponent = S.Format(home ? "hub.vs_home" : "hub.vs_away", ClubName(home ? next.AwayClubId : next.HomeClubId));
                    m.NextDetail = S.Format("hub.next_detail", Fmt.Date(next.Date), S.Format("choice.strength", SquadStrength(home ? next.AwayClubId : next.HomeClubId)));
                }
                else m.NextCompetition = S.Get("hub.no_match");
                m.Primary = new ButtonModel(S.Get("hub.advance"));
            }

            if (Session.LastMatch != null)
            {
                var r = Session.LastMatch;
                m.LastResult = S.Format("hub.last_result", ScoreLine(r.Fixture, r.Result));
            }
            m.SeasonNote = SeasonNote(club);
            var position = Session.LeaguePosition();
            if (position.HasValue)
            {
                var row = LeagueTable(club.DivisionIndex).First(x => x.ClubId == club.Id);
                m.Position = S.Format("hub.position", position.Value, Division(club.DivisionIndex), row.Points, row.Played);
            }
            if (State.Season.Objectives.TryGetValue(club.Id, out var objective))
                m.Objective = S.Format("hub.objective", ObjectiveText(objective.Type));
            var manager = State.World.ManagerOf(club.Id);
            if (manager != null) m.Confidence = S.Format("hub.confidence", manager.Confidence);
            return m;
        }

        /// <summary>The managed club's next fixture of the season already scheduled (league, or a drawn cup tie).</summary>
        private Fixture NextUserFixture()
        {
            var id = State.ManagedClubId.Value;
            return State.Season.AllFixtures().Where(f => !f.Played && f.Involves(id)).OrderBy(f => f.Date).FirstOrDefault();
        }

        private string SeasonNote(Club club)
        {
            // Until the club's first match of the new season.
            if (State.History.Count == 0 || State.Season.AllFixtures().Any(f => f.Played && f.Involves(club.Id))) return null;
            var last = State.History[State.History.Count - 1];
            int position = 0, division = -1;
            for (int d = 0; d < last.FinalTables.Count; d++)
            {
                int i = IndexOf(last.FinalTables[d], club.Id);
                if (i >= 0) { position = i + 1; division = d; }
            }
            if (division < 0) return null;
            string note = S.Format("hub.season_ended", last.Year, position, S.Format("division.name", DivisionLetter(division)));
            if (last.Promoted != null && last.Promoted.Contains(club.Id)) note += " " + S.Get("hub.promoted");
            else if (last.Relegated != null && last.Relegated.Contains(club.Id)) note += " " + S.Get("hub.relegated");
            if (last.CupWinner == club.Id) note += " " + S.Get("hub.cup_winner");
            return note;
        }

        private string DivisionLetter(int index)
        {
            foreach (var c in State.World.Clubs) if (c.DivisionIndex == index) return c.DivisionName;
            return index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int IndexOf(IReadOnlyList<Id> list, Id id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return i;
            return -1;
        }

        private string ScoreLine(Fixture f, MatchResult r)
        {
            int hg = r?.HomeGoals ?? f.HomeGoals, ag = r?.AwayGoals ?? f.AwayGoals;
            string line = S.Format("match.score_line", ClubName(f.HomeClubId), hg, ag, ClubName(f.AwayClubId));
            int? hp = r != null ? r.HomePenalties : f.HomePenalties, ap = r != null ? r.AwayPenalties : f.AwayPenalties;
            if (hp.HasValue) line += " " + S.Format("match.penalties", hp, ap);
            return line;
        }

        private List<StandingRow> LeagueTable(int division)
        {
            foreach (var l in State.Season.Leagues)
                if (l.DivisionIndex == division) return Standings.Table(Session.Db.Competitions.League, l);
            return new List<StandingRow>();
        }

        public PreMatchModel PreMatch()
        {
            var pending = Session.Pending;
            if (pending == null) return null;
            var f = pending.Fixture;
            var setup = Session.PlaySetup(pending);
            var home = State.Club(f.HomeClubId);
            var away = State.Club(f.AwayClubId);
            var mine = setup.Team(pending.UserSide);
            var m = new PreMatchModel
            {
                Title = S.Get("prematch.title"),
                Competition = CompetitionLine(f),
                Date = Fmt.Date(f.Date),
                HomeName = home.Name,
                AwayName = away.Name,
                HomeHex = home.Colors.PrimaryHex,
                AwayHex = away.Colors.PrimaryHex,
                HomeStrength = S.Format("choice.strength", TeamStrength(setup.Home)),
                AwayStrength = S.Format("choice.strength", TeamStrength(setup.Away)),
                UserIsHome = pending.UserSide == MatchSide.Home,
                Formation = S.Format("prematch.formation", mine.Tactic.FormationId),
                LineupNote = S.Get("prematch.auto_lineup"),
                Play = new ButtonModel(S.Format("prematch.play", setup.DurationMinutes)),
                Simulate = new ButtonModel(S.Get("prematch.simulate")),
                Back = new ButtonModel(S.Get("common.back")),
            };
            var formation = Session.Db.Formation(mine.Tactic.FormationId);
            for (int i = 0; i < mine.Starters.Count; i++)
            {
                var p = mine.Starters[i];
                var slot = formation != null && i < formation.Slots.Count ? formation.Slots[i].Position : (Position)p.MainPosition;
                m.Lineup.Add(new LineupRowModel
                {
                    Position = slot.ToString(),
                    Name = PlayerName(p.PlayerId),
                    Ovr = OvrCalculator.Rating(Session.Db.Ovr, p.Attributes, slot).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Energy = S.Format("common.percent", (int)Math.Round(p.Energy)),
                });
            }
            return m;
        }

        public PostMatchModel PostMatch()
        {
            var report = Session.LastMatch;
            if (report == null) return null;
            var f = report.Fixture;
            var r = report.Result;
            int mine = r.Goals(report.UserSide), theirs = r.Goals(report.UserSide == MatchSide.Home ? MatchSide.Away : MatchSide.Home);
            int sign = Sign(mine, theirs);
            if (sign == 0 && r.HomePenalties.HasValue)
            {
                int myPens = report.UserSide == MatchSide.Home ? r.HomePenalties.Value : r.AwayPenalties.Value;
                int theirPens = report.UserSide == MatchSide.Home ? r.AwayPenalties.Value : r.HomePenalties.Value;
                sign = Sign(myPens, theirPens);
            }
            var m = new PostMatchModel
            {
                Outcome = S.Get(sign > 0 ? "postmatch.win" : sign < 0 ? "postmatch.loss" : "postmatch.draw"),
                OutcomeSign = sign,
                Competition = CompetitionLine(f),
                HomeName = ClubName(f.HomeClubId),
                AwayName = ClubName(f.AwayClubId),
                Score = S.Format("match.score", r.HomeGoals, r.AwayGoals),
                Penalties = r.HomePenalties.HasValue ? S.Format("match.penalties", r.HomePenalties, r.AwayPenalties) : null,
                How = S.Get(report.Played ? "postmatch.played" : "postmatch.simulated"),
                RatingsTitle = S.Get("postmatch.ratings"),
                Continue = new ButtonModel(S.Get("common.continue")),
            };
            foreach (var e in r.Events)
            {
                string line;
                if (e.Type == MatchEventType.Goal) line = S.Format("postmatch.goal", e.Minute, PlayerName(e.PlayerId));
                else if (e.Type == MatchEventType.RedCard) line = S.Format("postmatch.red", e.Minute, PlayerName(e.PlayerId));
                else continue;
                (e.Side == MatchSide.Home ? m.HomeEvents : m.AwayEvents).Add(line);
            }
            void Stat(string key, string home, string away) => m.Stats.Add(new StatRowModel { Label = S.Get(key), Home = home, Away = away });
            string I(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Stat("stats.possession", S.Format("common.percent", (int)Math.Round(r.HomeStats.Possession)), S.Format("common.percent", (int)Math.Round(r.AwayStats.Possession)));
            Stat("stats.shots", I(r.HomeStats.Shots), I(r.AwayStats.Shots));
            Stat("stats.on_target", I(r.HomeStats.ShotsOnTarget), I(r.AwayStats.ShotsOnTarget));
            Stat("stats.corners", I(r.HomeStats.Corners), I(r.AwayStats.Corners));
            Stat("stats.fouls", I(r.HomeStats.Fouls), I(r.AwayStats.Fouls));
            Stat("stats.yellows", I(r.HomeStats.YellowCards), I(r.AwayStats.YellowCards));
            Stat("stats.reds", I(r.HomeStats.RedCards), I(r.AwayStats.RedCards));

            var rated = r.PlayerStats.Where(p => p.Rating.HasValue).ToList();
            var best = rated.OrderByDescending(p => p.Rating.Value).ThenBy(p => p.PlayerId).FirstOrDefault();
            if (best != null) m.BestPlayer = S.Format("postmatch.best", PlayerName(best.PlayerId), Fmt.Rating(best.Rating.Value));
            foreach (var p in rated.Where(p => p.Side == report.UserSide).OrderByDescending(p => p.Rating.Value).ThenBy(p => p.PlayerId))
                m.Ratings.Add(new StatRowModel
                {
                    Label = PlayerName(p.PlayerId),
                    Home = p.Goals > 0 ? S.Format("postmatch.goals_short", p.Goals) : "",
                    Away = Fmt.Rating(p.Rating.Value),
                });

            if (report.PositionBefore.HasValue)
            {
                var now = Session.LeaguePosition();
                if (now.HasValue) m.PositionChange = S.Format("postmatch.position", report.PositionBefore.Value, now.Value);
            }
            return m;
        }

        public CompetitionsModel Competitions()
        {
            var club = Session.ManagedClub;
            int division = _competitionsDivision ?? club.DivisionIndex;
            var m = new CompetitionsModel { Header = Header(), View = _competitionsView, Division = division };
            m.ViewTabs.Add(new ButtonModel(S.Get("competitions.table")));
            m.ViewTabs.Add(new ButtonModel(Session.Db.Competitions.Cup.Name));
            m.ViewTabs.Add(new ButtonModel(S.Get("competitions.scorers")));
            foreach (var l in State.Season.Leagues.OrderBy(l => l.DivisionIndex))
                m.Divisions.Add(new ButtonModel(l.DivisionName));

            var def = Session.Db.Competitions.League;
            int divisions = State.Season.Leagues.Count;
            switch (_competitionsView)
            {
                case CompetitionsView.Table:
                {
                    m.Title = Division(division);
                    m.TableHeader = S.Get("competitions.table_header");
                    m.Legend = S.Get("competitions.legend");
                    var table = LeagueTable(division);
                    for (int i = 0; i < table.Count; i++)
                    {
                        var row = table[i];
                        var zone = TableZone.None;
                        if (division > 0 && i < def.Promoted) zone = TableZone.Promotion;
                        else if (division < divisions - 1 && i >= table.Count - def.Relegated) zone = TableZone.Relegation;
                        string I(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        m.Table.Add(new TableRowModel
                        {
                            Pos = I(i + 1), Name = ClubShort(row.ClubId), Played = I(row.Played), Won = I(row.Won), Drawn = I(row.Drawn),
                            Lost = I(row.Lost), GoalDiff = Fmt.Signed(row.GoalDifference), Points = I(row.Points), Zone = zone,
                            IsUser = row.ClubId == club.Id,
                        });
                    }
                    break;
                }
                case CompetitionsView.Cup:
                {
                    var cup = State.Season.Cup;
                    m.Title = Session.Db.Competitions.Cup.Name;
                    for (int r = cup.Rounds.Count - 1; r >= 0; r--)
                    {
                        var round = new CupRoundModel { Title = CupRoundName(r, cup.RoundCount) };
                        foreach (var f in cup.Rounds[r])
                        {
                            round.Lines.Add(f.Played ? ScoreLine(f, null)
                                : S.Format("competitions.cup_pending", ClubName(f.HomeClubId), ClubName(f.AwayClubId), Fmt.ShortDate(f.Date)));
                            round.UserLine.Add(f.Involves(club.Id));
                        }
                        m.Cup.Add(round);
                    }
                    if (m.Cup.Count == 0) m.Empty = S.Get("competitions.cup_not_started");
                    break;
                }
                default:
                {
                    m.Title = S.Format("competitions.scorers_title", Division(division));
                    var clubs = new HashSet<Id>(State.World.Clubs.Where(c => c.DivisionIndex == division).Select(c => c.Id));
                    var scorers = new List<(Player Player, Id Club)>();
                    foreach (var c in clubs)
                        foreach (var p in State.World.SquadOf(c))
                            if (p.Condition.SeasonGoals > 0) scorers.Add((p, c));
                    int pos = 0;
                    foreach (var (p, c) in scorers.OrderByDescending(x => x.Player.Condition.SeasonGoals).ThenBy(x => x.Player.Id).Take(ScorersShown))
                        m.Scorers.Add(new ScorerRowModel
                        {
                            Pos = (++pos).ToString(System.Globalization.CultureInfo.InvariantCulture), Name = p.Name, Club = ClubShort(c),
                            Goals = p.Condition.SeasonGoals.ToString(System.Globalization.CultureInfo.InvariantCulture), IsUser = c == club.Id,
                        });
                    if (m.Scorers.Count == 0) m.Empty = S.Get("competitions.no_scorers");
                    break;
                }
            }
            return m;
        }

        /// <summary>Rows of the scorers list (a layout choice, not balance).</summary>
        public const int ScorersShown = 20;

        public CalendarModel Calendar()
        {
            var club = Session.ManagedClub;
            var m = new CalendarModel { Header = Header(), Title = S.Format("calendar.title", State.Season.Year), Back = new ButtonModel(S.Get("common.back")) };
            var pending = Session.Pending?.Fixture;
            var fixtures = State.Season.AllFixtures().Where(f => f.Involves(club.Id)).ToList();
            var next = pending ?? fixtures.Where(f => !f.Played).OrderBy(f => f.Date).FirstOrDefault();
            var rows = new List<(DateTime Date, CalendarRowModel Row)>();
            foreach (var f in fixtures)
            {
                bool home = f.HomeClubId == club.Id;
                var row = new CalendarRowModel
                {
                    Date = Fmt.ShortDate(f.Date),
                    Competition = CompetitionLine(f),
                    Opponent = S.Format(home ? "hub.vs_home" : "hub.vs_away", ClubName(home ? f.AwayClubId : f.HomeClubId)),
                    IsNext = f == next,
                };
                if (f.Played)
                {
                    row.Result = ScoreLine(f, null);
                    int mine = home ? f.HomeGoals : f.AwayGoals, theirs = home ? f.AwayGoals : f.HomeGoals;
                    int s = Sign(mine, theirs);
                    if (s == 0 && f.HomePenalties.HasValue) s = f.Winner == club.Id ? 1 : -1;
                    row.ResultSign = s;
                }
                rows.Add((f.Date, row));
            }
            // Cup rounds still to be drawn, while the club is alive in the cup.
            if (CupAlive(club.Id))
            {
                var cup = State.Season.Cup;
                foreach (var e in State.Season.Calendar)
                    if (e.Kind == CalendarEntryKind.CupRound && e.Round >= cup.Rounds.Count)
                        rows.Add((e.Date, new CalendarRowModel
                        {
                            Date = Fmt.ShortDate(e.Date),
                            Competition = S.Format("competition.cup_round", Session.Db.Competitions.Cup.Name, CupRoundName(e.Round, cup.RoundCount)),
                            Opponent = S.Get("calendar.to_be_drawn"),
                        }));
            }
            CalendarMonthModel month = null;
            int currentMonth = -1;
            foreach (var (date, row) in rows.OrderBy(x => x.Date))
            {
                if (date.Month != currentMonth)
                {
                    currentMonth = date.Month;
                    month = new CalendarMonthModel { Title = Fmt.Month(S, date.Month) };
                    m.Months.Add(month);
                }
                month.Rows.Add(row);
            }
            return m;
        }

        private bool CupAlive(Id clubId)
        {
            var cup = State.Season.Cup;
            if (cup.Qualified == null || !cup.Qualified.Contains(clubId) || !cup.Winner.IsNone) return false;
            foreach (var round in cup.Rounds)
                foreach (var f in round)
                    if (f.Involves(clubId) && f.Played && f.Winner != clubId) return false;
            return true;
        }

        public SettingsModel Settings()
        {
            var m = new SettingsModel
            {
                Title = S.Get("settings.title"),
                DurationLabel = S.Get("settings.duration"),
                DurationHint = S.Get("settings.duration_hint"),
                Language = S.Get("settings.language"),
                Close = new ButtonModel(S.Get("common.close")),
            };
            foreach (int minutes in Session.Db.Competitions.MatchRules.DurationOptions)
                m.Durations.Add(new OptionModel { Value = minutes, Label = S.Format("settings.minutes", minutes), Selected = minutes == Session.Settings.MatchMinutes });
            return m;
        }

        /// <summary>Squad, Market and Board until their sub-stages (C3b–C3d).</summary>
        public PlaceholderModel Placeholder(ScreenId screen)
        {
            var m = new PlaceholderModel { Header = Header() };
            switch (screen)
            {
                case ScreenId.Squad: m.Title = S.Get("placeholder.squad_title"); m.Text = S.Get("placeholder.squad_text"); break;
                case ScreenId.Market: m.Title = S.Get("placeholder.market_title"); m.Text = S.Get("placeholder.market_text"); break;
                default: m.Title = S.Get("placeholder.board_title"); m.Text = S.Get("placeholder.board_text"); break;
            }
            return m;
        }
    }
}
