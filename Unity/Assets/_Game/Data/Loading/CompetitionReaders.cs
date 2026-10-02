using System;
using System.Collections.Generic;
using Game.Core.Results;
using Game.Data.Competitions;

namespace Game.Data.Loading
{
    /// <summary>Strict readers + semantic checks for Data/Competitions/competitions.json and calendar.json.</summary>
    public static class CompetitionReaders
    {
        public const int SupportedSchemaVersion = 1;
        public const string InvalidCompetitions = "INVALID_COMPETITIONS";

        public static Result<CompetitionsDefinition> ReadCompetitions(string json)
        {
            var j = new StrictJson(GameDataLoader.CompetitionsFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "league", "cup", "matchRules");
            if (root == null) return Result<CompetitionsDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidCompetitions, m); }
            var d = new CompetitionsDefinition();

            var l = j.Object(root, "league", "root");
            if (l != null)
            {
                j.Keys(l, "league", "id", "name", "pointsWin", "pointsDraw", "tiebreakers", "cardWeights", "promoted", "relegated");
                var tbs = new List<Tiebreaker>();
                foreach (var name in j.StringList(l, "tiebreakers", "league"))
                    if (j.TryEnum(name, "league.tiebreakers", out Tiebreaker t)) tbs.Add(t);
                var cw = j.Object(l, "cardWeights", "league");
                if (cw != null) j.Keys(cw, "league.cardWeights", "yellow", "red");
                d.League = new LeagueDefinition
                {
                    Id = j.String(l, "id", "league"), Name = j.String(l, "name", "league"),
                    PointsWin = j.Int(l, "pointsWin", "league"), PointsDraw = j.Int(l, "pointsDraw", "league"),
                    Tiebreakers = tbs,
                    YellowCardWeight = cw == null ? 0 : j.Int(cw, "yellow", "league.cardWeights"),
                    RedCardWeight = cw == null ? 0 : j.Int(cw, "red", "league.cardWeights"),
                    Promoted = j.Int(l, "promoted", "league"), Relegated = j.Int(l, "relegated", "league"),
                };
                Check(d.League.PointsWin > d.League.PointsDraw && d.League.PointsDraw >= 0, "league points: win > draw >= 0.");
                Check(tbs.Count > 0 && tbs[tbs.Count - 1] == Tiebreaker.Draw, "league.tiebreakers must end with Draw (always decides).");
                Check(new HashSet<Tiebreaker>(tbs).Count == tbs.Count, "league.tiebreakers must not repeat.");
                Check(d.League.Promoted >= 0 && d.League.Relegated >= 0 && d.League.Promoted == d.League.Relegated,
                    "league: promoted must equal relegated (constant clubs per division).");
            }

            var c = j.Object(root, "cup", "root");
            if (c != null)
            {
                j.Keys(c, "cup", "id", "name", "clubs", "qualifiersPerDivision", "home", "finalNeutral", "firstSeasonQualification");
                j.TryEnum(j.String(c, "home", "cup"), "cup.home", out CupHomeRule home);
                j.TryEnum(j.String(c, "firstSeasonQualification", "cup"), "cup.firstSeasonQualification", out FirstSeasonQualification fsq);
                d.Cup = new CupDefinition
                {
                    Id = j.String(c, "id", "cup"), Name = j.String(c, "name", "cup"),
                    Clubs = j.Int(c, "clubs", "cup"), QualifiersPerDivision = j.Int(c, "qualifiersPerDivision", "cup"),
                    Home = home, FinalNeutral = j.Bool(c, "finalNeutral", "cup"), FirstSeasonQualification = fsq,
                };
                Check(d.Cup.Clubs >= 2 && (d.Cup.Clubs & (d.Cup.Clubs - 1)) == 0, "cup.clubs must be a power of two (single-match knockout).");
            }

            var mr = j.Object(root, "matchRules", "root");
            if (mr != null)
            {
                j.Keys(mr, "matchRules", "benchSize", "maxSubstitutions", "durationMinutes", "aiTactic");
                var ai = j.Object(mr, "aiTactic", "matchRules");
                if (ai != null) j.Keys(ai, "matchRules.aiTactic", "mentality", "defensiveLine", "pressure");
                d.MatchRules = new CompetitionMatchRules
                {
                    BenchSize = j.Int(mr, "benchSize", "matchRules"), MaxSubstitutions = j.Int(mr, "maxSubstitutions", "matchRules"),
                    DurationMinutes = j.Int(mr, "durationMinutes", "matchRules"),
                    AiMentality = ai == null ? 0 : j.Int(ai, "mentality", "matchRules.aiTactic"),
                    AiDefensiveLine = ai == null ? 0 : j.Int(ai, "defensiveLine", "matchRules.aiTactic"),
                    AiPressure = ai == null ? 0 : j.Int(ai, "pressure", "matchRules.aiTactic"),
                };
                Check(d.MatchRules.BenchSize >= 0 && d.MatchRules.MaxSubstitutions >= 0, "matchRules bench/subs must be >= 0.");
                Check(d.MatchRules.AiMentality >= 1 && d.MatchRules.AiMentality <= 5 && d.MatchRules.AiDefensiveLine >= 1 && d.MatchRules.AiDefensiveLine <= 3
                      && d.MatchRules.AiPressure >= 1 && d.MatchRules.AiPressure <= 3, "matchRules.aiTactic outside the MVP ranges.");
            }
            return j.Ok ? Result<CompetitionsDefinition>.Ok(d) : Result<CompetitionsDefinition>.Fail(j.Errors);
        }

        public static Result<CalendarDefinition> ReadCalendar(string json)
        {
            var j = new StrictJson(GameDataLoader.CalendarFile);
            var root = j.ParseRoot(json, SupportedSchemaVersion, "minDaysBetweenMatches", "leagueStart", "leagueIntervalDays",
                "cupRounds", "transferWindows", "seasonEnd");
            if (root == null) return Result<CalendarDefinition>.Fail(j.Errors);
            void Check(bool ok, string m) { if (!ok) j.Fail(InvalidCompetitions, m); }

            MonthWeekday ReadMw(Newtonsoft.Json.Linq.JToken t, string path)
            {
                var o = j.AsObject(t, path);
                if (o == null) return default;
                j.Keys(o, path, "month", "weekday");
                int month = j.Int(o, "month", path);
                j.TryEnum(j.String(o, "weekday", path), path + ".weekday", out DayOfWeek wd);
                Check(month >= 1 && month <= 12, path + ".month must be 1-12.");
                return new MonthWeekday(month, wd);
            }

            var d = new CalendarDefinition
            {
                MinDaysBetweenMatches = j.Int(root, "minDaysBetweenMatches", "root"),
                LeagueIntervalDays = j.Int(root, "leagueIntervalDays", "root"),
                LeagueStart = ReadMw(root["leagueStart"], "leagueStart"),
            };
            var rounds = new List<MonthWeekday>();
            var ra = j.Array(root, "cupRounds", "root");
            if (ra != null) for (int i = 0; i < ra.Count; i++) rounds.Add(ReadMw(ra[i], $"cupRounds[{i}]"));
            d.CupRounds = rounds;

            var windows = new List<MonthDayRange>();
            var wa = j.Array(root, "transferWindows", "root");
            if (wa != null)
                for (int i = 0; i < wa.Count; i++)
                {
                    string path = $"transferWindows[{i}]";
                    var o = j.AsObject(wa[i], path);
                    if (o == null) continue;
                    j.Keys(o, path, "openMonth", "openDay", "closeMonth", "closeDay");
                    windows.Add(new MonthDayRange(j.Int(o, "openMonth", path), j.Int(o, "openDay", path), j.Int(o, "closeMonth", path), j.Int(o, "closeDay", path)));
                }
            d.TransferWindows = windows;

            var end = j.Object(root, "seasonEnd", "root");
            if (end != null)
            {
                j.Keys(end, "seasonEnd", "month", "day");
                d.SeasonEndMonth = j.Int(end, "month", "seasonEnd");
                d.SeasonEndDay = j.Int(end, "day", "seasonEnd");
            }

            Check(d.MinDaysBetweenMatches >= 1, "minDaysBetweenMatches must be >= 1.");
            Check(d.LeagueIntervalDays >= d.MinDaysBetweenMatches, "leagueIntervalDays must be >= minDaysBetweenMatches.");
            for (int i = 1; i < rounds.Count; i++) Check(rounds[i].Month > rounds[i - 1].Month, "cupRounds must be in increasing months.");
            return j.Ok ? Result<CalendarDefinition>.Ok(d) : Result<CalendarDefinition>.Fail(j.Errors);
        }
    }
}
