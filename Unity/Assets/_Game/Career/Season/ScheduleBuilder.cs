using System.Collections.Generic;
using Game.Core.Ids;
using Game.Core.Random;

namespace Game.Career.Season
{
    /// <summary>Round-robin schedules (circle method).</summary>
    public static class ScheduleBuilder
    {
        /// <summary>
        /// Double round-robin: first half by the circle method on a shuffled order, second half mirrored with home and
        /// away swapped. Every club plays every other club once at home and once away; n-1 rounds per half.
        /// </summary>
        public static List<List<(Id Home, Id Away)>> DoubleRoundRobin(IReadOnlyList<Id> clubs, Rng rng)
        {
            var teams = new List<Id>(clubs);
            for (int i = teams.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (teams[i], teams[j]) = (teams[j], teams[i]);
            }
            if (teams.Count % 2 == 1) teams.Add(Id.None); // bye

            int n = teams.Count;
            var first = new List<List<(Id, Id)>>();
            for (int r = 0; r < n - 1; r++)
            {
                var round = new List<(Id, Id)>();
                for (int i = 0; i < n / 2; i++)
                {
                    Id a = teams[i], b = teams[n - 1 - i];
                    if (a.IsNone || b.IsNone) continue;
                    // Alternate home/away so no club plays long home or away streaks.
                    bool aHome = i == 0 ? r % 2 == 0 : (i % 2 == 1) == (r % 2 == 0);
                    round.Add(aHome ? (a, b) : (b, a));
                }
                first.Add(round);
                // Rotate all but the first team.
                var last = teams[n - 1];
                teams.RemoveAt(n - 1);
                teams.Insert(1, last);
            }

            var all = new List<List<(Id, Id)>>(first);
            foreach (var round in first)
            {
                var mirrored = new List<(Id, Id)>();
                foreach (var (h, a) in round) mirrored.Add((a, h));
                all.Add(mirrored);
            }
            return all;
        }
    }
}
