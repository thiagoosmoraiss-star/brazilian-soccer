using System;
using Game.Data.Career;

namespace Game.Rules.Economy
{
    /// <summary>
    /// Pure club-economy math (TECHNICAL_SPEC §13, GAME_DESIGN §11, B6, X-44): TV, gate, sponsorship, maintenance and
    /// prize amounts. Coefficients live in Data/Career/economy.json.
    /// </summary>
    public static class EconomyRules
    {
        public static long TvRevenuePerMonth(EconomyDefinition e, int divisionIndex) =>
            ScaleFor(e.Tv.PerSeasonByDivision, divisionIndex) / 12;

        /// <summary>
        /// Gate revenue for one home match (GAME_DESIGN §11: fan base, ticket price, a bonus on a win). Attendance
        /// is capped by the stadium's capacity: the fan base (hundreds of thousands) is a club-strength signal, not
        /// how many seats exist.
        /// </summary>
        public static long GateRevenue(EconomyDefinition e, int divisionIndex, int fans, int stadiumCapacity, bool won)
        {
            var t = e.Ticketing;
            long attendance = Math.Min(stadiumCapacity, (long)Math.Round(fans * t.AttendanceShare));
            long revenue = attendance * ScaleFor(t.PriceByDivision, divisionIndex);
            if (won) revenue += (long)Math.Round(revenue * t.WinBonusShare);
            return revenue;
        }

        public static long SponsorshipPerMonth(EconomyDefinition e, int divisionIndex) =>
            ScaleFor(e.Sponsorship.PerSeasonByDivision, divisionIndex) / 12;

        public static long MaintenancePerMonth(EconomyDefinition e, int stadiumLevel) =>
            ScaleFor(e.Maintenance.PerSeasonByStadiumLevel, stadiumLevel - 1) / 12;

        public static long LeagueChampionPrize(EconomyDefinition e, int divisionIndex) =>
            (long)(e.Prizes.Base * ScaleFor(e.Prizes.LeagueChampionMultiplier, divisionIndex));

        public static long CupWinnerPrize(EconomyDefinition e) => (long)(e.Prizes.Base * e.Prizes.CupWinnerMultiplier);

        public static long CupRunnerUpPrize(EconomyDefinition e) => (long)(e.Prizes.Base * e.Prizes.CupRunnerUpMultiplier);

        private static long ScaleFor(System.Collections.Generic.IReadOnlyList<long> scale, int index)
        {
            if (scale == null || scale.Count == 0) return 0L;
            int i = Math.Max(0, Math.Min(index, scale.Count - 1));
            return scale[i];
        }

        private static float ScaleFor(System.Collections.Generic.IReadOnlyList<float> scale, int index)
        {
            if (scale == null || scale.Count == 0) return 0f;
            int i = Math.Max(0, Math.Min(index, scale.Count - 1));
            return scale[i];
        }
    }
}
