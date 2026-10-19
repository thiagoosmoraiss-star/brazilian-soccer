using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Career.Match;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Ids;
using Game.Core.Random;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match.AI;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// C2 (TEST_PLAN "Divergência QuickSim × Headless"): the same matchups — same <see cref="MatchSetup"/>, same seed —
    /// resolved by the QuickSim and by the played match (AI × AI, headless), and the TEST_PLAN metrics of both.
    /// Matchups come from generated worlds with the career's own lineup AI (<see cref="MatchSetupFactory"/>), the AI
    /// tactic and the match duration of competitions.json. No NUnit here: the tests and dotnet/MatchReport share it.
    /// Matches run in parallel; results are stored by index, so a batch is deterministic.
    /// </summary>
    public static class Calibration
    {
        public const float StepSeconds = 1f / 50f;

        public sealed class Matchup
        {
            public MatchSetup Setup;
            public double HomeOvr, AwayOvr;
            public double Diff => HomeOvr - AwayOvr;
        }

        /// <summary>
        /// <paramref name="count"/> matchups: a random club of a random division against another club of the same
        /// division, or (with <paramref name="crossDivisionShare"/>) of the division right above or below; on a
        /// neutral venue when <paramref name="neutralVenue"/> (the played match has no home advantage, X-85).
        /// </summary>
        public static List<Matchup> Matchups(GameDatabase db, IReadOnlyList<WorldState> worlds, int count, ulong batchSeed,
            double crossDivisionShare, bool neutralVenue)
        {
            var sim = new Game.Simulation.QuickSim.QuickSim(db);
            var mr = db.Competitions.MatchRules;
            var rng = new Rng(batchSeed);
            var formations = db.Formations.Select(f => f.Id).ToArray();
            var ovr = new Dictionary<(ulong, Id), double>();
            double SquadOvr(WorldState w, Id club)
            {
                if (!ovr.TryGetValue((w.Seed, club), out var v)) ovr[(w.Seed, club)] = v = WorldValidator.SquadAverageOvr(w, db, club);
                return v;
            }

            var list = new List<Matchup>(count);
            for (int m = 0; m < count; m++)
            {
                var world = worlds[rng.NextInt(0, worlds.Count)];
                int divisions = world.DivisionNames.Count;
                int div = rng.NextInt(0, divisions);
                int otherDiv = div;
                if (rng.NextDouble() < crossDivisionShare)
                    otherDiv = div == 0 ? 1 : div == divisions - 1 ? div - 1 : div + (rng.NextInt(0, 2) == 0 ? -1 : 1);
                var home = world.Clubs.Where(c => c.DivisionIndex == div).ToList();
                var away = world.Clubs.Where(c => c.DivisionIndex == otherDiv).ToList();
                var h = home[rng.NextInt(0, home.Count)];
                Club a;
                do a = away[rng.NextInt(0, away.Count)]; while (a.Id == h.Id);
                TacticSetup Tactic() => new TacticSetup(formations[rng.NextInt(0, formations.Length)], mr.AiMentality, mr.AiDefensiveLine, mr.AiPressure);
                var setup = new MatchSetup(
                    MatchSetupFactory.Team(world, sim.Rules, db, h.Id, Tactic(), mr.BenchSize),
                    MatchSetupFactory.Team(world, sim.Rules, db, a.Id, Tactic(), mr.BenchSize),
                    neutralVenue, mr.DurationMinutes, mr.MaxSubstitutions, rng.NextULong());
                list.Add(new Matchup { Setup = setup, HomeOvr = SquadOvr(world, h.Id), AwayOvr = SquadOvr(world, a.Id) });
            }
            return list;
        }

        public static MatchResult[] QuickSim(GameDatabase db, IReadOnlyList<MatchSetup> setups)
        {
            var sim = new Game.Simulation.QuickSim.QuickSim(db);
            var results = new MatchResult[setups.Count];
            Parallel.For(0, setups.Count, i => results[i] = sim.Simulate(setups[i]));
            return results;
        }

        public static MatchResult[] Played(GameDatabase db, IReadOnlyList<MatchSetup> setups)
        {
            var results = new MatchResult[setups.Count];
            Parallel.For(0, setups.Count, i =>
            {
                var m = new AiMatch(db, setups[i], StepSeconds);
                m.RunToEnd();
                results[i] = m.Result();
            });
            return results;
        }

        public static List<MatchSetup> Setups(IEnumerable<Matchup> matchups) => matchups.Select(m => m.Setup).ToList();

        // ---------------- Metrics ----------------

        /// <summary>One |OVR difference| bucket, from the stronger side's point of view.</summary>
        public sealed class Bucket
        {
            public double From, To;
            public int N, Wins, Draws, Losses;
            public double StrongerPossession, StrongerGoals, WeakerGoals;
            public double WinPct => N == 0 ? 0 : 100.0 * Wins / N;
            public double DrawPct => N == 0 ? 0 : 100.0 * Draws / N;
            public double LossPct => N == 0 ? 0 : 100.0 * Losses / N;
            /// <summary>Stronger side's wins among the matches with a winner: the shape of the curve without the draws,
            /// which depend on how many goals an engine produces.</summary>
            public double DecidedWinPct => Wins + Losses == 0 ? 0 : 100.0 * Wins / (Wins + Losses);
            public double Possession => N == 0 ? 0 : StrongerPossession / N;
        }

        public sealed class Metrics
        {
            public int Matches;
            public readonly Dictionary<string, double> Values = new Dictionary<string, double>();
            public readonly List<Bucket> Buckets = new List<Bucket>();
            public double this[string name] => Values[name];
        }

        /// <summary>Names of the per-match metrics, in report order (TEST_PLAN list).</summary>
        public static readonly string[] MetricNames =
        {
            "goalsPerMatch", "zeroZeroPct", "shotsPerMatch", "shotsOnTargetPerMatch", "onTargetPct", "goalsPerShot",
            "cornersPerMatch", "foulsPerMatch", "yellowPerMatch", "redPerMatch", "injuriesPerMatch",
            "homeWinPct", "drawPct", "awayWinPct", "strongerWinPct", "strongerDecidedWinPct", "strongerGoalShare", "possessionPerOvrPoint",
        };

        public static Metrics Measure(IReadOnlyList<Matchup> matchups, IReadOnlyList<MatchResult> results, double bucketWidth, int bucketCount)
        {
            var m = new Metrics { Matches = results.Count };
            for (int b = 0; b < bucketCount; b++)
                m.Buckets.Add(new Bucket { From = b * bucketWidth, To = b == bucketCount - 1 ? double.PositiveInfinity : (b + 1) * bucketWidth });
            double goals = 0, zero = 0, shots = 0, onTarget = 0, corners = 0, fouls = 0, yellow = 0, red = 0, injuries = 0;
            double home = 0, draw = 0, away = 0, strongerWins = 0, strongerLosses = 0, strongerGoals = 0, decided = 0;
            // Least squares (through the origin) of the stronger side's possession above 50% against |OVR difference|.
            double sxy = 0, sxx = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var mu = matchups[i];
                int total = r.HomeGoals + r.AwayGoals;
                goals += total;
                if (total == 0) zero++;
                foreach (var s in new[] { r.HomeStats, r.AwayStats })
                {
                    shots += s.Shots; onTarget += s.ShotsOnTarget; corners += s.Corners; fouls += s.Fouls;
                    yellow += s.YellowCards; red += s.RedCards;
                }
                foreach (var p in r.PlayerStats) if (p.Injury != InjurySeverity.None) injuries++;
                if (r.HomeGoals > r.AwayGoals) home++; else if (r.HomeGoals == r.AwayGoals) draw++; else away++;

                var stronger = mu.Diff >= 0 ? MatchSide.Home : MatchSide.Away;
                var weaker = stronger == MatchSide.Home ? MatchSide.Away : MatchSide.Home;
                int gs = r.Goals(stronger), gw = r.Goals(weaker);
                if (gs > gw) strongerWins++; else if (gs < gw) strongerLosses++;
                strongerGoals += gs;
                decided += gs + gw;
                double abs = Math.Abs(mu.Diff);
                double possession = r.Stats(stronger).Possession;
                sxy += abs * (possession - 50.0);
                sxx += abs * abs;

                var bucket = m.Buckets[Math.Min(bucketCount - 1, (int)(abs / bucketWidth))];
                bucket.N++;
                if (gs > gw) bucket.Wins++; else if (gs == gw) bucket.Draws++; else bucket.Losses++;
                bucket.StrongerPossession += possession;
                bucket.StrongerGoals += gs;
                bucket.WeakerGoals += gw;
            }
            double n = Math.Max(1, results.Count);
            m.Values["goalsPerMatch"] = goals / n;
            m.Values["zeroZeroPct"] = 100 * zero / n;
            m.Values["shotsPerMatch"] = shots / n;
            m.Values["shotsOnTargetPerMatch"] = onTarget / n;
            m.Values["onTargetPct"] = shots == 0 ? 0 : 100 * onTarget / shots;
            m.Values["goalsPerShot"] = shots == 0 ? 0 : goals / shots;
            m.Values["cornersPerMatch"] = corners / n;
            m.Values["foulsPerMatch"] = fouls / n;
            m.Values["yellowPerMatch"] = yellow / n;
            m.Values["redPerMatch"] = red / n;
            m.Values["injuriesPerMatch"] = injuries / n;
            m.Values["homeWinPct"] = 100 * home / n;
            m.Values["drawPct"] = 100 * draw / n;
            m.Values["awayWinPct"] = 100 * away / n;
            m.Values["strongerWinPct"] = 100 * strongerWins / n;
            m.Values["strongerDecidedWinPct"] = strongerWins + strongerLosses == 0 ? 0 : 100 * strongerWins / (strongerWins + strongerLosses);
            m.Values["strongerGoalShare"] = decided == 0 ? 0 : 100 * strongerGoals / decided;
            m.Values["possessionPerOvrPoint"] = sxx == 0 ? 0 : sxy / sxx;
            return m;
        }

        // ---------------- Attribute sensitivity ----------------

        /// <summary>Paired result of one attribute boost in one engine: mean change of the boosted side's goal difference.</summary>
        public readonly struct Sensitivity
        {
            public readonly double Mean, Z;
            public readonly int N;
            public Sensitivity(double mean, double z, int n) { Mean = mean; Z = z; N = n; }
        }

        /// <summary>
        /// The home side of every matchup with <paramref name="attr"/> +<paramref name="boost"/> (all its players)
        /// against the same matchup unchanged, same seed (common random numbers): mean change of the home goal
        /// difference (or another metric) and its z. <paramref name="resolve"/> is one of the two engines.
        /// </summary>
        public static Sensitivity Measure(Func<IReadOnlyList<MatchSetup>, MatchResult[]> resolve, IReadOnlyList<MatchSetup> setups,
            IReadOnlyList<MatchResult> baseline, Attr attr, int boost) =>
            Measure(resolve, setups, baseline, new[] { attr }, boost);

        /// <summary>As above with several attributes raised together (e.g. all of them: a much better side).</summary>
        public static Sensitivity Measure(Func<IReadOnlyList<MatchSetup>, MatchResult[]> resolve, IReadOnlyList<MatchSetup> setups,
            IReadOnlyList<MatchResult> baseline, IReadOnlyList<Attr> attrs, int boost) =>
            Measure(resolve, setups, baseline, attrs, boost, GoalDifference);

        /// <summary>Home goal difference: the default sensitivity metric.</summary>
        public static double GoalDifference(MatchSetup setup, MatchResult r) => r.HomeGoals - r.AwayGoals;

        /// <summary>Average final energy of the home starters (Resistência acts on energy first, as quicksim.json).</summary>
        public static double HomeStartersFinalEnergy(MatchSetup setup, MatchResult r)
        {
            double sum = 0;
            int n = 0;
            foreach (var p in r.PlayerStats)
                if (p.Side == MatchSide.Home && p.Started) { sum += p.FinalEnergy; n++; }
            return n == 0 ? 0 : sum / n;
        }

        /// <summary>Sensitivity on any per-match metric of the home side (higher = better).</summary>
        public static Sensitivity Measure(Func<IReadOnlyList<MatchSetup>, MatchResult[]> resolve, IReadOnlyList<MatchSetup> setups,
            IReadOnlyList<MatchResult> baseline, IReadOnlyList<Attr> attrs, int boost, Func<MatchSetup, MatchResult, double> metric)
        {
            var boosted = setups.Select(s => new MatchSetup(Boost(s.Home, attrs, boost), s.Away, s.NeutralVenue, s.DurationMinutes,
                s.MaxSubstitutions, s.Seed, s.RequiresWinner)).ToList();
            var results = resolve(boosted);
            int n = setups.Count;
            var deltas = new double[n];
            for (int i = 0; i < n; i++)
                deltas[i] = metric(boosted[i], results[i]) - metric(setups[i], baseline[i]);
            double mean = deltas.Average();
            double sd = n < 2 ? 0 : Math.Sqrt(deltas.Sum(d => (d - mean) * (d - mean)) / (n - 1));
            double z = sd == 0 ? (mean > 0 ? double.PositiveInfinity : mean < 0 ? double.NegativeInfinity : 0) : mean / (sd / Math.Sqrt(n));
            return new Sensitivity(mean, z, n);
        }

        public static MatchTeamSetup Boost(MatchTeamSetup team, IReadOnlyList<Attr> raised, int boost)
        {
            MatchPlayerSetup B(MatchPlayerSetup p)
            {
                var attrs = p.Attributes.ToArray();
                foreach (var attr in raised) attrs[(int)attr] = Math.Min(AttrInfo.MaxValue, attrs[(int)attr] + boost);
                return new MatchPlayerSetup(p.PlayerId, attrs, p.MainPosition, p.SecondaryPositions.ToArray(), p.Energy, p.Morale, p.Form,
                    p.LeftFooted, p.WeakFoot);
            }
            return new MatchTeamSetup(team.ClubId, team.Tactic, team.Starters.Select(B).ToList(), team.Bench.Select(B).ToList());
        }
    }
}
