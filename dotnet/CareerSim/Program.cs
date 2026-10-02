using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Career.Season;
using Game.Data.Loading;

namespace Game.Tools.CareerSim
{
    /// <summary>
    /// Usage: dotnet run --project dotnet/CareerSim -- [--seed N] [--seasons N] [--tables]
    /// Runs a career headless (QuickSim for every match) and prints champions, promotions/relegations and cup results.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            ulong seed = 1; int seasons = 10; bool tables = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seed") seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--seasons") seasons = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--tables") tables = true;
            }
            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            var db = GameDataLoader.Load(new DirectoryDataSource(root));
            if (!db.IsSuccess) { Console.Error.WriteLine(db); return 1; }
            var quickSim = new Game.Simulation.QuickSim.QuickSim(db.Value);
            var career = CareerSimulator.Start(db.Value, seed, quickSim.Simulate);
            string Name(Game.Core.Ids.Id id) => career.State.Club(id).ShortName;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int s = 0; s < seasons; s++)
            {
                var season = career.State.Season;
                var summary = career.PlaySeason();
                Console.WriteLine($"{summary.Year}: champions {string.Join(" ", summary.FinalTables.Select((t, d) => $"{season.Leagues[d].DivisionName}={Name(t[0])}"))}" +
                                  $" | cup {Name(summary.CupWinner)} beat {Name(summary.CupRunnerUp)}" +
                                  $" | up {string.Join(",", summary.Promoted.Select(Name))} | down {string.Join(",", summary.Relegated.Select(Name))}");
                if (tables)
                    foreach (var l in season.Leagues)
                    {
                        var table = Standings.Table(db.Value.Competitions.League, l);
                        Console.WriteLine($"  Division {l.DivisionName}");
                        for (int i = 0; i < table.Count; i++)
                        {
                            var r = table[i];
                            Console.WriteLine($"   {i + 1,2}. {Name(r.ClubId),-3} {r.Points,3} pts  {r.Won,2}W {r.Drawn,2}D {r.Lost,2}L  {r.GoalsFor,3}:{r.GoalsAgainst,-3}");
                        }
                    }
            }
            Console.WriteLine($"{seasons} seasons in {sw.Elapsed.TotalSeconds:0.0} s");
            return 0;
        }
    }
}
