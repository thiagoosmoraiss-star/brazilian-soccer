using System;
using System.Collections.Generic;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Competitions;

namespace Game.Career.Season
{
    /// <summary>
    /// League table with the GDD tiebreak order (points, then the configured tiebreakers: wins, goal difference, goals
    /// for, head-to-head, cards, draw). Ties are resolved group by group; head-to-head uses a mini-table among the tied
    /// clubs. The final "Draw" uses a seeded RNG, so the table is deterministic.
    /// </summary>
    public static class Standings
    {
        public static List<StandingRow> Rows(LeagueDefinition def, IReadOnlyList<Id> clubs, IEnumerable<Fixture> fixtures)
        {
            var rows = new Dictionary<Id, StandingRow>();
            foreach (var c in clubs) rows[c] = new StandingRow { ClubId = c };
            foreach (var f in fixtures)
            {
                if (!f.Played) continue;
                Apply(def, rows[f.HomeClubId], f.HomeGoals, f.AwayGoals, f.HomeYellows, f.HomeReds);
                Apply(def, rows[f.AwayClubId], f.AwayGoals, f.HomeGoals, f.AwayYellows, f.AwayReds);
            }
            var list = new List<StandingRow>();
            foreach (var c in clubs) list.Add(rows[c]);
            return list;
        }

        private static void Apply(LeagueDefinition def, StandingRow r, int gf, int ga, int yellows, int reds)
        {
            r.Played++;
            r.GoalsFor += gf;
            r.GoalsAgainst += ga;
            r.Yellows += yellows;
            r.Reds += reds;
            if (gf > ga) { r.Won++; r.Points += def.PointsWin; }
            else if (gf == ga) { r.Drawn++; r.Points += def.PointsDraw; }
            else r.Lost++;
        }

        /// <summary>Sorted table (first = champion).</summary>
        public static List<StandingRow> Table(LeagueDefinition def, LeagueEdition edition) =>
            Table(def, edition.ClubIds, edition.Fixtures, edition.TiebreakSeed);

        public static List<StandingRow> Table(LeagueDefinition def, IReadOnlyList<Id> clubs, IReadOnlyList<Fixture> fixtures, ulong tiebreakSeed)
        {
            var rows = Rows(def, clubs, fixtures);
            var rng = new Rng(tiebreakSeed);
            // Random keys for the final "Draw" tiebreaker, drawn in a fixed (club Id) order for determinism.
            var drawKey = new Dictionary<Id, ulong>();
            var ordered = new List<Id>(clubs);
            ordered.Sort();
            foreach (var c in ordered) drawKey[c] = rng.NextULong();

            var result = new List<StandingRow>(rows);
            result.Sort((a, b) => b.Points.CompareTo(a.Points));
            return Resolve(def, result, fixtures, drawKey);
        }

        /// <summary>Recursively orders groups of rows that are equal on everything compared so far.</summary>
        private static List<StandingRow> Resolve(LeagueDefinition def, List<StandingRow> sortedByPoints,
            IReadOnlyList<Fixture> fixtures, Dictionary<Id, ulong> drawKey)
        {
            var output = new List<StandingRow>();
            int i = 0;
            while (i < sortedByPoints.Count)
            {
                int j = i + 1;
                while (j < sortedByPoints.Count && sortedByPoints[j].Points == sortedByPoints[i].Points) j++;
                var group = sortedByPoints.GetRange(i, j - i);
                output.AddRange(group.Count == 1 ? group : Break(def, group, 0, fixtures, drawKey));
                i = j;
            }
            return output;
        }

        private static List<StandingRow> Break(LeagueDefinition def, List<StandingRow> group, int criterion,
            IReadOnlyList<Fixture> fixtures, Dictionary<Id, ulong> drawKey)
        {
            if (group.Count <= 1 || criterion >= def.Tiebreakers.Count) return group;
            var tb = def.Tiebreakers[criterion];
            var keys = new Dictionary<Id, long>();
            Dictionary<Id, (int pts, int gd, int gf)> h2h = tb == Tiebreaker.HeadToHead ? HeadToHead(def, group, fixtures) : null;
            foreach (var r in group)
            {
                long key;
                switch (tb)
                {
                    case Tiebreaker.Wins: key = r.Won; break;
                    case Tiebreaker.GoalDifference: key = r.GoalDifference; break;
                    case Tiebreaker.GoalsFor: key = r.GoalsFor; break;
                    // Head-to-head: points, then goal difference, then goals among the tied clubs (higher is better).
                    case Tiebreaker.HeadToHead: var h = h2h[r.ClubId]; key = h.pts * 1_000_000L + (h.gd + 1000) * 1_000L + h.gf; break;
                    // Fewer weighted cards is better.
                    case Tiebreaker.Cards: key = -(r.Yellows * (long)def.YellowCardWeight + r.Reds * (long)def.RedCardWeight); break;
                    case Tiebreaker.Draw: key = (long)(drawKey[r.ClubId] >> 1); break;
                    default: throw new InvalidOperationException("Unknown tiebreaker " + tb);
                }
                keys[r.ClubId] = key;
            }
            group.Sort((a, b) => keys[b.ClubId].CompareTo(keys[a.ClubId]));

            var output = new List<StandingRow>();
            int i = 0;
            while (i < group.Count)
            {
                int j = i + 1;
                while (j < group.Count && keys[group[j].ClubId] == keys[group[i].ClubId]) j++;
                var sub = group.GetRange(i, j - i);
                output.AddRange(sub.Count == 1 ? sub : Break(def, sub, criterion + 1, fixtures, drawKey));
                i = j;
            }
            return output;
        }

        private static Dictionary<Id, (int pts, int gd, int gf)> HeadToHead(LeagueDefinition def, List<StandingRow> group, IReadOnlyList<Fixture> fixtures)
        {
            var members = new HashSet<Id>();
            foreach (var r in group) members.Add(r.ClubId);
            var t = new Dictionary<Id, (int pts, int gd, int gf)>();
            foreach (var id in members) t[id] = (0, 0, 0);
            foreach (var f in fixtures)
            {
                if (!f.Played || !members.Contains(f.HomeClubId) || !members.Contains(f.AwayClubId)) continue;
                var h = t[f.HomeClubId]; var a = t[f.AwayClubId];
                int hp = f.HomeGoals > f.AwayGoals ? def.PointsWin : f.HomeGoals == f.AwayGoals ? def.PointsDraw : 0;
                int ap = f.AwayGoals > f.HomeGoals ? def.PointsWin : f.HomeGoals == f.AwayGoals ? def.PointsDraw : 0;
                t[f.HomeClubId] = (h.pts + hp, h.gd + f.HomeGoals - f.AwayGoals, h.gf + f.HomeGoals);
                t[f.AwayClubId] = (a.pts + ap, a.gd + f.AwayGoals - f.HomeGoals, a.gf + f.AwayGoals);
            }
            return t;
        }
    }
}
