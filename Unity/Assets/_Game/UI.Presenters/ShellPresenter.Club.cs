using System;
using System.Collections.Generic;
using System.Linq;
using Game.Career.Board;
using Game.Career.World;
using Game.Core.Results;
using Game.Rules.Board;

namespace Game.UI.Presenters
{
    /// <summary>F2a (X-122 to X-125): the Diretoria, Clube and Finanças screens and the managed club's commands. A command
    /// that spends money waits for a confirmation; the career checks the rules again when it runs (ClubManagement).</summary>
    public sealed partial class ShellPresenter
    {
        private enum ClubAction { None, Facility, Staff }

        private ClubAction _clubAction;
        private FacilityType _clubFacility;
        private StaffRole _clubRole;
        private int _clubLevel;
        private string _clubMessage;
        private bool _clubMessageIsError;

        private static readonly FacilityType[] FacilityTypes = { FacilityType.Stadium, FacilityType.TrainingCenter };
        private static readonly StaffRole[] StaffRoles = { StaffRole.Physio, StaffRole.Assistant, StaffRole.Scout };

        // ---------------------------------------------------------------- commands

        public void OpenClub()
        {
            ClearClubState();
            Nav.Push(ScreenId.Club);
        }

        public void OpenFinances() => Nav.Push(ScreenId.Finances);

        /// <summary>Asks to start an upgrade of the facility (<see cref="FacilityType"/> as int); <see cref="ConfirmClubAction"/> runs it.</summary>
        public void RequestFacilityUpgrade(int type)
        {
            _clubAction = ClubAction.Facility;
            _clubFacility = (FacilityType)type;
            _clubMessage = null;
            Nav.Replace(ScreenId.Club);
        }

        /// <summary>Asks to replace the staff member (<see cref="StaffRole"/> as int) by a professional of <paramref name="level"/>.</summary>
        public void RequestStaffReplacement(int role, int level)
        {
            _clubAction = ClubAction.Staff;
            _clubRole = (StaffRole)role;
            _clubLevel = level;
            _clubMessage = null;
            Nav.Replace(ScreenId.Club);
        }

        public void ConfirmClubAction()
        {
            Result r;
            switch (_clubAction)
            {
                case ClubAction.Facility: r = Session.StartFacilityUpgrade(_clubFacility); break;
                case ClubAction.Staff: r = Session.ReplaceStaff(_clubRole, _clubLevel); break;
                default: return;
            }
            _clubMessageIsError = !r.IsSuccess;
            _clubMessage = r.IsSuccess
                ? (_clubAction == ClubAction.Facility ? S.Format("club.done_facility", FacilityName(_clubFacility)) : S.Format("club.done_staff", RoleName(_clubRole), _clubLevel))
                : ErrorText(r.Errors[0].Code);
            _clubAction = ClubAction.None;
            Nav.Replace(ScreenId.Club);
        }

        public void CancelClubAction()
        {
            _clubAction = ClubAction.None;
            Nav.Replace(ScreenId.Club);
        }

        private void ClearClubState()
        {
            _clubAction = ClubAction.None;
            _clubMessage = null;
        }

        // ---------------------------------------------------------------- screens

        public BoardModel Board()
        {
            var club = Session.ManagedClub;
            var db = Session.Db;
            var m = new BoardModel
            {
                Header = Header(),
                Title = S.Get("board.title"),
                ObjectiveLabel = S.Get("board.objective"),
                ConfidenceLabel = S.Get("board.confidence"),
                SponsorshipLabel = S.Get("board.sponsorship"),
                SponsorshipHint = S.Get("board.sponsorship_hint"),
                Club = new ButtonModel(S.Get("board.open_club")),
                Finances = new ButtonModel(S.Get("board.open_finances")),
            };
            if (State.Season.Objectives.TryGetValue(club.Id, out var objective))
            {
                m.Objective = ObjectiveText(objective.Type);
                m.ObjectiveHint = S.Format("board.objective_hint", db.Board.Confidence.ObjectiveMetBonus, db.Board.Confidence.ObjectiveMissedPenalty);
            }
            else m.Objective = S.Get("common.none");
            var manager = State.World.ManagerOf(club.Id);
            int confidence = manager?.Confidence ?? 0;
            m.ConfidenceValue = confidence;
            m.Confidence = S.Format("board.confidence_value", confidence);
            m.ConfidenceAtRisk = confidence - db.Board.Confidence.ObjectiveMissedPenalty < db.Board.Confidence.DismissalThreshold;
            m.ConfidenceHint = S.Format("board.confidence_hint", db.Board.Confidence.DismissalThreshold);
            long sponsorship = db.Economy.Sponsorship.PerSeasonByDivision[Math.Min(club.DivisionIndex, db.Economy.Sponsorship.PerSeasonByDivision.Count - 1)];
            m.Sponsorship = S.Format("board.sponsorship_value", Fmt.Money(S, sponsorship));
            return m;
        }

        public ClubModel Club()
        {
            var club = Session.ManagedClub;
            var db = Session.Db;
            var m = new ClubModel
            {
                Header = Header(),
                Back = new ButtonModel(S.Get("common.back")),
                Title = S.Get("club.title"),
                FacilitiesTitle = S.Get("club.facilities"),
                StaffTitle = S.Get("club.staff"),
                StaffHint = S.Get("club.staff_hint"),
                Message = _clubMessage,
                MessageIsError = _clubMessageIsError,
            };

            foreach (var type in FacilityTypes)
            {
                int level = ClubManagement.FacilityLevel(club, type), max = ClubManagement.FacilityMaxLevel(db, type);
                var card = new FacilityCardModel
                {
                    Type = (int)type,
                    Name = FacilityName(type),
                    Level = S.Format("club.level_of", level, max),
                };
                if (type == FacilityType.Stadium)
                {
                    long maintenance = db.Economy.Maintenance.PerSeasonByStadiumLevel[level - 1];
                    card.Detail = S.Format("club.stadium_detail", Fmt.Thousands(club.Stadium.Capacity), Fmt.Money(S, maintenance));
                    int minimum = BoardRules.StadiumMinimumLevel(db.Facilities, club.DivisionIndex);
                    card.Requirement = S.Format("club.stadium_minimum", minimum, Division(club.DivisionIndex));
                    if (ClubManagement.StadiumBelowMinimum(db, club, club.DivisionIndex)) card.Warning = S.Format("club.stadium_warning", minimum);
                }
                else card.Detail = S.Format("club.training_detail", Fmt.Decimal(BoardRules.TrainingCenterFactor(db.Facilities, level), 2));

                var work = State.World.ActiveProjectOf(club.Id, type);
                if (work != null) card.Work = S.Format("club.work", work.TargetLevel, Fmt.Date(work.CompletesOn));
                if (level < max)
                {
                    var error = ClubManagement.CheckFacilityUpgrade(db, State, type);
                    card.Upgrade = new ButtonModel(S.Format("club.upgrade", level + 1, Fmt.Money(S, ClubManagement.FacilityUpgradeCost(db, club, type)),
                        ClubManagement.FacilityUpgradeMonths(db, type)), error == null);
                    if (error != null) card.Reason = ErrorText(error.Value.Code);
                }
                else card.Reason = S.Get("club.error.max_level");
                m.Facilities.Add(card);
            }

            foreach (var role in StaffRoles)
            {
                var member = State.World.StaffOf(club.Id, role);
                if (member == null) continue;
                var card = new StaffCardModel
                {
                    Role = (int)role,
                    Name = RoleName(role),
                    Level = S.Format("club.level_of", member.Level, ClubManagement.StaffMaxLevel(db)),
                    Wage = S.Format("club.staff_wage", Fmt.Money(S, BoardRules.StaffWage(db.Staff, member.Level))),
                    Effect = StaffEffect(role, member.Level),
                };
                for (int level = 1; level <= ClubManagement.StaffMaxLevel(db); level++)
                {
                    bool current = level == member.Level;
                    long fee = current ? 0 : ClubManagement.StaffReplacementFee(db, member.Level, level);
                    card.Levels.Add(new StaffLevelOptionModel
                    {
                        Level = level,
                        Label = current ? S.Format("club.staff_current", level) : fee > 0 ? S.Format("club.staff_option_fee", level, Fmt.Money(S, fee)) : S.Format("club.staff_option_free", level),
                        Current = current,
                        Enabled = !current && ClubManagement.CheckStaffReplacement(db, State, role, level) == null,
                    });
                }
                m.Staff.Add(card);
            }

            m.Confirm = ConfirmFor(club);
            return m;
        }

        private ConfirmModel ConfirmFor(Club club)
        {
            var db = Session.Db;
            string text;
            switch (_clubAction)
            {
                case ClubAction.Facility:
                    int level = ClubManagement.FacilityLevel(club, _clubFacility);
                    text = S.Format("club.confirm_facility", FacilityName(_clubFacility), level + 1, Fmt.Money(S, ClubManagement.FacilityUpgradeCost(db, club, _clubFacility)),
                        ClubManagement.FacilityUpgradeMonths(db, _clubFacility));
                    break;
                case ClubAction.Staff:
                    var member = State.World.StaffOf(club.Id, _clubRole);
                    long fee = ClubManagement.StaffReplacementFee(db, member.Level, _clubLevel);
                    text = S.Format("club.confirm_staff", RoleName(_clubRole), member.Level, _clubLevel,
                        fee > 0 ? Fmt.Money(S, fee) : S.Get("club.no_fee"), Fmt.Money(S, BoardRules.StaffWage(db.Staff, _clubLevel)));
                    break;
                default: return null;
            }
            return new ConfirmModel { Text = text, Confirm = new ButtonModel(S.Get("club.confirm")), Cancel = new ButtonModel(S.Get("club.cancel")) };
        }

        public FinancesModel Finances()
        {
            var club = Session.ManagedClub;
            var m = new FinancesModel
            {
                Header = Header(),
                Back = new ButtonModel(S.Get("common.back")),
                Title = S.Get("finances.title"),
                Cash = S.Format("finances.cash", Fmt.Money(S, club.Balance)),
                CashNegative = club.Balance < 0,
                SeasonTitle = S.Format("finances.season", State.Season.Year),
                MonthsTitle = S.Get("finances.months"),
                MonthHeader = S.Get("finances.month"),
                RevenueHeader = S.Get("finances.revenue_short"),
                ExpensesHeader = S.Get("finances.expenses_short"),
                NetHeader = S.Get("finances.net_short"),
            };
            if (club.TransferLockout) m.Alert = S.Get("finances.lockout");
            else if (club.CashAlert) m.Alert = S.Get("finances.alert");

            var entries = State.Season.Ledger.Where(e => e.ClubId == club.Id).ToList();
            long revenue = entries.Where(e => e.Amount > 0).Sum(e => e.Amount), expenses = entries.Where(e => e.Amount < 0).Sum(e => e.Amount);
            m.Revenue = S.Format("finances.revenue", Fmt.Money(S, revenue));
            m.Expenses = S.Format("finances.expenses", Fmt.Money(S, expenses));
            m.Net = S.Format("finances.net", Fmt.Money(S, revenue + expenses));
            m.NetNegative = revenue + expenses < 0;
            foreach (LedgerCategory category in Enum.GetValues(typeof(LedgerCategory)))
            {
                long sum = entries.Where(e => e.Category == category).Sum(e => e.Amount);
                if (sum == 0) continue;
                m.Categories.Add(new FinanceRowModel { Label = S.Get("finances.category." + CategoryKey(category)), Amount = Fmt.Money(S, sum), Negative = sum < 0 });
            }
            var today = Today;
            for (int month = 1; month <= 12; month++)
            {
                var inMonth = entries.Where(e => e.Date.Month == month).ToList();
                long r = inMonth.Where(e => e.Amount > 0).Sum(e => e.Amount), x = inMonth.Where(e => e.Amount < 0).Sum(e => e.Amount);
                bool future = State.Season.Year == today.Year && month > today.Month && inMonth.Count == 0;
                m.Months.Add(new FinanceMonthModel
                {
                    Month = Fmt.Month(S, month),
                    Revenue = future ? S.Get("common.none") : Fmt.Money(S, r),
                    Expenses = future ? S.Get("common.none") : Fmt.Money(S, x),
                    Net = future ? S.Get("common.none") : Fmt.Money(S, r + x),
                    NetNegative = r + x < 0,
                    Future = future,
                });
            }
            return m;
        }

        // ---------------------------------------------------------------- texts

        private string FacilityName(FacilityType type) => S.Get(type == FacilityType.Stadium ? "club.stadium" : "club.training_center");

        private string RoleName(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Physio: return S.Get("club.role.physio");
                case StaffRole.Assistant: return S.Get("club.role.assistant");
                default: return S.Get("club.role.scout");
            }
        }

        private string StaffEffect(StaffRole role, int level)
        {
            var s = Session.Db.Staff;
            switch (role)
            {
                case StaffRole.Physio: return S.Format("club.effect.physio", Fmt.Decimal(BoardRules.PhysioEnergyMultiplier(s, level), 2));
                case StaffRole.Assistant: return S.Format("club.effect.assistant", Fmt.Decimal(BoardRules.AssistantDevelopmentFactor(s, level), 2));
                default: return S.Format("club.effect.scout", Fmt.Decimal(BoardRules.ScoutNarrowing(s, level), 2));
            }
        }

        private string ErrorText(string code)
        {
            switch (code)
            {
                case ClubManagement.MaxLevel: return S.Get("club.error.max_level");
                case ClubManagement.WorkInProgress: return S.Get("club.error.work_in_progress");
                case ClubManagement.NoCash: return S.Get("club.error.no_cash");
                case CareerSession.SaveFailed: return S.Get("club.error.save_failed");
                default: return S.Get("club.error.invalid");
            }
        }

        private static string CategoryKey(LedgerCategory category)
        {
            switch (category)
            {
                case LedgerCategory.Tv: return "tv";
                case LedgerCategory.Ticketing: return "ticketing";
                case LedgerCategory.Sponsorship: return "sponsorship";
                case LedgerCategory.Prizes: return "prizes";
                case LedgerCategory.TransferIn: return "transfer_in";
                case LedgerCategory.Wages: return "wages";
                case LedgerCategory.Maintenance: return "maintenance";
                case LedgerCategory.TransferOut: return "transfer_out";
                case LedgerCategory.StaffWages: return "staff_wages";
                default: return "facility_investment";
            }
        }
    }
}
