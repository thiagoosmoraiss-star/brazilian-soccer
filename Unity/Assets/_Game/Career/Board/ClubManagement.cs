using System;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Core.Results;
using Game.Data.Loading;
using Game.Rules.Board;

namespace Game.Career.Board
{
    /// <summary>
    /// The managed club's administrative commands (F2a, X-122, D-32b, D-32c, X-124): the user decides the club's works and
    /// staff, which <see cref="Facilities"/> and <see cref="Staff"/> no longer decide for it. Each command checks every rule
    /// first and only then changes the state, in one operation through <see cref="LedgerBook"/>: a refused command changes
    /// nothing. Payments are checked against the cash right now (<see cref="Club.Balance"/>), not the month-end
    /// <see cref="Club.Budget"/> the AI uses (X-124).
    /// </summary>
    public static class ClubManagement
    {
        public const string NotManaged = "NOT_MANAGED_CLUB";
        public const string MaxLevel = "FACILITY_MAX_LEVEL";
        public const string WorkInProgress = "FACILITY_WORK_IN_PROGRESS";
        public const string NoCash = "INSUFFICIENT_CASH";
        public const string InvalidLevel = "STAFF_INVALID_LEVEL";
        public const string NoStaffMember = "STAFF_MISSING";

        // ---------------- Facilities (D-32b) ----------------

        /// <summary>Highest level of a facility: one more than its upgrade costs (`facilities.json`).</summary>
        public static int FacilityMaxLevel(GameDatabase db, FacilityType type) =>
            (type == FacilityType.Stadium ? db.Facilities.Stadium.UpgradeCostPerLevel.Count : db.Facilities.TrainingCenter.UpgradeCostPerLevel.Count) + 1;

        public static int FacilityLevel(Club club, FacilityType type) => type == FacilityType.Stadium ? club.Stadium.Level : club.TrainingCenterLevel;

        /// <summary>Cost of the next level (the facility's current level + 1); only valid below the maximum.</summary>
        public static long FacilityUpgradeCost(GameDatabase db, Club club, FacilityType type) =>
            type == FacilityType.Stadium
                ? BoardRules.StadiumUpgradeCost(db.Facilities, club.Stadium.Level)
                : BoardRules.TrainingCenterUpgradeCost(db.Facilities, club.TrainingCenterLevel);

        public static int FacilityUpgradeMonths(GameDatabase db, FacilityType type) =>
            type == FacilityType.Stadium ? db.Facilities.Stadium.UpgradeDurationMonths : db.Facilities.TrainingCenter.UpgradeDurationMonths;

        /// <summary>Why an upgrade of <paramref name="type"/> would be refused now, or null when it can start.</summary>
        public static Error? CheckFacilityUpgrade(GameDatabase db, CareerState state, FacilityType type)
        {
            var club = Managed(state, out var error);
            if (club == null) return error;
            if (FacilityLevel(club, type) >= FacilityMaxLevel(db, type)) return new Error(MaxLevel, $"{type} is already at its highest level.");
            if (state.World.ActiveProjectOf(club.Id, type) != null) return new Error(WorkInProgress, $"{type} already has a work in progress.");
            long cost = FacilityUpgradeCost(db, club, type);
            if (club.Balance < cost) return new Error(NoCash, $"{type} upgrade costs {cost}; cash is {club.Balance}.");
            return null;
        }

        /// <summary>D-32b: one level up, the whole cost paid now, no cancelling; one work at a time per facility (the stadium
        /// and the training centre can both be in works). It completes on its date like every club's (<see cref="Facilities"/>).</summary>
        public static Result StartFacilityUpgrade(GameDatabase db, CareerState state, FacilityType type, DateTime today)
        {
            var error = CheckFacilityUpgrade(db, state, type);
            if (error != null) return Result.Fail(error.Value.Code, error.Value.Message);
            var club = state.Club(state.ManagedClubId.Value);
            long cost = FacilityUpgradeCost(db, club, type);
            LedgerBook.Post(state, club.Id, today, LedgerCategory.FacilityInvestment, -cost);
            state.World.FacilityProjectList.Add(new FacilityProject
            {
                Id = state.Ids.Next(), ClubId = club.Id, Type = type, TargetLevel = FacilityLevel(club, type) + 1,
                CompletesOn = today.AddMonths(FacilityUpgradeMonths(db, type)),
            });
            return Result.Success;
        }

        /// <summary>D-32b (X-123): a stadium below the division's minimum is only a warning; nothing is blocked.</summary>
        public static bool StadiumBelowMinimum(GameDatabase db, Club club, int divisionIndex) =>
            !Facilities.MeetsStadiumRequirement(db, club, divisionIndex);

        // ---------------- Staff (D-32c) ----------------

        public static int StaffMaxLevel(GameDatabase db) => db.Staff.WagePerLevel.Count;

        /// <summary>The fee to replace a level-<paramref name="current"/> member by a level-<paramref name="target"/> one: the sum of
        /// the promotion costs of every step in between going up (the price the AI pays one level at a time), none going down
        /// (X-123, X-124).</summary>
        public static long StaffReplacementFee(GameDatabase db, int current, int target)
        {
            long fee = 0;
            for (int level = current; level < target; level++) fee += BoardRules.StaffUpgradeCost(db.Staff, level);
            return fee;
        }

        /// <summary>Why replacing the <paramref name="role"/> by a level-<paramref name="level"/> professional would be refused now,
        /// or null when it can be done.</summary>
        public static Error? CheckStaffReplacement(GameDatabase db, CareerState state, StaffRole role, int level)
        {
            var club = Managed(state, out var error);
            if (club == null) return error;
            if (!Enum.IsDefined(typeof(StaffRole), role)) return new Error(InvalidLevel, $"Unknown staff role {(int)role}.");
            var member = state.World.StaffOf(club.Id, role);
            if (member == null) return new Error(NoStaffMember, $"The club has no {role}.");
            if (level < 1 || level > StaffMaxLevel(db)) return new Error(InvalidLevel, $"Staff level {level} is outside 1-{StaffMaxLevel(db)}.");
            if (level == member.Level) return new Error(InvalidLevel, $"The {role} is already level {level}.");
            long fee = StaffReplacementFee(db, member.Level, level);
            if (club.Balance < fee) return new Error(NoCash, $"Replacing the {role} costs {fee}; cash is {club.Balance}.");
            return null;
        }

        /// <summary>D-32c (X-122 (2)): the member leaves only with his replacement arriving in the same operation, so the role is
        /// never empty. The new professional's wage is his level's, paid at the month end like every club's (<see cref="Staff"/>).
        /// The fee goes to <see cref="LedgerCategory.StaffWages"/>, like the AI's promotions (X-125).</summary>
        public static Result ReplaceStaff(GameDatabase db, CareerState state, StaffRole role, int level, DateTime today)
        {
            var error = CheckStaffReplacement(db, state, role, level);
            if (error != null) return Result.Fail(error.Value.Code, error.Value.Message);
            var club = state.Club(state.ManagedClubId.Value);
            var member = state.World.StaffOf(club.Id, role);
            long fee = StaffReplacementFee(db, member.Level, level);
            if (fee > 0) LedgerBook.Post(state, club.Id, today, LedgerCategory.StaffWages, -fee);
            int index = state.World.StaffList.IndexOf(member);
            state.World.StaffList[index] = new StaffMember { Id = state.Ids.Next(), ClubId = club.Id, Role = role, Level = level };
            return Result.Success;
        }

        // ---------------- Undo when the save fails ----------------

        /// <summary>
        /// What <see cref="StartFacilityUpgrade"/> and <see cref="ReplaceStaff"/> change, taken just before one of them runs:
        /// the managed club's cash, the ledger, the Id counter, the works and the staff list. <see cref="Restore"/> puts it
        /// back, so a command whose save failed leaves the career in memory as it was (the last save). Valid only for those
        /// two commands and only right after the command: nothing else may run in between.
        /// </summary>
        public sealed class Checkpoint
        {
            private readonly Club _club;
            private readonly long _balance;
            private readonly int _ledgerCount;
            private readonly int _lastIssuedId;
            private readonly int _projectCount;
            private readonly StaffMember[] _staff;

            private Checkpoint(CareerState state)
            {
                _club = state.ManagedClubId.HasValue ? state.Club(state.ManagedClubId.Value) : null;
                _balance = _club?.Balance ?? 0;
                _ledgerCount = state.Season.Ledger.Count;
                _lastIssuedId = state.Ids.LastIssued;
                _projectCount = state.World.FacilityProjectList.Count;
                // The members themselves are not changed by the commands (the replaced one is swapped out), so the same
                // objects, in the same order, give back each Id, role and level.
                _staff = state.World.StaffList.ToArray();
            }

            public static Checkpoint Take(CareerState state) => new Checkpoint(state ?? throw new ArgumentNullException(nameof(state)));

            public void Restore(CareerState state)
            {
                if (_club != null) _club.Balance = _balance;
                var ledger = state.Season.Ledger;
                if (ledger.Count > _ledgerCount) ledger.RemoveRange(_ledgerCount, ledger.Count - _ledgerCount);
                var projects = state.World.FacilityProjectList;
                if (projects.Count > _projectCount) projects.RemoveRange(_projectCount, projects.Count - _projectCount);
                state.World.StaffList.Clear();
                state.World.StaffList.AddRange(_staff);
                state.Ids = new IdAllocator(_lastIssuedId);
            }
        }

        private static Club Managed(CareerState state, out Error? error)
        {
            error = null;
            Club club = state.ManagedClubId.HasValue ? state.Club(state.ManagedClubId.Value) : null;
            if (club == null) error = new Error(NotManaged, "There is no managed club.");
            return club;
        }

        /// <summary>True for the club the user manages (X-122 (1): the AI takes no administrative decision for it).</summary>
        public static bool IsManaged(CareerState state, Id clubId) => state.ManagedClubId.HasValue && state.ManagedClubId.Value == clubId;
    }
}
