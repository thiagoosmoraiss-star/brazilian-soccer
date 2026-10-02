using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Loading;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Game.Tests.Career
{
    /// <summary>Loads the real GameDatabase and the acceptance ranges from Data/TestRanges/world.json.</summary>
    internal static class CareerTestData
    {
        private static GameDatabase _db;
        private static JObject _ranges;

        public static string DataRoot()
        {
            string root = Data.Loading.DataRoot.Find(Directory.GetCurrentDirectory())
                          ?? Data.Loading.DataRoot.Find(AppContext.BaseDirectory);
            if (root == null) throw new InvalidOperationException("Repository Data/ folder not found from the test run directory.");
            return root;
        }

        public static GameDatabase Db()
        {
            if (_db == null)
            {
                var result = GameDataLoader.Load(new DirectoryDataSource(DataRoot()));
                Assert.IsTrue(result.IsSuccess, result.ToString());
                _db = result.Value;
            }
            return _db;
        }

        private static JObject Ranges() =>
            _ranges ?? (_ranges = JObject.Parse(File.ReadAllText(Path.Combine(DataRoot(), "TestRanges", "world.json"))));

        public static (int Min, int Max) Range(string name)
        {
            var a = (JArray)Ranges()[name];
            return (a[0].Value<int>(), a[1].Value<int>());
        }

        public static (int Min, int Max) SquadOvrRange(string division)
        {
            var a = (JArray)Ranges()["squadOvrByDivision"][division];
            return (a[0].Value<int>(), a[1].Value<int>());
        }

        public static int Int(string name) => Ranges()[name].Value<int>();

        public static IReadOnlyList<int> Ints(string name) => Ranges()[name].Values<int>().ToList();

        public static IReadOnlyList<ulong> Seeds() => Ranges()["seeds"].Values<ulong>().ToList();
    }
}
