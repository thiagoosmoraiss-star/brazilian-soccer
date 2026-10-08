using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Game.Data.Effects;
using Game.Data.Loading;
using Game.Match.AI;

namespace Game.Tools.MatchReport
{
    /// <summary>
    /// Usage: dotnet run --project dotnet/MatchReport -c Release -- [--matches N] [--minutes M] [--seed S] [--delta D]
    /// TECHNICAL_SPEC §19 (vertical slice): N headless matches strong × weak (Data/VerticalSlice/teams.json), then the
    /// same batch with the weak side's Velocidade (+Agilidade), Passe and Finalização raised by D: the weak side's
    /// results must move up ("mudar Velocidade/Passe/Finalização move o relatório na direção esperada").
    /// </summary>
    public static class Program
    {
        private const float StepSeconds = 1f / 50f;

        public static int Main(string[] args)
        {
            int matches = 200, minutes = 4, delta = 15;
            ulong seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--matches") matches = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--minutes") minutes = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--seed") seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--delta") delta = int.Parse(args[++i], CultureInfo.InvariantCulture);
            }
            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            var source = new DirectoryDataSource(root);
            var db = GameDataLoader.Load(source);
            if (!db.IsSuccess) { Console.Error.WriteLine(db); return 1; }
            var vs = GameDataLoader.LoadVerticalSlice(source, db.Value);
            if (!vs.IsSuccess) { Console.Error.WriteLine(vs); return 1; }
            var strong = vs.Value.Team("strong");
            var weak = vs.Value.Team("weak");

            Console.WriteLine($"Vertical slice headless report: {strong.Name} (strong) x {weak.Name} (weak), {matches} matches of {minutes} min, seed {seed}");
            var sw = Stopwatch.StartNew();
            var baseline = HeadlessReport.Run(db.Value, strong.ToSetup(), weak.ToSetup(), matches, minutes, seed, StepSeconds);
            Print("baseline", baseline);
            Console.WriteLine($"  time: {sw.ElapsedMilliseconds / 1000.0:F1} s ({sw.ElapsedMilliseconds / (double)matches:F0} ms/match)");

            Console.WriteLine();
            Console.WriteLine($"Sensitivity: weak side +{delta} (outfield players), same seeds");
            Console.WriteLine("  attribute            weak pts/match   weak goal diff   direction");
            Console.WriteLine($"  {"(baseline)",-20} {baseline.BPointsPerMatch,14:F2} {baseline.BGoalDifference,16:F2}");
            bool ok = true;
            ok &= Sensitivity(db.Value, strong, weak, "Velocidade+Agilidade", new[] { (Attr.Speed, delta), (Attr.Agility, delta) }, matches, minutes, seed, baseline);
            ok &= Sensitivity(db.Value, strong, weak, "Passe", new[] { (Attr.Passing, delta) }, matches, minutes, seed, baseline);
            ok &= Sensitivity(db.Value, strong, weak, "Finalização", new[] { (Attr.Finishing, delta) }, matches, minutes, seed, baseline);
            Console.WriteLine();
            Console.WriteLine(ok ? "All sensitivities move the weak side's results up." : "WARNING: some attribute did not move the results in the expected direction.");
            return 0;
        }

        private static bool Sensitivity(GameDatabase db, Game.Data.Match.VerticalSliceTeam strong, Game.Data.Match.VerticalSliceTeam weak,
            string label, (Attr, int)[] delta, int matches, int minutes, ulong seed, HeadlessReportResult baseline)
        {
            var r = HeadlessReport.Run(db, strong.ToSetup(), weak.ToSetup(delta), matches, minutes, seed, StepSeconds);
            bool up = r.BPointsPerMatch > baseline.BPointsPerMatch || r.BGoalDifference > baseline.BGoalDifference;
            Console.WriteLine($"  {label,-20} {r.BPointsPerMatch,14:F2} {r.BGoalDifference,16:F2}   {(up ? "up" : "NOT UP")}");
            return up;
        }

        private static void Print(string label, HeadlessReportResult r)
        {
            Console.WriteLine($"{label}:");
            Console.WriteLine($"  strong wins {r.AWins} ({r.AWinRate:P0}), draws {r.Draws} ({r.DrawRate:P0}), weak wins {r.BWins} ({r.BWinRate:P0})");
            Console.WriteLine($"  goals per match: strong {r.PerMatch(r.AGoals):F2}, weak {r.PerMatch(r.BGoals):F2}");
            Console.WriteLine($"  shots per match: strong {r.PerMatch(r.AShots):F1} ({r.PerMatch(r.AOnTarget):F1} on target), weak {r.PerMatch(r.BShots):F1} ({r.PerMatch(r.BOnTarget):F1})");
            Console.WriteLine($"  strong possession {r.PerMatch(r.APossession):F0}%, keeper saves {r.PerMatch(r.Saves):F1}, corners {r.PerMatch(r.Corners):F1} per match");
        }
    }
}
