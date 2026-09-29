using System;
using System.IO;
using Game.Data.Loading;

namespace Game.Tests.Unit
{
    /// <summary>Locates the repository from the test run (dotnet: bin folder; Unity: project folder).</summary>
    internal static class TestPaths
    {
        public static string DataRoot()
        {
            string root = Data.Loading.DataRoot.Find(Directory.GetCurrentDirectory())
                          ?? Data.Loading.DataRoot.Find(AppContext.BaseDirectory);
            if (root == null) throw new InvalidOperationException("Repository Data/ folder not found from the test run directory.");
            return root;
        }

        public static string RepositoryRoot() => Path.GetDirectoryName(DataRoot());

        public static string GameSources() => Path.Combine(RepositoryRoot(), "Unity", "Assets", "_Game");
    }
}
