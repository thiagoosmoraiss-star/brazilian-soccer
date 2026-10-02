using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Career.Match;
using Game.Career.World;
using Game.Core.Contracts.Match;
using Game.Core.Random;
using Game.Data.Loading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>Shared fixtures: real GameDatabase, generated worlds, QuickSim, acceptance ranges (Data/TestRanges/quicksim.json).</summary>
    internal static class SimTestData
    {
        private static GameDatabase _db;
        private static Game.Simulation.QuickSim.QuickSim _sim;
        private static JObject _ranges;
        private static readonly Dictionary<ulong, WorldState> Worlds = new Dictionary<ulong, WorldState>();

        public static string DataRoot()
        {
            string root = Data.Loading.DataRoot.Find(Directory.GetCurrentDirectory()) ?? Data.Loading.DataRoot.Find(AppContext.BaseDirectory);
            if (root == null) throw new InvalidOperationException("Repository Data/ folder not found from the test run directory.");
            return root;
        }

        public static GameDatabase Db()
        {
            if (_db != null) return _db;
            var result = GameDataLoader.Load(new DirectoryDataSource(DataRoot()));
            Assert.IsTrue(result.IsSuccess, result.ToString());
            return _db = result.Value;
        }

        public static Game.Simulation.QuickSim.QuickSim Sim() => _sim ?? (_sim = new Game.Simulation.QuickSim.QuickSim(Db()));

        public static WorldState World(ulong seed)
        {
            if (!Worlds.TryGetValue(seed, out var w)) Worlds[seed] = w = WorldGenerator.Generate(Db(), seed);
            return w;
        }

        public static JObject Ranges() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(DataRoot(), "TestRanges", "quicksim.json"))));

        public static (double Min, double Max) Range(string name)
        {
            var a = (JArray)Ranges()[name];
            return (a[0].Value<double>(), a[1].Value<double>());
        }

        public static MatchTeamSetup Team(WorldState world, Club club, TacticSetup tactic) =>
            MatchSetupFactory.Team(world, Sim().Rules, Db(), club.Id, tactic, Ranges()["league"]["benchSize"].Value<int>());

        public static int MaxSubstitutions => Ranges()["league"]["maxSubstitutions"].Value<int>();

        /// <summary>One league-like match description, recorded for the OVR-curve analysis.</summary>
        public sealed class LeagueMatch
        {
            public MatchSetup Setup;
            public MatchResult Result;
            public double HomeSquadOvr, AwaySquadOvr;
        }

        private static List<LeagueMatch> _league;

        /// <summary>
        /// Deterministic league-like batch: random pairs inside one division of generated worlds, random MVP tactics,
        /// home/away. Generated once and shared by the batch tests.
        /// </summary>
        public static IReadOnlyList<LeagueMatch> League()
        {
            if (_league != null) return _league;
            var cfg = Ranges()["league"];
            var worlds = cfg["worldSeeds"].Values<ulong>().Select(World).ToArray();
            var rng = new Rng(cfg["batchSeed"].Value<ulong>());
            var formations = Db().Formations.Select(f => f.Id).ToArray();
            var ovr = new Dictionary<(ulong, Core.Ids.Id), double>();
            double SquadOvr(WorldState w, Club c)
            {
                if (!ovr.TryGetValue((w.Seed, c.Id), out var v)) ovr[(w.Seed, c.Id)] = v = WorldValidator.SquadAverageOvr(w, Db(), c.Id);
                return v;
            }

            var list = new List<LeagueMatch>();
            int n = cfg["matches"].Value<int>();
            for (int m = 0; m < n; m++)
            {
                var world = worlds[rng.NextInt(0, worlds.Length)];
                int div = rng.NextInt(0, world.DivisionNames.Count);
                var clubs = world.Clubs.Where(c => c.DivisionIndex == div).ToList();
                int a = rng.NextInt(0, clubs.Count), b = rng.NextInt(0, clubs.Count - 1);
                if (b >= a) b++;
                TacticSetup Tactic() => new TacticSetup(formations[rng.NextInt(0, formations.Length)], rng.NextInt(2, 5), rng.NextInt(1, 4), rng.NextInt(1, 4));
                var setup = new MatchSetup(Team(world, clubs[a], Tactic()), Team(world, clubs[b], Tactic()), false, 6, MaxSubstitutions, rng.NextULong());
                list.Add(new LeagueMatch
                {
                    Setup = setup,
                    Result = Sim().Simulate(setup),
                    HomeSquadOvr = SquadOvr(world, clubs[a]),
                    AwaySquadOvr = SquadOvr(world, clubs[b]),
                });
            }
            return _league = list;
        }
    }
}
