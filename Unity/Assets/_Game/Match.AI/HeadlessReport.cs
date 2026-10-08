using System;
using Game.Core.Contracts.Match;
using Game.Data.Loading;

namespace Game.Match.AI
{
    /// <summary>Aggregate of a batch of headless matches between a reference side A and a side B (A7a).</summary>
    public sealed class HeadlessReportResult
    {
        public int Matches;
        public int AWins, Draws, BWins;
        public double AGoals, BGoals;
        public double AShots, BShots;
        public double AOnTarget, BOnTarget;
        public double APossession;
        public double Saves, Corners;

        public double AWinRate => Matches == 0 ? 0 : (double)AWins / Matches;
        public double BWinRate => Matches == 0 ? 0 : (double)BWins / Matches;
        public double DrawRate => Matches == 0 ? 0 : (double)Draws / Matches;
        /// <summary>B's points per match (3 a win, 1 a draw).</summary>
        public double BPointsPerMatch => Matches == 0 ? 0 : (3.0 * BWins + Draws) / Matches;
        /// <summary>B's goal difference per match.</summary>
        public double BGoalDifference => Matches == 0 ? 0 : (BGoals - AGoals) / Matches;
        public double PerMatch(double total) => Matches == 0 ? 0 : total / Matches;
    }

    /// <summary>
    /// TECHNICAL_SPEC §19: "Headless: 200 partidas forte × fraco com relatório". Runs whole AI-vs-AI matches (the same
    /// MatchEngine the device renders) and sums their <see cref="MatchResult"/>s. Sides swap home and away every match;
    /// match i uses seed <c>seed + i</c>. Pure: the dotnet/MatchReport tool and the tests both call it.
    /// </summary>
    public static class HeadlessReport
    {
        public static HeadlessReportResult Run(GameDatabase db, MatchTeamSetup a, MatchTeamSetup b, int matches, int durationMinutes,
            ulong seed, float stepSeconds)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            var result = new HeadlessReportResult();
            for (int i = 0; i < matches; i++)
            {
                bool aHome = i % 2 == 0;
                var setup = new MatchSetup(aHome ? a : b, aHome ? b : a, false, durationMinutes, 5, seed + (ulong)i);
                var m = new AiMatch(db, setup, stepSeconds);
                m.RunToEnd();
                var r = m.Result();
                var sa = aHome ? MatchSide.Home : MatchSide.Away;
                var sb = aHome ? MatchSide.Away : MatchSide.Home;
                int ga = r.Goals(sa), gb = r.Goals(sb);
                result.Matches++;
                if (ga > gb) result.AWins++; else if (ga == gb) result.Draws++; else result.BWins++;
                result.AGoals += ga;
                result.BGoals += gb;
                result.AShots += r.Stats(sa).Shots;
                result.BShots += r.Stats(sb).Shots;
                result.AOnTarget += r.Stats(sa).ShotsOnTarget;
                result.BOnTarget += r.Stats(sb).ShotsOnTarget;
                result.APossession += r.Stats(sa).Possession;
                result.Saves += m.HomeStats.Saves + m.AwayStats.Saves;
                result.Corners += r.HomeStats.Corners + r.AwayStats.Corners;
            }
            return result;
        }
    }
}
