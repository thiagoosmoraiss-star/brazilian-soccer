using System.Linq;
using Game.Career.World;
using Game.Rules.Board;
using Game.Save;
using Game.UI.Presenters;
using NUnit.Framework;
using static Game.Tests.UI.UiTestSupport;

namespace Game.Tests.UI
{
    /// <summary>F2a: the Diretoria, Clube and Finanças screens; the user's commands run only after a confirmation and are saved.</summary>
    public class ClubScreensTests
    {
        [Test]
        public void TheBoardTab_ShowsTheObjectiveTheConfidenceAndTheSponsorship_ReadOnly()
        {
            var shell = NewCareer(NewStore());
            shell.SelectTab(Tab.Board);
            Assert.AreEqual(ScreenId.Board, shell.Nav.Current, "the placeholder is gone");
            Assert.IsTrue(shell.Nav.ShowsTabBar);
            var m = shell.Board();
            AssertTextsComplete(m, "board");
            var club = shell.Session.ManagedClub;
            var db = shell.Session.Db;
            Assert.IsNotEmpty(m.Objective);
            Assert.AreEqual(shell.Session.Career.State.World.ManagerOf(club.Id).Confidence, m.ConfidenceValue);
            StringAssert.Contains(db.Board.Confidence.DismissalThreshold.ToString(), m.ConfidenceHint, "evaluated at the season end");
            StringAssert.Contains(Fmt.Money(shell.Session.Strings, db.Economy.Sponsorship.PerSeasonByDivision[club.DivisionIndex]), m.Sponsorship,
                "D-32a: the division's fixed sponsorship");
            shell.OpenClub();
            Assert.AreEqual(ScreenId.Club, shell.Nav.Current);
            shell.Back();
            shell.OpenFinances();
            Assert.AreEqual(ScreenId.Finances, shell.Nav.Current);
        }

        [Test]
        public void TheClubScreen_ListsBothFacilitiesAndTheThreeRoles_AndTheHubOpensIt()
        {
            var shell = NewCareer(NewStore());
            var hub = shell.Hub();
            Assert.IsNotNull(hub.Club);
            Assert.IsNotNull(hub.Finances);
            shell.OpenClub();
            var m = shell.Club();
            AssertTextsComplete(m, "club");
            Assert.AreEqual(2, m.Facilities.Count);
            Assert.AreEqual(3, m.Staff.Count);
            int max = shell.Session.Db.Staff.WagePerLevel.Count;
            foreach (var s in m.Staff)
            {
                Assert.AreEqual(max, s.Levels.Count);
                Assert.AreEqual(1, s.Levels.Count(l => l.Current));
                Assert.IsFalse(s.Levels.Single(l => l.Current).Enabled, "the current level is not an option");
            }
            Assert.IsNull(m.Confirm);
        }

        [Test]
        public void AFacilityUpgrade_WaitsForTheConfirmation_ThenRunsAndIsSaved()
        {
            var store = NewStore();
            var shell = NewCareer(store);
            var club = shell.Session.ManagedClub;
            // Cash through the ledger, so the save (balance = ledger sum) stays valid.
            Game.Career.Season.LedgerBook.Post(shell.Session.Career.State, club.Id, shell.Session.Career.Today, LedgerCategory.Prizes, 100_000_000);
            long before = club.Balance;
            shell.OpenClub();
            shell.RequestFacilityUpgrade((int)FacilityType.Stadium);
            var m = shell.Club();
            Assert.IsNotNull(m.Confirm, "a command that spends money is confirmed first");
            AssertTextsComplete(m, "club confirm");
            Assert.AreEqual(before, club.Balance, "nothing happens before the confirmation");
            shell.CancelClubAction();
            Assert.IsNull(shell.Club().Confirm);
            Assert.AreEqual(before, club.Balance);

            shell.RequestFacilityUpgrade((int)FacilityType.Stadium);
            shell.ConfirmClubAction();
            m = shell.Club();
            Assert.IsFalse(m.MessageIsError, m.Message);
            long cost = BoardRules.StadiumUpgradeCost(shell.Session.Db.Facilities, club.Stadium.Level);
            Assert.AreEqual(before - cost, club.Balance);
            var stadium = m.Facilities.Single(f => f.Type == (int)FacilityType.Stadium);
            Assert.IsNotNull(stadium.Work);
            Assert.IsFalse(stadium.Upgrade.Enabled, "one work at a time per facility");
            Assert.IsNotNull(stadium.Reason);
            Assert.IsTrue(m.Facilities.Single(f => f.Type == (int)FacilityType.TrainingCenter).Upgrade.Enabled, "the other facility can start too");

            var saved = SaveService.Load(store, CareerSession.Slot).Value;
            Assert.IsNotNull(saved.World.ActiveProjectOf(club.Id, FacilityType.Stadium), "saved as soon as it runs");
        }

        [Test]
        public void WithoutCash_TheUpgradesAndTheDearerStaffAreDisabled_ButADownwardReplacementIsNot()
        {
            var store = NewStore();
            var shell = NewCareer(store);
            var club = shell.Session.ManagedClub;
            club.Balance = 0;
            var physio = shell.Session.Career.State.World.StaffOf(club.Id, StaffRole.Physio);
            physio.Level = 3;
            shell.OpenClub();
            var m = shell.Club();
            foreach (var f in m.Facilities.Where(f => f.Upgrade != null))
            {
                Assert.IsFalse(f.Upgrade.Enabled);
                Assert.IsNotNull(f.Reason);
            }
            var card = m.Staff.Single(s => s.Role == (int)StaffRole.Physio);
            Assert.IsTrue(card.Levels.Where(l => l.Level > 3).All(l => !l.Enabled), "the fee is checked against the cash");
            Assert.IsTrue(card.Levels.Where(l => l.Level < 3).All(l => l.Enabled), "D-32c: downwards is free");

            // A refused command (here: forced through) shows why and changes nothing.
            shell.RequestFacilityUpgrade((int)FacilityType.TrainingCenter);
            shell.ConfirmClubAction();
            m = shell.Club();
            Assert.IsTrue(m.MessageIsError);
            Assert.AreEqual(0, club.Balance);

            shell.RequestStaffReplacement((int)StaffRole.Physio, 1);
            shell.ConfirmClubAction();
            Assert.IsFalse(shell.Club().MessageIsError);
            Assert.AreEqual(1, shell.Session.Career.State.World.StaffOf(club.Id, StaffRole.Physio).Level);
            Assert.AreEqual(1, SaveService.Load(store, CareerSession.Slot).Value.World.StaffOf(club.Id, StaffRole.Physio).Level, "saved");
        }

        [Test]
        public void TheFinances_ShowTheCashTheSeasonByCategoryAndTwelveMonths()
        {
            var shell = NewCareer(NewStore());
            shell.HubPrimary();
            shell.Simulate();
            shell.ContinueAfterMatch();
            shell.OpenFinances();
            var m = shell.Finances();
            AssertTextsComplete(m, "finances");
            Assert.AreEqual(12, m.Months.Count);
            var club = shell.Session.ManagedClub;
            StringAssert.Contains(Fmt.Money(shell.Session.Strings, club.Balance), m.Cash);
            var entries = shell.Session.Career.State.Season.Ledger.Where(e => e.ClubId == club.Id).ToList();
            Assert.AreEqual(entries.Select(e => e.Category).Distinct().Count(), m.Categories.Count, "one row per category used this season");
            Assert.IsTrue(m.Months.Any(x => !x.Future));
        }
    }
}
