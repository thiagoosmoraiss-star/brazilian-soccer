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
    /// Usage: dotnet run --project dotnet/MatchReport -c Release -- [--matches N] [--minutes M] [--seed S] [--delta D] [--baseline-only]
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
            bool quick = false;
            int penaltyTrials = 0;
            ulong seed = 1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--matches") matches = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--minutes") minutes = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--seed") seed = ulong.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--delta") delta = int.Parse(args[++i], CultureInfo.InvariantCulture);
                else if (args[i] == "--baseline-only") quick = true;
                else if (args[i] == "--penalties") penaltyTrials = int.Parse(args[++i], CultureInfo.InvariantCulture);
            }
            string root = DataRoot.Find(Directory.GetCurrentDirectory()) ?? DataRoot.Find(AppContext.BaseDirectory);
            var source = new DirectoryDataSource(root);
            var db = GameDataLoader.Load(source);
            if (!db.IsSuccess) { Console.Error.WriteLine(db); return 1; }
            var vs = GameDataLoader.LoadVerticalSlice(source, db.Value);
            if (!vs.IsSuccess) { Console.Error.WriteLine(vs); return 1; }
            var strong = vs.Value.Team("strong");
            var weak = vs.Value.Team("weak");

            if (penaltyTrials > 0) { Penalties(db.Value, strong, weak, penaltyTrials, seed); return 0; }
            Console.WriteLine($"Vertical slice headless report: {strong.Name} (strong) x {weak.Name} (weak), {matches} matches of {minutes} min, seed {seed}");
            var sw = Stopwatch.StartNew();
            var baseline = HeadlessReport.Run(db.Value, strong.ToSetup(), weak.ToSetup(), matches, minutes, seed, StepSeconds);
            Print("baseline", baseline);
            Console.WriteLine($"  time: {sw.ElapsedMilliseconds / 1000.0:F1} s ({sw.ElapsedMilliseconds / (double)matches:F0} ms/match)");
            if (quick) return 0;

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

        /// <summary>--penalties N: N isolated penalties (strong side shooting), with their outcome.</summary>
        private static void Penalties(GameDatabase db, Game.Data.Match.VerticalSliceTeam strong, Game.Data.Match.VerticalSliceTeam weak, int n, ulong seed)
        {
            int goals = 0, saves = 0, wide = 0, other = 0, dives = 0, correct = 0;
            for (int i = 0; i < n; i++)
            {
                var m = new AiMatch(db, new Game.Core.Contracts.Match.MatchSetup(strong.ToSetup(), weak.ToSetup(), false, 6, 5, seed + (ulong)i), StepSeconds);
                m.CancelRestart();
                m.AwardPenalty(Game.Core.Contracts.Match.MatchSide.Home);
                var outcome = AiMatchEvent.None;
                bool shot = false;
                float shotY = 0f;
                for (int s = 0; s < 400 && outcome == AiMatchEvent.None; s++)
                {
                    var ev = m.Step();
                    if (ev == AiMatchEvent.Shot) { shot = true; continue; }
                    if (!shot) continue;
                    if (shotY == 0f && m.Ball.Position.X > 40f) shotY = m.Ball.Position.Y;
                    if (ev == AiMatchEvent.Goal || ev == AiMatchEvent.Save || ev == AiMatchEvent.Out) outcome = ev;
                }
                var k = m.AwayKeeper;
                if (k.Dove) { dives++; if (MathF.Sign(k.Target.Y) == MathF.Sign(shotY)) correct++; }
                if (outcome == AiMatchEvent.Goal) goals++;
                else if (outcome == AiMatchEvent.Save) saves++;
                else if (outcome == AiMatchEvent.Out) wide++;
                else other++;
            }
            Console.WriteLine($"Penalties: {n}, goals {goals} ({(double)goals / n:P0}), saves {saves}, out {wide}, unresolved {other}; keeper dived {dives} (right side {correct})");
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
            Console.WriteLine($"  referee per match: fouls {r.PerMatch(r.Fouls):F1}, yellow {r.PerMatch(r.Yellows):F2}, red {r.PerMatch(r.Reds):F2}, "
                              + $"offsides {r.PerMatch(r.Offsides):F1}, free kicks {r.PerMatch(r.FreeKicks):F1}, penalties {r.PerMatch(r.Penalties):F2} "
                              + $"(scored {(r.Penalties > 0 ? r.PenaltyGoals / r.Penalties : 0):P0})");
            Console.WriteLine($"  high ball per match: crosses {r.PerMatch(r.Crosses):F1}, long balls {r.PerMatch(r.LongBalls):F1}, "
                              + $"headers {r.PerMatch(r.Headers):F1} ({r.PerMatch(r.HeaderShots):F2} shots, {r.PerMatch(r.HeaderGoals):F2} goals), "
                              + $"aerial duels {r.PerMatch(r.AerialDuels):F1}");
            Console.WriteLine($"  duels per match: slides {r.PerMatch(r.Slides):F1} (won {r.PerMatch(r.SlidesWon):F1}, fouls {r.PerMatch(r.SlideFouls):F2}), "
                              + $"body duels {r.PerMatch(r.BodyDuels):F1}, runs {r.PerMatch(r.Runs):F1}, through balls {r.PerMatch(r.ThroughBalls):F1}, "
                              + $"keeper rushes {r.PerMatch(r.KeeperRushes):F1} (smothers {r.PerMatch(r.Smothers):F2})");
            Console.WriteLine($"  substitutions per match: {r.PerMatch(r.Substitutions):F1} (both sides; most by one side in a match {r.MaxSubstitutionsInAMatch})");
        }
    }
}
