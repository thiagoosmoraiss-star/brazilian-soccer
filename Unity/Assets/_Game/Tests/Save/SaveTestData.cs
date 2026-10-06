using System;
using System.IO;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.Save
{
    /// <summary>Loads the real GameDatabase (mirrors Tests.Career/CareerTestData.cs; duplicated rather than shared
    /// since both classes are internal to their own assembly).</summary>
    internal static class SaveTestData
    {
        private static GameDatabase _db;

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

        /// <summary>A fresh, empty temp directory for a <c>FileSystemFileStore</c>; the caller is not expected to
        /// clean it up (CI runs in a throwaway container).</summary>
        public static string NewTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "save-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static string RepositoryRoot() => Path.GetDirectoryName(DataRoot());

        /// <summary>Fixtures live under <c>dotnet/Tests.Save/Fixtures</c> (not under Unity/Assets, D-09: no JSON
        /// data copy inside Assets) but are plain files on disk, so both Unity and the .NET test run reach them
        /// the same way <c>Data/</c> itself is reached from either side.</summary>
        public static string FixturePath(string fileName) =>
            Path.Combine(RepositoryRoot(), "dotnet", "Tests.Save", "Fixtures", fileName);
    }
}
