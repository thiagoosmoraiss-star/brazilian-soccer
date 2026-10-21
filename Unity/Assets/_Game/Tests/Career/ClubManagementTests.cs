using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Board;
using Game.Career.Season;
using Game.Career.World;
using Game.Rules.Board;
using NUnit.Framework;
using static Game.Tests.Career.CareerTestData;

namespace Game.Tests.Career
{
    /// <summary>F2a (X-122 to X-125): the managed club is administered by the user only; works and staff changes are commands
    /// that check every rule before changing anything; the AI keeps running every other club.</summary>
    public class ClubManagementTests
    {
        private static CareerSimulator NewCareer(ulong seed = 21)
        {
            var db = Db();
            var career = CareerSimulator.Start(db, seed, new Game.Simulation.QuickSim.QuickSim(db).Simulate,
                new Game.Career.Market.TransferWindow(), new Game.Career.Economy.EconomySystem());
            int lowest = career.State.World.Clubs.Max(c => c.DivisionIndex);
            career.State.ManagedClubId = career.State.World.Clubs.Where(c => c.DivisionIndex == lowest).OrderBy(c => c.Id).First().Id;
            return career;
        }

        private static Club Managed(CareerSimulator career) => career.State.Club(career.State.ManagedClubId.Value);

        /// <summary>Plays the managed club's matches by the QuickSim until the season changes.</summary>
        private static void PlaySeason(CareerSimulator career, Action<CareerSimulator> beforeEachMatch = null)
        {
            int year = career.State.Season.Year;
            while (career.State.Season.Year == year)
            {
                if (career.PendingUserMatch == null) { career.AdvanceToUserMatch(); continue; }
                beforeEachMatch?.Invoke(career);
                career.SimulateUserMatch();
            }
        }

        /// <summary>The cash is always the archived seasons' nets plus this season's ledger (X-44).</summary>
        private static void AssertLedgerInvariant(CareerSimulator career, string context)
        {
            foreach (var club in career.State.World.Clubs)
            {
                long archived = career.State.History.Sum(h => h.SeasonNetByClub.TryGetValue(club.Id, out var n) ? n : 0);
                long season = career.State.Season.Ledger.Where(e => e.ClubId == club.Id).Sum(e => e.Amount);
                Assert.AreEqual(archived + season, club.Balance, $"{context}: club {club.Id}");
            }
        }

        private static (long Balance, int Ledger, int Projects, int Staff, long LastId, int Stadium, int Training, string StaffLevels) Snapshot(CareerSimulator career)
        {
            var club = Managed(career);
            var w = career.State.World;
            return (club.Balance, career.State.Season.Ledger.Count, w.FacilityProjects.Count, w.StaffMembers.Count, career.State.Ids.LastIssued,
                club.Stadium.Level, club.TrainingCenterLevel,
                string.Join(",", w.StaffMembers.Where(s => s.ClubId == club.Id).OrderBy(s => s.Role).Select(s => s.Id.Value + ":" + s.Level)));
        }

        // ---------------- no automatic administration of the managed club ----------------

        [Test]
        public void AManagedSeason_WithoutCommands_StartsNoWorkThere_AndKeepsItsStaff_WhileTheAiRunsTheOthers()
        {
            var career = NewCareer();
            var club = Managed(career);
            int stadium = club.Stadium.Level, training = club.TrainingCenterLevel;
            var staff = career.State.World.StaffMembers.Where(s => s.ClubId == club.Id).Select(s => (s.Id, s.Role, s.Level)).OrderBy(s => s.Role).ToList();
            var aiWorks = new HashSet<long>();
            int aiStaffChanges = 0;
            var before = career.State.World.StaffMembers.Where(s => s.ClubId != club.Id).ToDictionary(s => s.Id, s => s.Level);
            PlaySeason(career, c =>
            {
                Assert.IsNull(c.State.World.ActiveProjectOf(club.Id, FacilityType.Stadium), "X-122: no automatic work on the managed club");
                Assert.IsNull(c.State.World.ActiveProjectOf(club.Id, FacilityType.TrainingCenter));
                foreach (var p in c.State.World.FacilityProjects) aiWorks.Add(p.Id.Value);
            });
            foreach (var s in career.State.World.StaffMembers.Where(s => s.ClubId != club.Id))
                if (!before.TryGetValue(s.Id, out int level) || level != s.Level) aiStaffChanges++;

            Assert.AreEqual(stadium, club.Stadium.Level, "seed 21");
            Assert.AreEqual(training, club.TrainingCenterLevel, "seed 21");
            CollectionAssert.AreEqual(staff, career.State.World.StaffMembers.Where(s => s.ClubId == club.Id).Select(s => (s.Id, s.Role, s.Level)).OrderBy(s => s.Role).ToList(),
                "X-122: the managed club's staff is never hired, promoted or replaced automatically");
            Assert.Greater(aiWorks.Count + aiStaffChanges, 0, "the AI still administers the other clubs");
            AssertLedgerInvariant(career, "seed 21");
        }

        [Test]
        public void TheManagedClub_StillPaysItsStaff_AndItsWorkCompletesOnItsDate()
        {
            var career = NewCareer(22);
            career.AdvanceToUserMatch();
            var club = Managed(career);
            LedgerBook.Post(career.State, club.Id, career.Today, LedgerCategory.Prizes, 50_000_000); // cash through the ledger
            int stadium = club.Stadium.Level;
            Assert.IsTrue(career.StartFacilityUpgrade(FacilityType.Stadium).IsSuccess);
            var work = career.State.World.ActiveProjectOf(club.Id, FacilityType.Stadium);
            int wagesBefore = career.State.Season.Ledger.Count(e => e.ClubId == club.Id && e.Category == LedgerCategory.StaffWages);
            while (career.Today <= work.CompletesOn.AddMonths(1) && career.State.Season.Year == work.CompletesOn.Year)
            {
                if (career.PendingUserMatch == null) { career.AdvanceToUserMatch(); continue; }
                career.SimulateUserMatch();
            }
            Assert.AreEqual(stadium + 1, club.Stadium.Level, "the started work completes like every club's");
            Assert.AreEqual(Db().World.Generation.StadiumCapacityByLevel[stadium], club.Stadium.Capacity);
            Assert.IsNull(career.State.World.ActiveProjectOf(club.Id, FacilityType.Stadium));
            Assert.Greater(career.State.Season.Ledger.Count(e => e.ClubId == club.Id && e.Category == LedgerCategory.StaffWages), wagesBefore,
                "the staff wages are still paid every month");
            AssertLedgerInvariant(career, "seed 22");
        }

        // ---------------- facilities (D-32b) ----------------

        [Test]
        public void AFacilityUpgrade_IsOneLevel_PaidInFullNow_FromTheCurrentCash()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            long cost = BoardRules.StadiumUpgradeCost(db.Facilities, club.Stadium.Level);
            club.Balance = cost; // exactly the cost: the Budget (month-end copy) is not what is checked (X-124)
            club.Budget = 0;
            var today = career.Today;
            var r = career.StartFacilityUpgrade(FacilityType.Stadium);
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(0, club.Balance);
            var entry = career.State.Season.Ledger.Last();
            Assert.AreEqual((club.Id, LedgerCategory.FacilityInvestment, -cost, today), (entry.ClubId, entry.Category, entry.Amount, entry.Date));
            var work = career.State.World.ActiveProjectOf(club.Id, FacilityType.Stadium);
            Assert.AreEqual(club.Stadium.Level + 1, work.TargetLevel, "one level per command");
            Assert.AreEqual(today.AddMonths(db.Facilities.Stadium.UpgradeDurationMonths), work.CompletesOn);
        }

        [Test]
        public void TheStadiumAndTheTrainingCentre_CanBothBeInWorks_ButEachOnlyOnce()
        {
            var career = NewCareer();
            var club = Managed(career);
            club.Balance = 100_000_000;
            Assert.IsTrue(career.StartFacilityUpgrade(FacilityType.Stadium).IsSuccess);
            Assert.IsTrue(career.StartFacilityUpgrade(FacilityType.TrainingCenter).IsSuccess, "X-125: works at the same time on different facilities");
            var snapshot = Snapshot(career);
            var r = career.StartFacilityUpgrade(FacilityType.Stadium);
            Assert.AreEqual(ClubManagement.WorkInProgress, r.Errors[0].Code);
            Assert.AreEqual(ClubManagement.WorkInProgress, career.StartFacilityUpgrade(FacilityType.TrainingCenter).Errors[0].Code);
            Assert.AreEqual(snapshot, Snapshot(career), "a refused command changes nothing");
        }

        [Test]
        public void RefusedUpgrades_ChangeNothing()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            club.Balance = BoardRules.TrainingCenterUpgradeCost(db.Facilities, club.TrainingCenterLevel) - 1;
            club.Budget = 1_000_000_000;
            var snapshot = Snapshot(career);
            Assert.AreEqual(ClubManagement.NoCash, career.StartFacilityUpgrade(FacilityType.TrainingCenter).Errors[0].Code, "the current cash, not the Budget");
            Assert.AreEqual(snapshot, Snapshot(career));

            club.Balance = 1_000_000_000;
            club.Stadium.Level = ClubManagement.FacilityMaxLevel(db, FacilityType.Stadium);
            club.TrainingCenterLevel = ClubManagement.FacilityMaxLevel(db, FacilityType.TrainingCenter);
            snapshot = Snapshot(career);
            Assert.AreEqual(ClubManagement.MaxLevel, career.StartFacilityUpgrade(FacilityType.Stadium).Errors[0].Code);
            Assert.AreEqual(ClubManagement.MaxLevel, career.StartFacilityUpgrade(FacilityType.TrainingCenter).Errors[0].Code);
            Assert.AreEqual(snapshot, Snapshot(career));
            Assert.AreEqual(10, ClubManagement.FacilityMaxLevel(db, FacilityType.Stadium), "facilities.json: 9 upgrade costs");
            Assert.AreEqual(5, ClubManagement.FacilityMaxLevel(db, FacilityType.TrainingCenter), "facilities.json: 4 upgrade costs");

            career.State.ManagedClubId = null;
            Assert.AreEqual(ClubManagement.NotManaged, career.StartFacilityUpgrade(FacilityType.Stadium).Errors[0].Code);
        }

        [Test]
        public void AStadiumBelowTheDivisionMinimum_IsOnlyAWarning()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            int minimum = BoardRules.StadiumMinimumLevel(db.Facilities, 0);
            club.Stadium.Level = Math.Max(1, minimum - 1);
            Assert.IsTrue(ClubManagement.StadiumBelowMinimum(db, club, 0));
            club.Balance = 1_000_000_000;
            Assert.IsTrue(career.StartFacilityUpgrade(FacilityType.TrainingCenter).IsSuccess, "D-32b (X-123): nothing is blocked");
        }

        // ---------------- staff (D-32c) ----------------

        [Test]
        public void ReplacingStaffUpwards_PaysTheSumOfThePromotionsInBetween_AndTheRoleIsNeverEmpty()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            var member = career.State.World.StaffOf(club.Id, StaffRole.Assistant);
            member.Level = 1;
            long fee = 0;
            for (int l = 1; l < 4; l++) fee += BoardRules.StaffUpgradeCost(db.Staff, l);
            Assert.AreEqual(fee, ClubManagement.StaffReplacementFee(db, 1, 4));
            club.Balance = fee;
            club.Budget = 0;
            long lastId = career.State.Ids.LastIssued;
            var r = career.ReplaceStaff(StaffRole.Assistant, 4);
            Assert.IsTrue(r.IsSuccess, r.ToString());
            Assert.AreEqual(0, club.Balance);
            var entry = career.State.Season.Ledger.Last();
            Assert.AreEqual((LedgerCategory.StaffWages, -fee), (entry.Category, entry.Amount), "X-125: the existing StaffWages category");
            var assistants = career.State.World.StaffMembers.Where(s => s.ClubId == club.Id && s.Role == StaffRole.Assistant).ToList();
            Assert.AreEqual(1, assistants.Count, "X-122 (2): the replacement arrives in the same operation");
            Assert.AreEqual(4, assistants[0].Level);
            Assert.AreNotEqual(member.Id, assistants[0].Id, "a new professional");
            Assert.AreEqual(lastId + 2, career.State.Ids.LastIssued, "one Id for the ledger entry, one for the new professional");
            Assert.AreEqual(3, career.State.World.StaffMembers.Count(s => s.ClubId == club.Id));
        }

        [Test]
        public void ReplacingStaffDownwards_IsFree_AndTheWageIsTheNewLevels()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            career.State.World.StaffOf(club.Id, StaffRole.Physio).Level = 4;
            club.Balance = 0;
            int ledger = career.State.Season.Ledger.Count;
            Assert.IsTrue(career.ReplaceStaff(StaffRole.Physio, 2).IsSuccess, "no fee, so no cash needed");
            Assert.AreEqual(ledger, career.State.Season.Ledger.Count, "nothing paid");
            Assert.AreEqual(0, club.Balance);
            Assert.AreEqual(2, career.State.World.StaffOf(club.Id, StaffRole.Physio).Level);

            // The next month end pays the level-2 wage.
            int paid = career.State.Season.Ledger.Count(e => e.ClubId == club.Id && e.Category == LedgerCategory.StaffWages);
            while (career.State.Season.Ledger.Count(e => e.ClubId == club.Id && e.Category == LedgerCategory.StaffWages) == paid)
            {
                if (career.PendingUserMatch == null) { career.AdvanceToUserMatch(); continue; }
                career.SimulateUserMatch();
            }
            var monthEnd = career.State.Season.Ledger.Where(e => e.ClubId == club.Id && e.Category == LedgerCategory.StaffWages).Skip(paid).Select(e => e.Amount).ToList();
            Assert.Contains(-(BoardRules.StaffWage(db.Staff, 2) / 12), monthEnd);
        }

        [Test]
        public void RefusedStaffReplacements_ChangeNothing()
        {
            var career = NewCareer();
            var club = Managed(career);
            var db = Db();
            var member = career.State.World.StaffOf(club.Id, StaffRole.Scout);
            member.Level = 2;
            club.Balance = ClubManagement.StaffReplacementFee(db, 2, 3) - 1;
            club.Budget = 1_000_000_000;
            var snapshot = Snapshot(career);
            Assert.AreEqual(ClubManagement.NoCash, career.ReplaceStaff(StaffRole.Scout, 3).Errors[0].Code, "the current cash, not the Budget");
            Assert.AreEqual(ClubManagement.InvalidLevel, career.ReplaceStaff(StaffRole.Scout, 2).Errors[0].Code, "the same level");
            Assert.AreEqual(ClubManagement.InvalidLevel, career.ReplaceStaff(StaffRole.Scout, 0).Errors[0].Code);
            Assert.AreEqual(ClubManagement.InvalidLevel, career.ReplaceStaff(StaffRole.Scout, ClubManagement.StaffMaxLevel(db) + 1).Errors[0].Code);
            Assert.AreEqual(ClubManagement.InvalidLevel, career.ReplaceStaff((StaffRole)7, 3).Errors[0].Code);
            Assert.AreEqual(snapshot, Snapshot(career), "a refused command changes nothing");
        }

        // ---------------- undo when the save fails ----------------

        [Test]
        public void ACheckpoint_UndoesEachCommand_CashLedgerIdsWorksAndTheSameStaffMember()
        {
            var career = NewCareer(24);
            var club = Managed(career);
            var db = Db();
            LedgerBook.Post(career.State, club.Id, career.Today, LedgerCategory.Prizes, 100_000_000);
            var staffBefore = career.State.World.StaffMembers.ToList();
            var snapshot = Snapshot(career);

            foreach (var type in new[] { FacilityType.Stadium, FacilityType.TrainingCenter })
            {
                var checkpoint = ClubManagement.Checkpoint.Take(career.State);
                Assert.IsTrue(career.StartFacilityUpgrade(type).IsSuccess, $"seed 24: {type}");
                Assert.AreNotEqual(snapshot, Snapshot(career));
                checkpoint.Restore(career.State);
                Assert.AreEqual(snapshot, Snapshot(career), $"seed 24: {type} undone");
                Assert.IsNull(career.State.World.ActiveProjectOf(club.Id, type));
            }

            foreach (var role in new[] { StaffRole.Physio, StaffRole.Assistant, StaffRole.Scout })
            {
                int current = career.State.World.StaffOf(club.Id, role).Level;
                int target = current < ClubManagement.StaffMaxLevel(db) ? ClubManagement.StaffMaxLevel(db) : 1;
                var checkpoint = ClubManagement.Checkpoint.Take(career.State);
                Assert.IsTrue(career.ReplaceStaff(role, target).IsSuccess, $"seed 24: {role}");
                checkpoint.Restore(career.State);
                Assert.AreEqual(snapshot, Snapshot(career), $"seed 24: {role} undone");
                CollectionAssert.AreEqual(staffBefore, career.State.World.StaffMembers, "the same members, in the same order");
                Assert.AreEqual(1, career.State.World.StaffMembers.Count(s => s.ClubId == club.Id && s.Role == role));
            }

            // The Id counter is back: the next Id issued is the one the undone command had used.
            int last = career.State.Ids.LastIssued;
            Assert.IsTrue(career.StartFacilityUpgrade(FacilityType.Stadium).IsSuccess);
            Assert.AreEqual(last + 2, career.State.Ids.LastIssued, "one ledger entry and one work");
            AssertLedgerInvariant(career, "seed 24");
        }

        // ---------------- a longer managed career ----------------

        [Test]
        public void ThreeManagedSeasons_WithCommands_KeepEveryInvariant()
        {
            var career = NewCareer(23);
            var roles = new[] { StaffRole.Physio, StaffRole.Assistant, StaffRole.Scout };
            int step = 0;
            for (int season = 0; season < 3; season++)
            {
                PlaySeason(career, c =>
                {
                    step++;
                    if (step % 7 == 0) c.StartFacilityUpgrade(step % 14 == 0 ? FacilityType.Stadium : FacilityType.TrainingCenter);
                    if (step % 5 == 0) c.ReplaceStaff(roles[step % 3], 1 + step % ClubManagement.StaffMaxLevel(Db()));
                    AssertLedgerInvariant(c, $"seed 23, step {step}");
                });
                var club = Managed(career);
                foreach (var role in roles)
                    Assert.AreEqual(1, career.State.World.StaffMembers.Count(s => s.ClubId == club.Id && s.Role == role), $"seed 23, season {season}: {role}");
            }
            AssertLedgerInvariant(career, "seed 23, end");
        }
    }
}
