using System;
using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Unit
{
    /// <summary>The loader reads the repository-root Data/ (D-09) - the single source of truth.</summary>
    public class DataLoadingTests
    {
        [Test]
        public void DataRoot_IsTheRepositoryRootFolder_OutsideAssets()
        {
            string root = TestPaths.DataRoot();
            Assert.AreEqual("Data", Path.GetFileName(root));
            Assert.IsTrue(File.Exists(Path.Combine(TestPaths.RepositoryRoot(), "CLAUDE.md")), "Data/ must sit at the repository root.");
            StringAssert.DoesNotContain(Path.DirectorySeparatorChar + "Assets" + Path.DirectorySeparatorChar, root + Path.DirectorySeparatorChar);
        }

        [Test]
        public void Loader_ReadsRepositoryData()
        {
            var source = new DirectoryDataSource(TestPaths.DataRoot());
            var result = GameDataLoader.Load(source);
            Assert.IsTrue(result.IsSuccess, result.ToString());
            StringAssert.StartsWith("{", source.ReadAllText(GameDataLoader.EffectsFile).TrimStart());
        }

        [Test]
        public void Loader_ReportsMissingFiles()
        {
            string empty = Path.Combine(Path.GetTempPath(), "data-empty-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                var result = GameDataLoader.Load(new DirectoryDataSource(empty));
                Assert.IsFalse(result.IsSuccess);
                Assert.AreEqual(GameDataLoader.RequiredFiles.Count, result.Errors.Count);
                Assert.AreEqual(GameDataLoader.MissingFile, result.Errors[0].Code);
            }
            finally
            {
                Directory.Delete(empty, true);
            }
        }

        [Test]
        public void DataSource_RejectsPathsEscapingTheRoot()
        {
            var source = new DirectoryDataSource(TestPaths.DataRoot());
            Assert.Throws<ArgumentException>(() => source.Exists("../CLAUDE.md"));
            Assert.Throws<ArgumentException>(() => source.Exists("Balance\\effects.json"));
            Assert.Throws<ArgumentException>(() => source.Exists(Path.GetFullPath("/etc/passwd")));
        }

        [Test]
        public void NoDataCopyInsideUnityAssets()
        {
            // Single source of truth: no Data folder or JSON data under Assets (including StreamingAssets).
            string assets = Path.Combine(TestPaths.RepositoryRoot(), "Unity", "Assets");
            Assert.IsFalse(Directory.Exists(Path.Combine(assets, "StreamingAssets", "Data")));
            foreach (var json in Directory.GetFiles(assets, "*.json", SearchOption.AllDirectories))
                Assert.Fail("Unexpected JSON data file under Assets: " + json);
        }
    }
}
