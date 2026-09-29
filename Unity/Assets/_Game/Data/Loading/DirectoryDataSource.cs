using System;
using System.IO;
using System.Text;

namespace Game.Data.Loading
{
    /// <summary>
    /// <see cref="IDataSource"/> over a directory on disk. Used by the .NET solution/headless runs and by
    /// the Unity editor, both pointing at the same repository-root Data/ folder (single source of truth).
    /// </summary>
    public sealed class DirectoryDataSource : IDataSource
    {
        public string RootPath { get; }
        public string Description => RootPath;

        public DirectoryDataSource(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("Root path is required.", nameof(rootPath));
            RootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(RootPath)) throw new DirectoryNotFoundException("Data root not found: " + RootPath);
        }

        public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

        public string ReadAllText(string relativePath) => File.ReadAllText(Resolve(relativePath), Encoding.UTF8);

        private string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) throw new ArgumentException("Path is required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath) || relativePath.Contains("\\"))
                throw new ArgumentException("Data paths must be relative and use '/': " + relativePath, nameof(relativePath));
            foreach (var segment in relativePath.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new ArgumentException("Invalid data path: " + relativePath, nameof(relativePath));
            return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
