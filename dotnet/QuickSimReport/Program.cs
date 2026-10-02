using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Career.Match;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Loading;
using Game.Rules.Match;
using Game.Rules.Ovr;

namespace Game.Tools.QuickSimReport
{
    /// <summary>
    /// Usage: dotnet run --project dotnet/QuickSimReport -- [--matches N] [--seed S]
    /// League-like batch: random home/away pairs inside the same division of generated worlds, random MVP tactics.
    /// Prints goals, 0x0, home/draw/away, fouls, cards, corners, shots, injuries, and the OVR-difference curve.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            int matches = 10000; ulong seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--matches") matches = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--seed") seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
            }
            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            var db = GameDataLoader.Load(new DirectoryDataSource(root));
            if (!db.IsSuccess) { Console.Error.WriteLine(db); return 1; }
            var sim = new Game.Simulation.QuickSim.QuickSim(db.Value);
            var rng = new Rng(seed);
            var worlds = Enumerable.Range(1, 5).Select(s => WorldGenerator.Generate(db.Value, (ulong)s)).ToArray();
            var formations = db.Value.Formations.Select(f => f.Id).ToArray();

            double goals = 0, zero = 0, home = 0, draw = 0, away = 0, fouls = 0, yellow = 0, red = 0, corners = 0, shots = 0, onTarget = 0, injuries = 0, subs = 0;
            var curve = new (int n, int w, int d, int l)[8];
            var sw = Stopwatch.StartNew();
            for (int m = 0; m < matches; m++)
            {
                var world = worlds[rng.NextInt(0, worlds.Length)];
                int div = rng.NextInt(0, world.DivisionNames.Count);
                var clubs = world.Clubs.Where(c => c.DivisionIndex == div).ToList();
                int a = rng.NextInt(0, clubs.Count), b = rng.NextInt(0, clubs.Count - 1); if (b >= a) b++;
                TacticSetup Tactic() => new TacticSetup(formations[rng.NextInt(0, formations.Length)], rng.NextInt(2, 5), rng.NextInt(1, 4), rng.NextInt(1, 4));
                var h = MatchSetupFactory.Team(world, sim.Rules, db.Value, clubs[a].Id, Tactic(), 9);
                var v = MatchSetupFactory.Team(world, sim.Rules, db.Value, clubs[b].Id, Tactic(), 9);
                var r = sim.Simulate(new MatchSetup(h, v, false, 6, 5, rng.NextULong()));

                goals += r.HomeGoals + r.AwayGoals;
                if (r.HomeGoals + r.AwayGoals == 0) zero++;
                if (r.HomeGoals > r.AwayGoals) home++; else if (r.HomeGoals == r.AwayGoals) draw++; else away++;
                foreach (var s in new[] { r.HomeStats, r.AwayStats })
                { fouls += s.Fouls; yellow += s.YellowCards; red += s.RedCards; corners += s.Corners; shots += s.Shots; onTarget += s.ShotsOnTarget; subs += s.Substitutions; }
                injuries += r.PlayerStats.Count(p => p.Injury != InjurySeverity.None);

                double diff = WorldValidator.SquadAverageOvr(world, db.Value, clubs[a].Id) - WorldValidator.SquadAverageOvr(world, db.Value, clubs[b].Id);
                int bucket = Math.Min(7, (int)(Math.Abs(diff) / 2));
                int sign = diff >= 0 ? 1 : -1;
                int res = Math.Sign(r.HomeGoals - r.AwayGoals) * sign;
                var c = curve[bucket];
                curve[bucket] = (c.n + 1, c.w + (res > 0 ? 1 : 0), c.d + (res == 0 ? 1 : 0), c.l + (res < 0 ? 1 : 0));
            }
            sw.Stop();
            double n = matches;
            Console.WriteLine($"{matches} matches in {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMilliseconds / n:0.000} ms/match incl. setup)");
            Console.WriteLine($"goals/match {goals / n:0.00} | 0x0 {100 * zero / n:0.0}% | home {100 * home / n:0.0}% draw {100 * draw / n:0.0}% away {100 * away / n:0.0}%");
            Console.WriteLine($"fouls {fouls / n:0.0} | yellow {yellow / n:0.00} | red {red / n:0.000} | corners {corners / n:0.0} | shots {shots / n:0.0} on target {onTarget / n:0.0} | injuries {injuries / n:0.00} | subs {subs / n:0.0}");
            Console.WriteLine("OVR diff (home-away, |d| bucket of 2) -> stronger side W/D/L %:");
            for (int i = 0; i < curve.Length; i++)
                if (curve[i].n > 0)
                    Console.WriteLine($"  {2 * i,2}-{2 * i + 2,2}: n={curve[i].n,5} W {100.0 * curve[i].w / curve[i].n,5:0.0} D {100.0 * curve[i].d / curve[i].n,5:0.0} L {100.0 * curve[i].l / curve[i].n,5:0.0}");
            return 0;
        }
    }
}
