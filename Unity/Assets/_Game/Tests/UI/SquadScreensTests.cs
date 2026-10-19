using System.Linq;
using Game.Career.Season;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.UI.Presenters;
using NUnit.Framework;
using static Game.Tests.UI.UiTestSupport;

namespace Game.Tests.UI
{
    /// <summary>C3b (X-97): squad list, player profile, lineup and tactic, driven through the screens' commands.</summary>
    public class SquadScreensTests
    {
        private static ShellPresenter CareerAtAMatch(Game.Save.IO.IFileStore store = null)
        {
            var shell = NewCareer(store ?? NewStore());
            shell.HubPrimary();
            Assert.AreEqual(ScreenId.PreMatch, shell.Nav.Current);
            return shell;
        }

        private static MatchTeamSetup UserTeamOfPending(ShellPresenter shell)
        {
            var pending = shell.Session.Pending;
            return shell.Session.PlaySetup(pending).Team(pending.UserSide);
        }

        [Test]
        public void TheSquadTab_ListsEveryPlayerBySection_AndEachProfileOpens()
        {
            var shell = CareerAtAMatch();
            shell.SelectTab(Tab.Squad);
            Assert.AreEqual(ScreenId.Squad, shell.Nav.Current);
            var squad = shell.Squad();
            AssertTextsComplete(squad, "squad");
            var players = squad.Rows.Where(r => r.Section == null).ToList();
            Assert.AreEqual(shell.Session.Career.State.World.SquadOf(shell.Session.ManagedClub.Id).Count, players.Count);
            Assert.AreEqual(4, squad.Rows.Count(r => r.Section != null), "keepers, defenders, midfielders, forwards");
            Assert.AreEqual(11, players.Count(r => r.Status == shell.Session.Strings.Get("squad.starter")));
            foreach (var row in players)
            {
                shell.OpenPlayer(row.PlayerId);
                Assert.AreEqual(ScreenId.Player, shell.Nav.Current);
                Assert.IsTrue(shell.Nav.ShowsTabBar);
                var profile = shell.PlayerProfile();
                AssertTextsComplete(profile, "player " + row.PlayerId);
                Assert.AreEqual(row.Name, profile.Name);
                Assert.GreaterOrEqual(profile.Groups.Count, 6, "data, three attribute groups, condition, season, contract");
                bool keeper = row.Pos == "GOL";
                Assert.AreEqual(keeper ? 18 : 14, profile.Groups.Sum(g => g.Rows.Count(r => r.Bar >= 0)),
                    "keepers show the 18 attributes; outfield players the 14 that are not the goalkeeper's");
                shell.Back();
            }
            Assert.AreEqual(ScreenId.Squad, shell.Nav.Current);
        }

        [Test]
        public void TappingAReserveThenAStarter_PutsHimInTheLineup_ForThePendingMatch_AndIsSaved()
        {
            var store = NewStore();
            var shell = CareerAtAMatch(store);
            shell.OpenLineup();
            Assert.AreEqual(ScreenId.Lineup, shell.Nav.Current);
            var lineup = shell.Lineup();
            AssertTextsComplete(lineup, "lineup");
            Assert.IsFalse(lineup.Automatic.Enabled, "already automatic");
            Assert.AreEqual(11, lineup.Starters.Count);
            Assert.AreEqual(shell.Session.Db.Competitions.MatchRules.BenchSize, lineup.Bench.Count);

            var reserveName = lineup.Reserves[0].Name;
            var replacedName = lineup.Starters[10].Name;
            shell.LineupTap(LineupArea.Reserve, 0);
            Assert.IsTrue(shell.Lineup().Reserves[0].Selected);
            shell.LineupTap(LineupArea.Starter, 10);
            lineup = shell.Lineup();
            Assert.AreEqual(reserveName, lineup.Starters[10].Name);
            Assert.IsTrue(lineup.Reserves.Any(r => r.Name == replacedName), "the replaced starter goes out");
            Assert.IsTrue(lineup.Automatic.Enabled);

            var team = UserTeamOfPending(shell);
            var reserveId = shell.Session.Career.State.UserPlan.Starters[10];
            Assert.AreEqual(reserveId, team.Starters[10].PlayerId, "the pending match plays the user's lineup");

            shell.Back();
            Assert.AreEqual(ScreenId.PreMatch, shell.Nav.Current);
            StringAssert.Contains(reserveName, string.Join(",", shell.PreMatch().Lineup.Select(r => r.Name)));
            Assert.AreEqual(shell.Session.Strings.Get("prematch.lineup_manual"), shell.PreMatch().LineupNote);

            var reopened = Open(store);
            CollectionAssert.AreEqual(shell.Session.Career.State.UserPlan.Starters, reopened.Career.State.UserPlan.Starters, "saved");
        }

        [Test]
        public void StartersSwapPlaces_BenchTakesAReserve_AndAStartersPlaceIsNeverLeftEmpty()
        {
            var shell = CareerAtAMatch();
            shell.OpenLineup();
            var before = shell.Lineup();
            shell.LineupTap(LineupArea.Starter, 1);
            shell.LineupTap(LineupArea.Starter, 2);
            var after = shell.Lineup();
            Assert.AreEqual(before.Starters[1].Name, after.Starters[2].Name);
            Assert.AreEqual(before.Starters[2].Name, after.Starters[1].Name);

            var benchName = after.Bench[0].Name;
            shell.LineupTap(LineupArea.Bench, 0);
            shell.LineupTap(LineupArea.Reserve, 0);
            after = shell.Lineup();
            Assert.AreNotEqual(benchName, after.Bench[0].Name);
            Assert.IsTrue(after.Reserves.Any(r => r.Name == benchName));

            // Bench → empty a slot by moving a bench player to a starter's place, then try to move a starter into the gap.
            int benchSize = after.Bench.Count;
            var plan = shell.Session.Career.State.UserPlan;
            shell.Session.SetLineup(plan.Starters, plan.Bench.Take(benchSize - 1).ToList());
            shell.Nav.Replace(ScreenId.Lineup);
            after = shell.Lineup();
            Assert.IsTrue(after.Bench[benchSize - 1].Empty);
            var startersBefore = after.Starters.Select(s => s.Name).ToList();
            shell.LineupTap(LineupArea.Starter, 0);
            shell.LineupTap(LineupArea.Bench, benchSize - 1);
            after = shell.Lineup();
            CollectionAssert.AreEqual(startersBefore, after.Starters.Select(s => s.Name), "no empty starter");
            Assert.IsTrue(after.Bench[benchSize - 1].Selected, "the tap just selects");

            shell.AutomaticLineup();
            Assert.IsFalse(shell.Session.Career.State.UserPlan.ManualLineup);
            Assert.IsFalse(shell.Lineup().Automatic.Enabled);
        }

        [Test]
        public void TheTacticControls_ChangeThePendingMatchTactic_WithinTheirLevels()
        {
            var shell = CareerAtAMatch();
            shell.OpenLineup();
            var formations = shell.Session.Db.Formations.Select(f => f.Id).ToList();
            string first = shell.Session.Career.UserTactic.FormationId;
            for (int i = 0; i < formations.Count; i++) shell.TacticStep(TacticControl.Formation, 1);
            Assert.AreEqual(first, shell.Session.Career.UserTactic.FormationId, "the formation cycles");
            shell.TacticStep(TacticControl.Formation, 1);
            Assert.AreNotEqual(first, UserTeamOfPending(shell).Tactic.FormationId);

            for (int i = 0; i < 10; i++) shell.TacticStep(TacticControl.Mentality, 1);
            Assert.AreEqual(Game.Data.Match.TacticModifiers.MentalityLevels, UserTeamOfPending(shell).Tactic.Mentality);
            Assert.IsFalse(shell.Lineup().Tactic[1].CanNext);
            for (int i = 0; i < 10; i++) shell.TacticStep(TacticControl.Pressure, -1);
            Assert.AreEqual(1, UserTeamOfPending(shell).Tactic.Pressure);
            shell.TacticStep(TacticControl.DefensiveLine, 1);
            Assert.AreEqual(shell.Session.Strings.Get("tactic.line_3"), shell.Lineup().Tactic[2].Value);
            AssertTextsComplete(shell.Lineup(), "lineup after tactic changes");
        }

        [Test]
        public void AnInjuredPlayerInTheUsersLineup_IsFlagged_AndReplacedInTheMatch()
        {
            var shell = CareerAtAMatch();
            shell.OpenLineup();
            shell.LineupTap(LineupArea.Starter, 0);
            shell.LineupTap(LineupArea.Starter, 0); // select/deselect: still automatic
            shell.LineupTap(LineupArea.Starter, 3);
            shell.LineupTap(LineupArea.Starter, 4); // swap → the user's lineup
            var plan = shell.Session.Career.State.UserPlan;
            var injured = shell.Session.Career.State.World.Players.First(p => p.Id == plan.Starters[5]);
            injured.Condition.InjuryMatchesLeft = 3;
            shell.Session.SetLineup(plan.Starters, plan.Bench); // rebuild the pending match
            shell.Nav.Replace(ScreenId.Lineup);
            var row = shell.Lineup().Starters[5];
            Assert.AreEqual(injured.Name, row.Name);
            StringAssert.Contains("3", row.Warning);
            Assert.IsFalse(UserTeamOfPending(shell).Starters.Any(p => p.PlayerId == injured.Id));
            shell.SelectTab(Tab.Squad);
            Assert.IsTrue(shell.Squad().Rows.Single(r => r.PlayerId == injured.Id.Value).StatusBad);
        }
    }
}
