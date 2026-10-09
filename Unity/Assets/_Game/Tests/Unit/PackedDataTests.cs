using System.IO;
using System.Linq;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>A7b: the player build reads Data/ from a generated pack (D-09) — the same database as the folder.</summary>
    public class PackedDataTests
    {
        [Test]
        public void PackedData_LoadsTheSameDatabase_AsTheFolder()
        {
            var packed = PackedDataSource.FromPack(PackedDataSource.Pack(TestPaths.DataRoot()), "test pack");
            var db = GameDataLoader.Load(packed);
            Assert.IsTrue(db.IsSuccess, db.ToString());
            Assert.IsTrue(GameDataLoader.LoadVerticalSlice(packed, db.Value).IsSuccess);
            Assert.IsTrue(GameDataLoader.LoadMatchView(packed).IsSuccess);
            var folder = new DirectoryDataSource(TestPaths.DataRoot());
            foreach (var path in packed.Paths)
                Assert.AreEqual(folder.ReadAllText(path), packed.ReadAllText(path), path);
            int jsonFiles = Directory.GetFiles(TestPaths.DataRoot(), "*.json", SearchOption.AllDirectories).Length;
            Assert.AreEqual(jsonFiles, packed.Paths.Count());
        }

        [Test]
        public void RealMatchViewFile_Loads() =>
            Assert.IsTrue(GameDataLoader.LoadMatchView(new DirectoryDataSource(TestPaths.DataRoot())).IsSuccess);

        [Test]
        public void MatchView_RejectsANearCameraFurtherThanTheFarOne()
        {
            string json = File.ReadAllText(Path.Combine(TestPaths.DataRoot(), "Presentation", "match_view.json"));
            Assert.IsFalse(MatchViewReader.Read(json.Replace("\"nearDistance\": 27.0", "\"nearDistance\": 60.0")).IsSuccess);
        }
    }
}
