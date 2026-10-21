using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Career.Board;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Save;
using Game.Save.IO;
using Game.Save.Serialization;
using Game.UI.Presenters;
using NUnit.Framework;
using static Game.Tests.UI.UiTestSupport;

namespace Game.Tests.UI
{
    /// <summary>F2a: an administrative command whose save fails (refused or a file error) is undone before the screen says so:
    /// the career in memory, the files on disk and every later save stay as the last save was.</summary>
    public class ClubSaveFailureTests
    {
        public enum Failure { None, Refused, WriteThrows, BodyMoveThrows, MetaMoveThrows, Defect }

        /// <summary>The real store, with one kind of failure switched on for the career save.</summary>
        private sealed class FailingStore : IFileStore
        {
            private readonly IFileStore _inner;
            public Failure Mode;
            public FailingStore(IFileStore inner) { _inner = inner; }

            private static readonly string BodyTmp = SaveSlotPaths.Body(CareerSession.Slot) + ".tmp";
            private static readonly string MetaTmp = SaveSlotPaths.Meta(CareerSession.Slot) + ".tmp";

            public bool Exists(string path) => _inner.Exists(path);
            public byte[] ReadAllBytes(string path) => _inner.ReadAllBytes(path);
            public void Delete(string path) => _inner.Delete(path);
            public void Copy(string from, string to) => _inner.Copy(from, to);
            public IReadOnlyList<string> ListFiles(string dir) => _inner.ListFiles(dir);

            public void WriteAllBytes(string path, byte[] bytes)
            {
                if (path == BodyTmp && Mode == Failure.WriteThrows) throw new IOException("disk full (test)");
                if (path == BodyTmp && Mode == Failure.Defect) throw new InvalidOperationException("defect (test)");
                // A body that does not decompress: the save's own check refuses it (Result.Fail).
                _inner.WriteAllBytes(path, path == BodyTmp && Mode == Failure.Refused ? new byte[] { 1, 2, 3 } : bytes);
            }

            public void Move(string from, string to)
            {
                if (from == BodyTmp && Mode == Failure.BodyMoveThrows)
                {
                    _inner.Delete(to); // the worst case: the old body already deleted when the move fails
                    throw new IOException("move failed (test)");
                }
                if (from == MetaTmp && Mode == Failure.MetaMoveThrows) throw new IOException("move failed (test)"); // new body, old meta
                _inner.Move(from, to);
            }
        }

        private static string Serialized(CareerState state) => CareerStateWriter.Write(state).ToString(Newtonsoft.Json.Formatting.None);

        private static string Loaded(IFileStore store)
        {
            var r = SaveService.Load(store, CareerSession.Slot);
            Assert.IsTrue(r.IsSuccess, r.ToString());
            return Serialized(r.Value);
        }

        private static readonly string[] SlotFiles =
        {
            SaveSlotPaths.Body(CareerSession.Slot), SaveSlotPaths.Meta(CareerSession.Slot),
            SaveSlotPaths.BodyBackup(CareerSession.Slot, "last"), SaveSlotPaths.MetaBackup(CareerSession.Slot, "last"),
        };

        private static Dictionary<string, byte[]> Files(IFileStore store) =>
            SlotFiles.Where(store.Exists).ToDictionary(f => f, store.ReadAllBytes);

        /// <summary>A managed career with cash for any command, saved twice (so the "last" backup exists) with the cash in it.</summary>
        private static (ShellPresenter Shell, FailingStore Store) Career()
        {
            var store = new FailingStore(NewStore());
            var shell = NewCareer(store);
            var club = shell.Session.ManagedClub;
            LedgerBook.Post(shell.Session.Career.State, club.Id, shell.Session.Career.Today, LedgerCategory.Prizes, 100_000_000); // cash through the ledger
            Assert.IsTrue(shell.Session.Save().IsSuccess);
            Assert.IsTrue(shell.Session.Save().IsSuccess);
            shell.OpenClub();
            return (shell, store);
        }

        /// <summary>A level with a fee when there is one above, so the ledger is involved.</summary>
        private static int StaffTarget(ShellPresenter shell, StaffRole role)
        {
            int current = shell.Session.Career.State.World.StaffOf(shell.Session.ManagedClub.Id, role).Level, max = ClubManagement.StaffMaxLevel(shell.Session.Db);
            return current < max ? max : 1;
        }

        private static void Run(ShellPresenter shell, bool facility)
        {
            if (facility) shell.RequestFacilityUpgrade((int)FacilityType.Stadium);
            else shell.RequestStaffReplacement((int)StaffRole.Physio, StaffTarget(shell, StaffRole.Physio));
            Assert.IsNotNull(shell.Club().Confirm);
            Assert.DoesNotThrow(shell.ConfirmClubAction, "no file error reaches the screen");
        }

        [Test]
        public void AFailedSave_UndoesTheCommand_AndTheScreenSaysTheSaveFailed(
            [Values(Failure.Refused, Failure.WriteThrows, Failure.BodyMoveThrows, Failure.MetaMoveThrows)] Failure failure,
            [Values(true, false)] bool facility)
        {
            var (shell, store) = Career();
            var state = shell.Session.Career.State;
            var club = shell.Session.ManagedClub;
            string before = Serialized(state);
            var files = Files(store);
            long balance = club.Balance;
            int ledger = state.Season.Ledger.Count, lastId = state.Ids.LastIssued, projects = state.World.FacilityProjects.Count;
            var staff = state.World.StaffMembers.ToList();
            var physio = state.World.StaffOf(club.Id, StaffRole.Physio);
            (Id, StaffRole, int) physioBefore = (physio.Id, physio.Role, physio.Level);

            store.Mode = failure;
            Run(shell, facility);

            // The screen: the save's own message (not "Operação inválida"), the confirmation closed.
            var m = shell.Club();
            Assert.IsTrue(m.MessageIsError, $"{failure}");
            Assert.AreEqual(shell.Session.Strings.Get("club.error.save_failed"), m.Message);
            Assert.AreNotEqual(shell.Session.Strings.Get("club.error.invalid"), m.Message);
            Assert.IsNull(m.Confirm, "the confirmation is over");

            // The career in memory is exactly the last save.
            Assert.AreEqual(before, Serialized(state), $"{failure}, facility {facility}: the whole state is back");
            Assert.AreEqual(balance, club.Balance, "cash");
            Assert.AreEqual(ledger, state.Season.Ledger.Count, "ledger");
            Assert.AreEqual(lastId, state.Ids.LastIssued, "Id counter");
            Assert.AreEqual(projects, state.World.FacilityProjects.Count, "works");
            Assert.IsNull(state.World.ActiveProjectOf(club.Id, FacilityType.Stadium));
            CollectionAssert.AreEqual(staff, state.World.StaffMembers, "the same staff members, in the same order");
            var p = state.World.StaffOf(club.Id, StaffRole.Physio);
            Assert.AreEqual(physioBefore, (p.Id, p.Role, p.Level), "the previous physio: Id, role and level");
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
                Assert.AreEqual(1, state.World.StaffMembers.Count(s => s.ClubId == club.Id && s.Role == role), $"one {role}");

            // The disk: what loads is the last save; a refused or unwritten save leaves the files as they were.
            Assert.AreEqual(before, Loaded(store), $"{failure}: the save that loads");
            if (failure == Failure.Refused || failure == Failure.WriteThrows)
            {
                var after = Files(store);
                CollectionAssert.AreEquivalent(files.Keys, after.Keys);
                foreach (var f in files) CollectionAssert.AreEqual(f.Value, after[f.Key], f.Key);
            }
        }

        [Test]
        public void ADefectInTheSave_IsNotHidden_ButTheCommandIsUndone([Values(true, false)] bool facility)
        {
            var (shell, store) = Career();
            var state = shell.Session.Career.State;
            string before = Serialized(state);
            store.Mode = Failure.Defect;
            if (facility) Assert.Throws<InvalidOperationException>(() => shell.Session.StartFacilityUpgrade(FacilityType.Stadium));
            else Assert.Throws<InvalidOperationException>(() => shell.Session.ReplaceStaff(StaffRole.Physio, StaffTarget(shell, StaffRole.Physio)));
            Assert.AreEqual(before, Serialized(state), "the whole state is back");
            store.Mode = Failure.None;
            Assert.AreEqual(before, Loaded(store), "the save that loads");
        }

        [Test]
        public void AfterAFailedSave_TheNextSavesDoNotBringTheCommandBack(
            [Values(Failure.Refused, Failure.WriteThrows, Failure.BodyMoveThrows, Failure.MetaMoveThrows)] Failure failure,
            [Values(true, false)] bool facility)
        {
            var (shell, store) = Career();
            var state = shell.Session.Career.State;
            var club = shell.Session.ManagedClub;
            var physio = state.World.StaffOf(club.Id, StaffRole.Physio);
            int ledger = state.Season.Ledger.Count;
            store.Mode = failure;
            Run(shell, facility);

            store.Mode = Failure.None;
            shell.Session.AdvanceToNextMatch();
            var saved = SaveService.Load(store, CareerSession.Slot).Value;
            Assert.IsNull(saved.World.ActiveProjectOf(club.Id, FacilityType.Stadium), $"{failure}: no work saved later");
            Assert.AreEqual(0, saved.Season.Ledger.Skip(ledger).Count(e => e.ClubId == club.Id && e.Category == LedgerCategory.FacilityInvestment));
            Assert.AreEqual(physio.Id, saved.World.StaffOf(club.Id, StaffRole.Physio).Id, "the same physio saved later");
            Assert.AreEqual(Serialized(state), Serialized(saved));
        }

        [Test]
        public void RetriedAfterTheStorageRecovers_TheCommandRunsAndIsPaidOnce(
            [Values(Failure.Refused, Failure.WriteThrows, Failure.BodyMoveThrows, Failure.MetaMoveThrows)] Failure failure,
            [Values(true, false)] bool facility)
        {
            var (shell, store) = Career();
            var state = shell.Session.Career.State;
            var club = shell.Session.ManagedClub;
            var db = shell.Session.Db;
            long balance = club.Balance;
            int ledger = state.Season.Ledger.Count;
            int physioLevel = state.World.StaffOf(club.Id, StaffRole.Physio).Level, target = StaffTarget(shell, StaffRole.Physio);
            long price = facility
                ? ClubManagement.FacilityUpgradeCost(db, club, FacilityType.Stadium)
                : ClubManagement.StaffReplacementFee(db, physioLevel, target);

            store.Mode = failure;
            Run(shell, facility);
            Assert.IsTrue(shell.Club().MessageIsError);
            store.Mode = Failure.None;
            Run(shell, facility);
            Assert.IsFalse(shell.Club().MessageIsError, shell.Club().Message);

            Assert.AreEqual(balance - price, club.Balance, $"{failure}: charged once");
            int entries = price > 0 ? 1 : 0;
            Assert.AreEqual(entries, state.Season.Ledger.Count - ledger, "one ledger entry");
            if (facility) Assert.AreEqual(1, state.World.FacilityProjects.Count(w => w.ClubId == club.Id && w.Type == FacilityType.Stadium));
            else Assert.AreEqual(target, state.World.StaffOf(club.Id, StaffRole.Physio).Level);
            Assert.AreEqual(Serialized(state), Loaded(store), "saved as it is in memory");
        }
    }
}
