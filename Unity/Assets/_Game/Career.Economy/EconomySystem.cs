using System;
using System.Collections.Generic;
using Game.Career.Season;
using Game.Career.World;
using Game.Core.Ids;
using Game.Data.Loading;
using Game.Rules.Economy;

namespace Game.Career.Economy
{
    /// <summary>
    /// Club economy (B6, X-44): TV, gate (home matches), sponsorship, wages and maintenance each month end; league
    /// and cup prizes at season end. Every movement goes through <see cref="LedgerBook"/>, so
    /// <see cref="Club.Balance"/> is always exactly the ledger's sum. A club 3 months in the red (GAME_DESIGN §11)
    /// is flagged for the market (B5) to stop signing for it.
    /// </summary>
    public sealed class EconomySystem : IEconomySystem
    {
        public void MonthEnd(CareerState state, GameDatabase db, DateTime date)
        {
            var world = state.World;
            var season = state.Season;
            var e = db.Economy;

            // Ticketing follows the fixtures actually played this month; TV and sponsorship are flat broadcasting/
            // sponsor deals spread evenly across the year, so a club's income does not swing with the fixture list
            // (e.g. no league games in January-March, before the season kicks off).
            foreach (var f in season.AllFixtures())
            {
                if (!f.Played || f.Date.Year != date.Year || f.Date.Month != date.Month) continue;
                var home = state.Club(f.HomeClubId);
                LedgerBook.Post(state, f.HomeClubId, date, LedgerCategory.Ticketing,
                    EconomyRules.GateRevenue(e, home.DivisionIndex, home.Fans, home.Stadium.Capacity, f.Winner == f.HomeClubId));
            }

            foreach (var club in world.Clubs)
            {
                LedgerBook.Post(state, club.Id, date, LedgerCategory.Tv, EconomyRules.TvRevenuePerMonth(e, club.DivisionIndex));
                LedgerBook.Post(state, club.Id, date, LedgerCategory.Sponsorship,
                    EconomyRules.SponsorshipPerMonth(e, club.DivisionIndex));
                LedgerBook.Post(state, club.Id, date, LedgerCategory.Maintenance,
                    -EconomyRules.MaintenancePerMonth(e, club.Stadium.Level));

                long wageBill = 0;
                foreach (var p in world.SquadOf(club.Id)) wageBill += world.ContractOf(p.Id)?.Wage ?? 0;
                LedgerBook.Post(state, club.Id, date, LedgerCategory.Wages, -(wageBill / 12));

                UpdateCashStateAndBudget(e, club);
            }
        }

        public void SeasonEnd(CareerState state, GameDatabase db, IReadOnlyList<IReadOnlyList<Id>> finalTables,
            Id cupWinner, Id cupRunnerUp, int year)
        {
            var e = db.Economy;
            var date = new DateTime(year, 12, 31);
            for (int d = 0; d < finalTables.Count; d++)
            {
                if (finalTables[d].Count == 0) continue;
                LedgerBook.Post(state, finalTables[d][0], date, LedgerCategory.Prizes, EconomyRules.LeagueChampionPrize(e, d));
            }
            if (!cupWinner.IsNone) LedgerBook.Post(state, cupWinner, date, LedgerCategory.Prizes, EconomyRules.CupWinnerPrize(e));
            if (!cupRunnerUp.IsNone) LedgerBook.Post(state, cupRunnerUp, date, LedgerCategory.Prizes, EconomyRules.CupRunnerUpPrize(e));

            foreach (var club in state.World.Clubs) UpdateCashStateAndBudget(e, club);
        }

        /// <summary>Negative-cash streak, alert/lockout flags (GAME_DESIGN §11), and the market's (B5) budget sync.</summary>
        private static void UpdateCashStateAndBudget(Data.Career.EconomyDefinition e, Club club)
        {
            club.MonthsNegativeCash = club.Balance < 0 ? club.MonthsNegativeCash + 1 : 0;
            club.CashAlert = club.MonthsNegativeCash >= e.CashAlert.AlertMonths;
            club.TransferLockout = club.MonthsNegativeCash >= e.CashAlert.TransferLockoutMonths;
            club.Budget = Math.Max(0, club.Balance);
        }
    }
}
