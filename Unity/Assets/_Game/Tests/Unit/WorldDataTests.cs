using System;
using System.IO;
using System.Linq;
using Game.Data.Loading;
using Game.Data.World;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>Data/World/*.json: real files load and validate; broken copies are rejected.</summary>
    public class WorldDataTests
    {
        private static readonly string[] Files =
        {
            GameDataLoader.NamesFile, GameDataLoader.CitiesFile, GameDataLoader.ClubTemplatesFile,
            GameDataLoader.CrestTemplatesFile, GameDataLoader.GenerationFile,
        };

        [Test]
        public void RealWorldData_LoadsAndValidates()
        {
            var world = GameDataLoader.LoadWorld(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(world.IsSuccess, world.ToString());
            Assert.AreEqual(4, world.Value.Generation.Divisions.Count);
            Assert.AreEqual(16, world.Value.Generation.ClubsPerDivision);
            Assert.GreaterOrEqual(world.Value.Cities.Count, 64);
        }

        [Test]
        public void RealGameDatabase_ExposesWorldAndOvr()
        {
            var db = GameDataLoader.Load(new DirectoryDataSource(TestPaths.DataRoot()));
            Assert.IsTrue(db.IsSuccess, db.ToString());
            Assert.IsNotNull(db.Value.World);
            Assert.IsNotNull(db.Value.Ovr);
        }

        private static Game.Core.Results.Result<WorldDefinition> LoadWithEdit(string file, Func<string, string> edit)
        {
            string dir = Path.Combine(Path.GetTempPath(), "world-data-" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var f in Files)
                {
                    string target = Path.Combine(dir, f.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string text = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), f.Replace('/', Path.DirectorySeparatorChar)));
                    File.WriteAllText(target, f == file ? edit(text) : text);
                }
                return GameDataLoader.LoadWorld(new DirectoryDataSource(dir));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        private static void AssertRejected(string file, Func<string, string> edit, string expectedFragment)
        {
            var result = LoadWithEdit(file, edit);
            Assert.IsFalse(result.IsSuccess, "Edited data should be rejected.");
            Assert.IsTrue(result.Errors.Any(e => e.Message.Contains(expectedFragment)), result.ToString());
        }

        [Test]
        public void TooFewCities_IsRejected() =>
            AssertRejected(GameDataLoader.GenerationFile, t => t.Replace("\"clubsPerDivision\": 16", "\"clubsPerDivision\": 40"), "one city per club");

        [Test]
        public void InvalidUf_IsRejected() =>
            AssertRejected(GameDataLoader.CitiesFile, t => t.Replace("\"uf\": \"SP\"", "\"uf\": \"XX\""), "invalid UF");

        [Test]
        public void NamePatternWithoutCity_IsRejected() =>
            AssertRejected(GameDataLoader.ClubTemplatesFile, t => t.Replace("Esporte Clube {city}", "Esporte Clube"), "{city}");

        [Test]
        public void InvalidColor_IsRejected() =>
            AssertRejected(GameDataLoader.ClubTemplatesFile, t => t.Replace("#C8102E", "red"), "#RRGGBB");

        [Test]
        public void UnknownProperty_IsRejected() =>
            AssertRejected(GameDataLoader.NamesFile, t => t.Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"extra\": true,"), "unknown property");

        [Test]
        public void MissingPositionInPerPositionTable_IsRejected() =>
            AssertRejected(GameDataLoader.GenerationFile, t => t.Replace("\"GOL\": [184, 198],", ""), "missing position GOL");

        [Test]
        public void ReversedRange_IsRejected() =>
            AssertRejected(GameDataLoader.GenerationFile, t => t.Replace("\"squadOvrMean\": [46, 54]", "\"squadOvrMean\": [54, 46]"), "squadOvrMean");

        [Test]
        public void SquadWithoutGoalkeeper_IsRejected() =>
            AssertRejected(GameDataLoader.GenerationFile, t => t.Replace("\"position\": \"GOL\"", "\"position\": \"ZAG\""), "no slot for GOL");

        [Test]
        public void InvalidJson_IsRejected() =>
            AssertRejected(GameDataLoader.CitiesFile, t => t.Substring(0, t.Length / 2), "cities.json");
    }
}
