using System;
using System.Linq;
using Game.Career.Season;
using Game.Match.AI;
using Game.Save;
using Game.UI.Presenters;
using NUnit.Framework;
using static Game.Tests.UI.UiTestSupport;

namespace Game.Tests.UI
{
    /// <summary>
    /// C3a: the Shell driven only through its presenter commands, as the screens do (ROADMAP C3 tests: navigation;
    /// "fim de partida → próxima em ≤ 3 toques"). The Unity views only lay these models out.
    /// </summary>
    public class ShellFlowTests
    {
        [Test]
        public void ANewCareer_OffersTheLowestDivisionClubs_AndStartsSavedWithTheChosenOne()
        {
            var store = NewStore();
            var shell = Shell(store);
            Assert.AreEqual(ScreenId.MainMenu, shell.Nav.Current);
            Assert.IsFalse(shell.MainMenu().Continue.Enabled, "nothing to continue yet");

            shell.NewCareer();
            var choice = shell.ClubChoice();
            AssertTextsComplete(choice, "club choice");
            Assert.IsNull(choice.Warning, "no saved career to replace");
            int lowest = shell.Session.LowestDivision;
            var lowestClubs = shell.Session.Career.State.World.Clubs.Where(c => c.DivisionIndex == lowest).Select(c => c.Id.Value).OrderBy(x => x);
            CollectionAssert.AreEqual(lowestClubs, choice.Options.Select(o => o.ClubId).OrderBy(x => x), "X-93: every club of the lowest division");
            Assert.IsTrue(choice.Options.All(o => o.Objective.Length > 0 && o.Strength.Length > 0));

            var picked = choice.Options[3];
            shell.ChooseClub(picked.ClubId);
            Assert.AreEqual(ScreenId.Hub, shell.Nav.Current);
            Assert.AreEqual(picked.ClubId, shell.Session.ManagedClub.Id.Value);
            Assert.IsTrue(store.Exists(SaveSlotPaths.Body(CareerSession.Slot)), "saved as soon as the club is chosen");

            var reopened = Shell(store);
            Assert.IsTrue(reopened.MainMenu().Continue.Enabled);
            StringAssert.Contains(picked.Name, reopened.MainMenu().CareerLine);
            reopened.ContinueCareer();
            Assert.AreEqual(ScreenId.Hub, reopened.Nav.Current);
            Assert.AreEqual(picked.ClubId, reopened.Session.ManagedClub.Id.Value);
        }

        [Test]
        public void BackingOutOfANewCareer_KeepsTheSavedOne()
        {
            var store = NewStore();
            var shell = NewCareer(store);
            var club = shell.Session.ManagedClub.Id;
            shell.OpenMainMenu();
            shell.NewCareer();
            Assert.IsNotNull(shell.ClubChoice().Warning, "the screen warns before replacing the career");
            shell.CancelClubChoice();
            Assert.AreEqual(ScreenId.MainMenu, shell.Nav.Current);
            Assert.AreEqual(club, shell.Session.ManagedClub.Id);
            Assert.AreEqual(club, Shell(store).Session.ManagedClub.Id, "the save on disk is the old career");
        }

        [Test]
        public void FromTheEndOfAMatchToTheNext_TakesAtMostThreeTaps_AllSeasonLong()
        {
            var shell = NewCareer(NewStore());
            int year = shell.Session.Career.State.Season.Year;
            shell.HubPrimary();
            Assert.AreEqual(ScreenId.PreMatch, shell.Nav.Current, "Avançar opens the next match");
            shell.Simulate();
            int matches = 1;
            while (shell.Session.Career.State.Season.Year == year)
            {
                Assert.AreEqual(ScreenId.PostMatch, shell.Nav.Current);
                AssertTextsComplete(shell.PostMatch(), "post-match");
                int taps = 0;
                shell.ContinueAfterMatch(); taps++;
                AssertTextsComplete(shell.Hub(), "hub");
                shell.HubPrimary(); taps++;
                if (shell.Nav.Current == ScreenId.Hub)
                {
                    Assert.IsNull(shell.Session.Pending, "only the season end stops Avançar short of a match");
                    break;
                }
                Assert.AreEqual(ScreenId.PreMatch, shell.Nav.Current);
                AssertTextsComplete(shell.PreMatch(), "pre-match");
                shell.Simulate(); taps++;
                Assert.LessOrEqual(taps, 3);
                matches++;
            }
            TestContext.WriteLine($"season {year}: {matches} matches through the Shell");
            Assert.Greater(matches, 25, "a whole league season plus cup ties");
            Assert.AreEqual(1, shell.Session.Career.State.History.Count);
            var hub = shell.Hub();
            StringAssert.Contains(year.ToString(), hub.SeasonNote, "the hub sums up the season that ended");
        }

        [Test]
        public void APlayedMatch_ComesBackFromTheMatchScene_AsAPostMatchReport()
        {
            var store = NewStore();
            var shell = NewCareer(store);
            shell.HubPrimary();
            Assert.AreEqual(ShellEffect.LoadMatchScene, shell.Play());

            // The Match scene: the same setup, played (here by the AI on both sides), then the result goes back.
            var session = shell.Session;
            var setup = session.PlaySetup(session.Pending);
            Assert.AreEqual(session.Settings.MatchMinutes, setup.DurationMinutes);
            var match = new AiMatch(session.Db, setup, 1f / 50f);
            match.RunToEnd();
            var result = match.Result();
            var fixture = session.Pending.Fixture;
            session.FinishPlayedMatch(result);

            var back = new ShellPresenter(session, new ShellNavigator(), () => 1);
            back.Start(returningFromMatch: true);
            Assert.AreEqual(ScreenId.PostMatch, back.Nav.Current);
            var post = back.PostMatch();
            AssertTextsComplete(post, "post-match");
            Assert.AreEqual($"{result.HomeGoals} x {result.AwayGoals}", post.Score);
            Assert.AreEqual(session.Strings.Get("postmatch.played"), post.How);
            Assert.IsTrue(fixture.Played);
            Assert.AreEqual(result.HomeGoals, fixture.HomeGoals);
            back.ContinueAfterMatch();
            Assert.AreEqual(ScreenId.Hub, back.Nav.Current);
            StringAssert.Contains($"{result.HomeGoals} x {result.AwayGoals}", back.Hub().LastResult);
            Assert.IsTrue(SaveService.Load(store, CareerSession.Slot).Value.Season.AllFixtures().Single(f => f.Id == fixture.Id).Played,
                "the played result is saved");
        }

        [Test]
        public void TheMatchDuration_IsASettingKeptOutsideTheCareer()
        {
            var store = NewStore();
            var shell = NewCareer(store);
            var options = shell.Session.Db.Competitions.MatchRules.DurationOptions;
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 10 }, options, "D-22 (X-111): 3/4/5/6/10 min");
            Assert.AreEqual(4, shell.Session.Db.Competitions.MatchRules.DurationMinutes, "D-22 (X-111): 4 min by default");
            Assert.AreEqual(4, shell.Session.Settings.MatchMinutes, "no settings file yet: the default");
            shell.OpenSettings();
            Assert.AreEqual(ScreenId.Settings, shell.Nav.Current);
            var settings = shell.Settings();
            AssertTextsComplete(settings, "settings");
            CollectionAssert.AreEqual(options, settings.Durations.Select(d => d.Value), "every option on the screen");
            Assert.AreEqual(4, settings.Durations.Single(d => d.Selected).Value);
            shell.SetMatchMinutes(10);
            Assert.IsTrue(shell.Settings().Durations.Single(d => d.Selected).Value == 10);
            Assert.Throws<ArgumentOutOfRangeException>(() => shell.SetMatchMinutes(7));
            shell.Back();
            Assert.AreEqual(ScreenId.Hub, shell.Nav.Current);

            var again = Shell(store);
            Assert.AreEqual(10, again.Session.Settings.MatchMinutes, "kept across app runs");
            again.ContinueCareer();
            again.HubPrimary();
            Assert.AreEqual(10, again.Session.PlaySetup(again.Session.Pending).DurationMinutes);
            Assert.AreNotEqual(10, again.Session.Pending.Setup.DurationMinutes, "the career's own setup (QuickSim) is untouched");
            StringAssert.Contains("10", again.PreMatch().Play.Label);
        }

        [Test]
        public void TheDifficulty_IsChosenInTheNewCareer_ChangeableInSettings_AndSavedWithTheCareer()
        {
            var store = NewStore();
            var shell = Shell(store);
            shell.OpenSettings();
            var noCareer = shell.Settings();
            Assert.AreEqual(0, noCareer.Difficulties.Count, "no career: nothing to set (D-23c: it lives in the save)");
            Assert.IsNotEmpty(noCareer.DifficultyHint);
            shell.Back();

            shell.NewCareer();
            var choice = shell.ClubChoice();
            AssertTextsComplete(choice, "club choice");
            CollectionAssert.AreEqual(new[] { "Fácil", "Normal", "Difícil" }, choice.Difficulties.Select(d => d.Label), "D-23");
            Assert.AreEqual("normal", choice.Difficulties.Single(d => d.Selected).Id, "the default level");
            shell.SetDifficulty("easy");
            Assert.AreEqual(ScreenId.ClubChoice, shell.Nav.Current);
            Assert.AreEqual("easy", shell.ClubChoice().Difficulties.Single(d => d.Selected).Id);
            Assert.IsFalse(store.Exists(SaveSlotPaths.Body(CareerSession.Slot)), "nothing saved before the club is chosen");
            shell.ChooseClub(choice.Options[0].ClubId);
            Assert.AreEqual("easy", Shell(store).Session.Career.State.Difficulty, "saved with the new career");

            shell.OpenSettings();
            var settings = shell.Settings();
            AssertTextsComplete(settings, "settings");
            Assert.AreEqual("easy", settings.Difficulties.Single(d => d.Selected).Id);
            shell.SetDifficulty("hard");
            Assert.AreEqual(ScreenId.Settings, shell.Nav.Current);
            Assert.AreEqual("hard", shell.Settings().Difficulties.Single(d => d.Selected).Id);
            Assert.Throws<ArgumentException>(() => shell.SetDifficulty("nightmare"));

            var again = Shell(store);
            Assert.AreEqual("hard", again.Session.Career.State.Difficulty, "D-23c: gravada no save");
            again.ContinueCareer();
            again.HubPrimary();
            var play = again.Session.PlaySetup(again.Session.Pending);
            Assert.AreEqual("hard", play.Difficulty.LevelId, "the played match carries the level");
            Assert.AreEqual(again.Session.Pending.UserSide, play.Difficulty.UserSide);
        }

        /// <summary>F1a: a settings file written before D-22 (options 4/6/10, default 6) still opens. 6 and 10 stay options,
        /// so the user's choice is kept; a value that is no longer an option, or no file, means the new default (4 min).</summary>
        [TestCase(6, 6)]
        [TestCase(10, 10)]
        [TestCase(4, 4)]
        [TestCase(7, 4)]
        public void ASettingsFileFromBeforeD22_KeepsItsDuration_WhenItIsStillAnOption(int saved, int expected)
        {
            var store = NewStore();
            store.WriteAllBytes(AppSettings.File, System.Text.Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"matchMinutes\":{saved}}}"));
            Assert.AreEqual(expected, Shell(store).Session.Settings.MatchMinutes);
            Assert.AreEqual(4, Shell(NewStore()).Session.Settings.MatchMinutes, "no file: the default");
        }

        [Test]
        public void EveryCareerScreen_BuildsWithAllItsTexts_DuringASeason()
        {
            var shell = NewCareer(NewStore());
            void Visit()
            {
                foreach (var tab in (Tab[])Enum.GetValues(typeof(Tab)))
                {
                    shell.SelectTab(tab);
                    Assert.AreEqual(ShellNavigator.TabRoot(tab), shell.Nav.Current);
                    Assert.IsTrue(shell.Nav.ShowsTabBar);
                    AssertTextsComplete(CurrentModel(shell), tab.ToString());
                }
                foreach (var view in (CompetitionsView[])Enum.GetValues(typeof(CompetitionsView)))
                    for (int d = 0; d < shell.Session.Career.State.Season.Leagues.Count; d++)
                    {
                        shell.ShowCompetitions(view);
                        shell.ShowDivision(d);
                        var c = shell.Competitions();
                        AssertTextsComplete(c, $"competitions {view} {d}");
                        if (view == CompetitionsView.Table)
                        {
                            Assert.AreEqual(16, c.Table.Count);
                            Assert.AreEqual(d == shell.Session.ManagedClub.DivisionIndex ? 1 : 0, c.Table.Count(r => r.IsUser));
                        }
                    }
                shell.SelectTab(Tab.Club);
                shell.OpenCalendar();
                var cal = shell.Calendar();
                AssertTextsComplete(cal, "calendar");
                Assert.Greater(cal.Months.Sum(mo => mo.Rows.Count), 25);
                shell.Back();
                Assert.AreEqual(ScreenId.Hub, shell.Nav.Current);
            }
            Visit();
            for (int i = 0; i < 12; i++)
            {
                shell.HubPrimary();
                shell.Simulate();
                shell.ContinueAfterMatch();
            }
            Visit();
            var calendar = shell.Calendar();
            Assert.AreEqual(12, calendar.Months.SelectMany(mo => mo.Rows).Count(r => r.ResultSign.HasValue), "the twelve results");
            Assert.AreEqual(1, calendar.Months.SelectMany(mo => mo.Rows).Count(r => r.IsNext));
        }

        [Test]
        public void TheNavigator_ResetsOnTabs_PushesAndPops()
        {
            var nav = new ShellNavigator();
            int changes = 0;
            nav.Changed += () => changes++;
            nav.Reset(ScreenId.MainMenu);
            Assert.IsFalse(nav.ShowsTabBar);
            nav.Reset(ScreenId.Hub);
            nav.Push(ScreenId.Calendar);
            Assert.IsTrue(nav.CanGoBack);
            nav.Push(ScreenId.Settings);
            Assert.IsFalse(nav.ShowsTabBar, "a modal hides the bar");
            nav.SelectTab(Tab.Competitions);
            Assert.AreEqual(ScreenId.Competitions, nav.Current);
            Assert.AreEqual(1, nav.Depth);
            Assert.IsFalse(nav.Back(), "nothing under a tab root");
            nav.Push(ScreenId.PreMatch);
            nav.Replace(ScreenId.PostMatch);
            Assert.AreEqual(ScreenId.PostMatch, nav.Current);
            Assert.IsTrue(nav.Back());
            Assert.AreEqual(ScreenId.Competitions, nav.Current);
            Assert.AreEqual(8, changes, "a Back with nothing to pop changes nothing");
        }

        [TestCase(0L, "R$ 0")]
        [TestCase(950L, "R$ 950")]
        [TestCase(850_000L, "R$ 850 mil")]
        [TestCase(1_250_000L, "R$ 1,25 mi")]
        [TestCase(12_340_000L, "R$ 12,3 mi")]
        [TestCase(-2_000_000L, "−R$ 2 mi")]
        public void Money_IsShortWithADecimalComma(long value, string expected)
        {
            Assert.AreEqual(expected, Fmt.Money(Strings(), value));
        }
    }
}
