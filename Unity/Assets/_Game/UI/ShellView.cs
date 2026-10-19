using System;
using Game.UI.Presenters;
using UnityEngine;
using UnityEngine.UIElements;
using static Game.UI.UiKit;
using Tab = Game.UI.Presenters.Tab;

namespace Game.UI
{
    /// <summary>
    /// The Shell's screens in UI Toolkit, built in code (C3a, X-95; TECHNICAL_SPEC §15: retrato, navegação por pilha).
    /// Shows <see cref="ShellNavigator.Current"/> from the <see cref="ShellPresenter"/>'s models and forwards taps to its
    /// commands; no game logic here. Layout: header (career screens) · scrolling content · bottom tab bar.
    /// </summary>
    public sealed class ShellView
    {
        private readonly ShellPresenter _shell;
        private readonly Action _loadMatch;
        private readonly VisualElement _root;
        private readonly VisualElement _header;
        private readonly ScrollView _scroll;
        private readonly VisualElement _tabBar;

        public ShellView(VisualElement root, ShellPresenter shell, Action loadMatch)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _loadMatch = loadMatch ?? throw new ArgumentNullException(nameof(loadMatch));

            _root.style.backgroundColor = Base;
            _root.style.flexDirection = FlexDirection.Column;
            _header = new VisualElement();
            _header.style.flexShrink = 0;
            _header.style.paddingLeft = _header.style.paddingRight = 36;
            _header.style.paddingTop = 24;
            _header.style.paddingBottom = 16;
            _header.style.backgroundColor = CardColor;
            _root.Add(_header);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            _scroll.contentContainer.style.paddingLeft = _scroll.contentContainer.style.paddingRight = 36;
            _scroll.contentContainer.style.paddingTop = 28;
            _scroll.contentContainer.style.paddingBottom = 60;
            _root.Add(_scroll);
            _tabBar = Row(_root);
            _tabBar.style.backgroundColor = CardColor;
            _tabBar.style.paddingLeft = _tabBar.style.paddingRight = 8;
            _tabBar.style.paddingBottom = 8;

            _shell.Nav.Changed += Render;
            Render();
        }

        public void Dispose() => _shell.Nav.Changed -= Render;

        /// <summary>Extra top padding for the phone's status bar / notch (reference px).</summary>
        public void SetTopInset(float inset) => _root.style.paddingTop = inset;

        /// <summary>Android back: pops the stack (from a tab root, nothing).</summary>
        public bool Back()
        {
            if (_shell.Nav.Current == ScreenId.ClubChoice) { _shell.CancelClubChoice(); return true; }
            return _shell.Back();
        }

        private void Render()
        {
            var nav = _shell.Nav;
            var c = _scroll.contentContainer;
            c.Clear();
            _scroll.scrollOffset = Vector2.zero;
            _header.Clear();
            bool career = _shell.Session.HasCareer && nav.ShowsTabBar;
            _header.style.display = career ? DisplayStyle.Flex : DisplayStyle.None;
            if (career) Header(_shell.Header());
            _tabBar.style.display = nav.ShowsTabBar ? DisplayStyle.Flex : DisplayStyle.None;
            if (nav.ShowsTabBar) TabBar(_shell.TabBar());

            switch (nav.Current)
            {
                case ScreenId.MainMenu: MainMenu(c, _shell.MainMenu()); break;
                case ScreenId.ClubChoice: ClubChoice(c, _shell.ClubChoice()); break;
                case ScreenId.Hub: Hub(c, _shell.Hub()); break;
                case ScreenId.PreMatch: PreMatch(c, _shell.PreMatch()); break;
                case ScreenId.PostMatch: PostMatch(c, _shell.PostMatch()); break;
                case ScreenId.Competitions: Competitions(c, _shell.Competitions()); break;
                case ScreenId.Calendar: Calendar(c, _shell.Calendar()); break;
                case ScreenId.Settings: Settings(c, _shell.Settings()); break;
                default: Placeholder(c, _shell.Placeholder(nav.Current)); break;
            }
        }

        // ---------------------------------------------------------------- frame

        private void Header(HeaderModel m)
        {
            var row = Row(_header);
            var left = new VisualElement();
            left.style.flexGrow = 1;
            left.style.flexShrink = 1;
            row.Add(left);
            Text(left, m.Title, BigSize, Surface, true);
            Text(left, m.Subtitle, SmallSize, Neutral);
            var cash = Text(row, m.Cash, TextSize, m.CashNegative ? Negative : Positive, true);
            cash.style.marginLeft = 16;
        }

        private void TabBar(TabBarModel m)
        {
            _tabBar.Clear();
            for (int i = 0; i < m.Tabs.Count; i++)
            {
                int index = i;
                var b = Button(_tabBar, m.Tabs[i], () => _shell.SelectTab((Tab)index), i == m.Selected, SmallSize);
                b.style.marginLeft = b.style.marginRight = 3;
                b.style.paddingLeft = b.style.paddingRight = 4;
                b.style.flexBasis = 0;
            }
        }

        private void BackButton(VisualElement c, ButtonModel model, Action onBack)
        {
            var row = Row(c);
            var b = Button(row, model, onBack);
            b.style.flexGrow = 0;
            b.style.minWidth = 260;
        }

        // ---------------------------------------------------------------- screens

        private void MainMenu(VisualElement c, MainMenuModel m)
        {
            var spacer = new VisualElement();
            spacer.style.height = 260;
            c.Add(spacer);
            var title = Text(c, m.Title, 140, Accent, true);
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            var tagline = Text(c, m.Tagline, TextSize, Surface);
            tagline.style.unityTextAlign = TextAnchor.MiddleCenter;
            tagline.style.marginBottom = 120;
            if (m.CareerLine != null)
            {
                var line = Text(c, m.CareerLine, SmallSize, Neutral);
                line.style.unityTextAlign = TextAnchor.MiddleCenter;
            }
            Button(c, m.Continue, _shell.ContinueCareer, primary: true, BigSize);
            Button(c, m.NewCareer, _shell.NewCareer);
            Button(c, m.Settings, _shell.OpenSettings);
        }

        private void ClubChoice(VisualElement c, ClubChoiceModel m)
        {
            BackButton(c, m.Back, () => _shell.CancelClubChoice());
            Text(c, m.Title, TitleSize, Accent, true);
            Text(c, m.Intro, TextSize);
            if (m.Warning != null) Text(c, m.Warning, SmallSize, Negative, true);
            foreach (var o in m.Options)
            {
                var card = Card(c);
                var top = Row(card);
                Stripe(top, Hex(o.PrimaryHex), 22, 90).style.marginRight = 6;
                Stripe(top, Hex(o.SecondaryHex), 22, 90).style.marginRight = 20;
                var names = new VisualElement();
                names.style.flexGrow = 1;
                names.style.flexShrink = 1;
                top.Add(names);
                Text(names, o.Name, BigSize, Surface, true);
                Text(names, o.Place + "   " + o.Stars, SmallSize, Neutral);
                Text(card, o.Strength + " · " + o.Budget, TextSize);
                Text(card, o.Stadium, SmallSize, Neutral);
                Text(card, o.Objective, SmallSize, Accent);
                int id = o.ClubId;
                Button(card, new ButtonModel(m.ChooseLabel), () => _shell.ChooseClub(id), primary: true);
            }
        }

        private void Hub(VisualElement c, HubModel m)
        {
            var next = Card(c, CardStrong);
            Text(next, m.NextTitle, SmallSize, Neutral);
            Text(next, m.NextCompetition, TextSize, Accent, true);
            if (m.NextOpponent != null) Text(next, m.NextOpponent, BigSize, Surface, true);
            if (m.NextDetail != null) Text(next, m.NextDetail, SmallSize, Neutral);
            var primary = Button(next, m.Primary, _shell.HubPrimary, primary: true, BigSize);
            primary.style.minHeight = MinTouch * 1.3f;
            primary.style.marginTop = 16;

            var info = Card(c);
            if (m.SeasonNote != null) Text(info, m.SeasonNote, TextSize, Accent, true);
            if (m.LastResult != null) Text(info, m.LastResult, TextSize);
            if (m.Position != null) Text(info, m.Position, TextSize);
            if (m.Objective != null) Text(info, m.Objective, SmallSize, Neutral);
            if (m.Confidence != null) Text(info, m.Confidence, SmallSize, Neutral);

            var row = Row(c);
            Button(row, m.Calendar, _shell.OpenCalendar);
            Button(row, m.Settings, _shell.OpenSettings);
            Button(c, m.MainMenu, _shell.OpenMainMenu, size: SmallSize);
        }

        private void PreMatch(VisualElement c, PreMatchModel m)
        {
            if (m == null) { _shell.ContinueAfterMatch(); return; }
            BackButton(c, m.Back, () => _shell.Back());
            Text(c, m.Title, TitleSize, Accent, true);
            Text(c, m.Competition + " · " + m.Date, TextSize, Neutral);

            var vs = Card(c, CardStrong);
            Team(vs, m.HomeName, m.HomeHex, m.HomeStrength, m.UserIsHome);
            var x = Text(vs, "×", BigSize, Neutral, true);
            x.style.unityTextAlign = TextAnchor.MiddleCenter;
            Team(vs, m.AwayName, m.AwayHex, m.AwayStrength, !m.UserIsHome);

            var lineup = Card(c);
            Text(lineup, m.Formation, TextSize, Accent, true);
            Text(lineup, m.LineupNote, SmallSize, Neutral);
            foreach (var p in m.Lineup)
            {
                var row = Row(lineup);
                row.style.minHeight = 64;
                Cell(row, p.Position, 110, SmallSize, Accent, true, TextAnchor.MiddleLeft);
                Cell(row, p.Name, 0, SmallSize, Surface, false, TextAnchor.MiddleLeft);
                Cell(row, p.Ovr, 100, SmallSize, Surface, true);
                Cell(row, p.Energy, 120, SmallSize, Neutral);
            }
            var buttons = Row(c);
            Button(buttons, m.Play, () => { if (_shell.Play() == ShellEffect.LoadMatchScene) _loadMatch(); }, primary: true, BigSize);
            Button(buttons, m.Simulate, _shell.Simulate, size: BigSize);
        }

        private static void Team(VisualElement parent, string name, string hex, string strength, bool user)
        {
            var row = Row(parent);
            Stripe(row, Hex(hex), 22, 70).style.marginRight = 18;
            var label = Cell(row, name, 0, TextSize, user ? Accent : Surface, true, TextAnchor.MiddleLeft);
            label.style.whiteSpace = WhiteSpace.Normal;
            Cell(row, strength, 180, SmallSize, Neutral);
        }

        private void PostMatch(VisualElement c, PostMatchModel m)
        {
            if (m == null) { _shell.ContinueAfterMatch(); return; }
            var outcome = Text(c, m.Outcome, 90, Sign(m.OutcomeSign), true);
            outcome.style.unityTextAlign = TextAnchor.MiddleCenter;
            var comp = Text(c, m.Competition + " · " + m.How, SmallSize, Neutral);
            comp.style.unityTextAlign = TextAnchor.MiddleCenter;

            var score = Card(c, CardStrong);
            var row = Row(score);
            Cell(row, m.HomeName, 0, TextSize, Surface, true, TextAnchor.MiddleRight).style.whiteSpace = WhiteSpace.Normal;
            Cell(row, m.Score, 220, 64, Accent, true);
            Cell(row, m.AwayName, 0, TextSize, Surface, true, TextAnchor.MiddleLeft).style.whiteSpace = WhiteSpace.Normal;
            if (m.Penalties != null) Text(score, m.Penalties, SmallSize, Neutral).style.unityTextAlign = TextAnchor.MiddleCenter;
            var events = Row(score);
            events.style.alignItems = Align.FlexStart;
            var home = new VisualElement(); home.style.flexGrow = 1; home.style.flexBasis = 0; events.Add(home);
            var away = new VisualElement(); away.style.flexGrow = 1; away.style.flexBasis = 0; events.Add(away);
            foreach (var e in m.HomeEvents) Text(home, e, SmallSize).style.unityTextAlign = TextAnchor.MiddleRight;
            foreach (var e in m.AwayEvents) Text(away, e, SmallSize).style.marginLeft = 24;

            var stats = Card(c);
            foreach (var s in m.Stats)
            {
                var r = Row(stats);
                r.style.minHeight = 56;
                Cell(r, s.Home, 160, TextSize, Surface, true);
                Cell(r, s.Label, 0, SmallSize, Neutral);
                Cell(r, s.Away, 160, TextSize, Surface, true);
            }
            if (m.PositionChange != null || m.BestPlayer != null)
            {
                var notes = Card(c);
                if (m.BestPlayer != null) Text(notes, m.BestPlayer, TextSize, Accent);
                if (m.PositionChange != null) Text(notes, m.PositionChange, TextSize);
            }
            var ratings = Card(c);
            Text(ratings, m.RatingsTitle, TextSize, Accent, true);
            foreach (var p in m.Ratings)
            {
                var r = Row(ratings);
                r.style.minHeight = 56;
                Cell(r, p.Label, 0, SmallSize, Surface, false, TextAnchor.MiddleLeft);
                Cell(r, p.Home, 160, SmallSize, Neutral);
                Cell(r, p.Away, 110, SmallSize, Surface, true);
            }
            Button(c, m.Continue, _shell.ContinueAfterMatch, primary: true, BigSize);
        }

        private void Competitions(VisualElement c, CompetitionsModel m)
        {
            var views = Row(c);
            for (int i = 0; i < m.ViewTabs.Count; i++)
            {
                int index = i;
                Button(views, m.ViewTabs[i], () => _shell.ShowCompetitions((CompetitionsView)index), (int)m.View == i, SmallSize).style.flexBasis = 0;
            }
            if (m.View != CompetitionsView.Cup)
            {
                var divisions = Row(c);
                for (int i = 0; i < m.Divisions.Count; i++)
                {
                    int index = i;
                    Button(divisions, m.Divisions[i], () => _shell.ShowDivision(index), m.Division == i, SmallSize).style.flexBasis = 0;
                }
            }
            Text(c, m.Title, BigSize, Accent, true);
            if (m.Empty != null) Text(c, m.Empty, TextSize, Neutral);

            if (m.View == CompetitionsView.Table && m.Table.Count > 0)
            {
                var card = Card(c);
                var cols = m.TableHeader.Split('|');
                var head = Row(card);
                TableRow(head, cols[0], cols[1], cols[2], cols[3], cols[4], cols[5], cols[6], cols[7], Neutral, true, Color.clear);
                foreach (var r in m.Table)
                {
                    var row = Row(card);
                    row.style.minHeight = 60;
                    var zone = r.Zone == TableZone.Promotion ? Positive : r.Zone == TableZone.Relegation ? Negative : Color.clear;
                    TableRow(row, r.Pos, r.Name, r.Played, r.Won, r.Drawn, r.Lost, r.GoalDiff, r.Points, r.IsUser ? Accent : Surface, r.IsUser, zone);
                }
                Text(c, m.Legend, SmallSize, Neutral);
            }
            else if (m.View == CompetitionsView.Cup)
            {
                foreach (var round in m.Cup)
                {
                    var card = Card(c);
                    Text(card, round.Title, TextSize, Accent, true);
                    for (int i = 0; i < round.Lines.Count; i++)
                        Text(card, round.Lines[i], SmallSize, round.UserLine[i] ? Accent : Surface, round.UserLine[i]);
                }
            }
            else if (m.Scorers.Count > 0)
            {
                var card = Card(c);
                foreach (var s in m.Scorers)
                {
                    var row = Row(card);
                    row.style.minHeight = 60;
                    var color = s.IsUser ? Accent : Surface;
                    Cell(row, s.Pos, 70, SmallSize, Neutral);
                    Cell(row, s.Name, 0, SmallSize, color, s.IsUser, TextAnchor.MiddleLeft);
                    Cell(row, s.Club, 260, SmallSize, Neutral, false, TextAnchor.MiddleLeft);
                    Cell(row, s.Goals, 80, SmallSize, color, true);
                }
            }
        }

        private static void TableRow(VisualElement row, string pos, string name, string p, string w, string d, string l, string gd, string pts,
            Color color, bool bold, Color zone)
        {
            Stripe(row, zone, 8, 44).style.marginRight = 8;
            Cell(row, pos, 60, SmallSize, Neutral);
            Cell(row, name, 0, SmallSize, color, bold, TextAnchor.MiddleLeft);
            Cell(row, p, 64, SmallSize, color);
            Cell(row, w, 64, SmallSize, color);
            Cell(row, d, 64, SmallSize, color);
            Cell(row, l, 64, SmallSize, color);
            Cell(row, gd, 84, SmallSize, color);
            Cell(row, pts, 84, SmallSize, color, true);
        }

        private void Calendar(VisualElement c, CalendarModel m)
        {
            BackButton(c, m.Back, () => _shell.Back());
            Text(c, m.Title, TitleSize, Accent, true);
            foreach (var month in m.Months)
            {
                Text(c, month.Title, TextSize, Neutral, true).style.marginTop = 16;
                var card = Card(c);
                foreach (var r in month.Rows)
                {
                    var row = Row(card);
                    row.style.minHeight = 76;
                    if (r.IsNext) row.style.backgroundColor = CardStrong;
                    Cell(row, r.Date, 110, SmallSize, r.IsNext ? Accent : Neutral, r.IsNext, TextAnchor.MiddleLeft);
                    var mid = new VisualElement();
                    mid.style.flexGrow = 1;
                    mid.style.flexShrink = 1;
                    row.Add(mid);
                    Text(mid, r.Opponent, SmallSize, Surface, r.IsNext);
                    Text(mid, r.Competition, 24, Neutral);
                    if (r.Result != null)
                    {
                        var result = Text(mid, r.Result, 24, Sign(r.ResultSign ?? 0), true);
                        result.style.marginBottom = 8;
                    }
                }
            }
        }

        private void Settings(VisualElement c, SettingsModel m)
        {
            Text(c, m.Title, TitleSize, Accent, true);
            var card = Card(c);
            Text(card, m.DurationLabel, TextSize, Surface, true);
            var row = Row(card);
            foreach (var o in m.Durations)
            {
                int minutes = o.Value;
                Button(row, new ButtonModel(o.Label), () => _shell.SetMatchMinutes(minutes), o.Selected).style.flexBasis = 0;
            }
            Text(card, m.DurationHint, SmallSize, Neutral);
            Text(Card(c), m.Language, TextSize);
            Button(c, m.Close, () => _shell.Back(), primary: true);
        }

        private static void Placeholder(VisualElement c, PlaceholderModel m)
        {
            Text(c, m.Title, TitleSize, Accent, true);
            Text(Card(c), m.Text, TextSize);
        }
    }
}
