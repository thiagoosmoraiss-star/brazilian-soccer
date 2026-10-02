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
            ulong seed = 1; int seasons = 10; bool tables = false, stats = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seed") seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--seasons") seasons = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--tables") tables = true;
                else if (args[i] == "--stats") stats = true;
            }
            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            var db = GameDataLoader.Load(new DirectoryDataSource(root));
            if (!db.IsSuccess) { Console.Error.WriteLine(db); return 1; }
            var quickSim = new Game.Simulation.QuickSim.QuickSim(db.Value);
            var career = CareerSimulator.Start(db.Value, seed, quickSim.Simulate);
            string Name(Game.Core.Ids.Id id) => career.State.Club(id).ShortName;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (stats) PrintStats(db.Value, career, "start");
            for (int s = 0; s < seasons; s++)
            {
                var season = career.State.Season;
                var summary = career.PlaySeason();
                Console.WriteLine($"{summary.Year}: champions {string.Join(" ", summary.FinalTables.Select((t, d) => $"{season.Leagues[d].DivisionName}={Name(t[0])}"))}" +
                                  $" | cup {Name(summary.CupWinner)} beat {Name(summary.CupRunnerUp)}" +
                                  $" | up {string.Join(",", summary.Promoted.Select(Name))} | down {string.Join(",", summary.Relegated.Select(Name))}");
                if (stats) PrintStats(db.Value, career, summary.Year.ToString(CultureInfo.InvariantCulture) + $" (retired {summary.Retired.Count})");
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

        private static void PrintStats(GameDatabase db, CareerSimulator career, string label)
        {
            var w = career.State.World;
            int year = career.State.Season.Year;
            var parts = w.DivisionNames.Select((name, d) =>
            {
                var clubs = w.Clubs.Where(c => c.DivisionIndex == d).ToList();
                double ovr = clubs.Average(c => w.SquadOf(c.Id)
                    .Select(p => Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition))
                    .OrderByDescending(x => x).Take(22).Average());
                return $"{name} {ovr:0.0}";
            });
            var players = w.Players;
            int above = players.Count(p => Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition) > p.Potential);
            double age = players.Average(p => p.BirthDate.AgeOn(year, 1, 1));
            int maxOvr = players.Max(p => Game.Rules.Ovr.OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition));
            double squad = w.Clubs.Average(c => w.SquadOf(c.Id).Count);
            Console.WriteLine($"  [{label}] top-22 OVR {string.Join(" | ", parts)} | players {players.Count} (squad {squad:0.0}) | age {age:0.0} | max OVR {maxOvr} | above potential {above}");
        }
    }
}
