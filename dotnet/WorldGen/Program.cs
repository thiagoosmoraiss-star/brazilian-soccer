using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Game.Career.World;
using Game.Data.Loading;
using Game.Rules.Ovr;

namespace Game.Tools.WorldGen
{
    /// <summary>
    /// Usage: dotnet run --project dotnet/WorldGen -- [--seed N] [--club SHORTNAME]
    /// Loads the repository-root Data/, generates the world, validates it and prints a summary
    /// (or one club's squad with --club).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            ulong seed = 1;
            string club = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seed" && i + 1 < args.Length) seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--club" && i + 1 < args.Length) club = args[++i].ToUpperInvariant();
                else { Console.Error.WriteLine("Usage: WorldGen [--seed N] [--club SHORTNAME]"); return 2; }
            }

            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            if (root == null) { Console.Error.WriteLine("Data/ not found."); return 1; }
            var db = GameDataLoader.Load(new DirectoryDataSource(root));
            if (!db.IsSuccess) { Console.Error.WriteLine(db.ToString()); return 1; }

            var world = WorldGenerator.Generate(db.Value, seed);
            var errors = WorldValidator.Validate(world, db.Value);
            Console.WriteLine($"Seed {seed} | {world.Clubs.Count} clubs | {world.Players.Count} players | {world.Contracts.Count} contracts | " +
                              $"validation: {(errors.Count == 0 ? "OK" : errors.Count + " errors")}");
            foreach (var e in errors.Take(20)) Console.WriteLine("  " + e);

            if (club != null) return PrintClub(world, db.Value, club);

            foreach (var div in world.DivisionNames)
            {
                var clubs = world.Clubs.Where(c => c.DivisionName == div)
                    .Select(c => (c, avg: WorldValidator.SquadAverageOvr(world, db.Value, c.Id)))
                    .OrderByDescending(x => x.avg).ToList();
                Console.WriteLine();
                Console.WriteLine($"Division {div} - squad OVR {clubs.Min(x => x.avg):0.0}..{clubs.Max(x => x.avg):0.0}");
                Console.WriteLine($"  {"",-3} {"Club",-44} {"UF",-2} {"OVR",5} {"Rep",4} {"*",1} {"Budget (R$)",13} {"Stad",4} {"Cap",6} {"Fans",9}  Colors / crest");
                foreach (var (c, avg) in clubs)
                    Console.WriteLine($"  {c.ShortName,-3} {Trim(c.Name, 44),-44} {c.Uf,-2} {avg,5:0.0} {c.Reputation,4} {c.Stars,1} {c.Budget,13:N0} {c.Stadium.Level,4} {c.Stadium.Capacity,6} {c.Fans,9:N0}  " +
                                      $"{c.Colors.PrimaryId}/{c.Colors.SecondaryId} {c.Crest.ShapeId}+{c.Crest.SymbolId}");
            }
            return errors.Count == 0 ? 0 : 1;
        }

        private static int PrintClub(WorldState world, GameDatabase db, string shortName)
        {
            var c = world.Clubs.FirstOrDefault(x => x.ShortName == shortName);
            if (c == null) { Console.Error.WriteLine("Club not found: " + shortName); return 1; }
            Console.WriteLine($"{c.Name} ({c.ShortName}) - {c.City}/{c.Uf} - Division {c.DivisionName} - squad OVR {WorldValidator.SquadAverageOvr(world, db, c.Id):0.0}");
            Console.WriteLine($"  {"Pos",-3} {"Name",-26} {"Age",3} {"OVR",3} {"Pot",3} {"Sec",-8} {"Cm",3} Ft WF  Attributes (Speed..GkHandling)");
            foreach (var p in world.SquadOf(c.Id))
            {
                int ovr = OvrCalculator.Rating(db.Ovr, p.AttributeSpan, p.MainPosition);
                Console.WriteLine($"  {p.MainPosition,-3} {Trim(p.Name, 26),-26} {world.AgeAtStart(p),3} {ovr,3} {p.Potential,3} {string.Join(",", p.SecondaryPositions),-8} {p.HeightCm,3} {(p.PreferredFoot == Foot.Left ? "L" : "R"),2} {p.WeakFoot,2}  {string.Join(" ", p.Attributes.Select(a => a.ToString("00")))}");
            }
            return 0;
        }

        private static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";
    }
}
