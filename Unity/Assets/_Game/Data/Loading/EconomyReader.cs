using Game.Core.Results;
using Game.Data.Career;

namespace Game.Data.Loading
{
    /// <summary>Strict reader + semantic checks for Data/Career/economy.json.</summary>
    public static class EconomyReader
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidEconomy = "INVALID_ECONOMY";

        public static Result<EconomyDefinition> Read(string json)
        {
            var j = new StrictJson(GameDataLoader.EconomyFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "tv", "ticketing", "sponsorship", "maintenance", "prizes", "cashAlert");
            if (root == null) return Result<EconomyDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidEconomy, m); }
            bool Prob(float p) => p >= 0f && p <= 1f;

            var e = new EconomyDefinition();

            var tv = j.Object(root, "tv", "root");
            if (tv != null)
            {
                j.Keys(tv, "tv", "perSeasonByDivision");
                e.Tv = new TvParameters { PerSeasonByDivision = j.LongList(tv, "perSeasonByDivision", "tv") };
                Check(e.Tv.PerSeasonByDivision.Count > 0, "tv.perSeasonByDivision cannot be empty.");
            }

            var ti = j.Object(root, "ticketing", "root");
            if (ti != null)
            {
                j.Keys(ti, "ticketing", "attendanceShare", "priceByDivision", "winBonusShare");
                e.Ticketing = new TicketingParameters
                {
                    AttendanceShare = j.Float(ti, "attendanceShare", "ticketing"),
                    PriceByDivision = j.LongList(ti, "priceByDivision", "ticketing"),
                    WinBonusShare = j.Float(ti, "winBonusShare", "ticketing"),
                };
                Check(Prob(e.Ticketing.AttendanceShare) && e.Ticketing.PriceByDivision.Count > 0 && e.Ticketing.WinBonusShare >= 0f,
                    "ticketing values invalid.");
            }

            var sp = j.Object(root, "sponsorship", "root");
            if (sp != null)
            {
                j.Keys(sp, "sponsorship", "perSeasonByDivision");
                e.Sponsorship = new SponsorshipParameters { PerSeasonByDivision = j.LongList(sp, "perSeasonByDivision", "sponsorship") };
                Check(e.Sponsorship.PerSeasonByDivision.Count > 0, "sponsorship.perSeasonByDivision cannot be empty.");
            }

            var ma = j.Object(root, "maintenance", "root");
            if (ma != null)
            {
                j.Keys(ma, "maintenance", "perSeasonByStadiumLevel");
                e.Maintenance = new MaintenanceParameters { PerSeasonByStadiumLevel = j.LongList(ma, "perSeasonByStadiumLevel", "maintenance") };
                Check(e.Maintenance.PerSeasonByStadiumLevel.Count > 0, "maintenance.perSeasonByStadiumLevel cannot be empty.");
            }

            var pr = j.Object(root, "prizes", "root");
            if (pr != null)
            {
                j.Keys(pr, "prizes", "base", "leagueChampionMultiplier", "cupWinnerMultiplier", "cupRunnerUpMultiplier");
                e.Prizes = new PrizeParameters
                {
                    Base = j.Long(pr, "base", "prizes"),
                    LeagueChampionMultiplier = j.FloatList(pr, "leagueChampionMultiplier", "prizes"),
                    CupWinnerMultiplier = j.Float(pr, "cupWinnerMultiplier", "prizes"),
                    CupRunnerUpMultiplier = j.Float(pr, "cupRunnerUpMultiplier", "prizes"),
                };
                Check(e.Prizes.Base > 0 && e.Prizes.LeagueChampionMultiplier.Count > 0, "prizes values invalid.");
            }

            var ca = j.Object(root, "cashAlert", "root");
            if (ca != null)
            {
                j.Keys(ca, "cashAlert", "alertMonths", "transferLockoutMonths");
                e.CashAlert = new CashAlertParameters
                {
                    AlertMonths = j.Int(ca, "alertMonths", "cashAlert"),
                    TransferLockoutMonths = j.Int(ca, "transferLockoutMonths", "cashAlert"),
                };
                Check(e.CashAlert.AlertMonths > 0 && e.CashAlert.TransferLockoutMonths >= e.CashAlert.AlertMonths,
                    "cashAlert months invalid.");
            }

            return j.Ok ? Result<EconomyDefinition>.Ok(e) : Result<EconomyDefinition>.Fail(j.Errors);
        }
    }
}
