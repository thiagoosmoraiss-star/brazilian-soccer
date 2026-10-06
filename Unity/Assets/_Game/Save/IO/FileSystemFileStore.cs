using System;
using System.Collections.Generic;
using System.IO;

namespace Game.Save.IO
{
    /// <summary>
    /// <see cref="IFileStore"/> over a real directory on disk (mirrors <c>Game.Data.Loading.DirectoryDataSource</c>'s
    /// path handling). Used by the .NET solution/headless tools and, rooted at <c>Application.persistentDataPath</c>,
    /// by the Unity app.
    /// </summary>
    public sealed class FileSystemFileStore : IFileStore
    {
        public string RootPath { get; }

        public FileSystemFileStore(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) throw new ArgumentException("Root path is required.", nameof(rootPath));
            RootPath = Path.GetFullPath(rootPath);
            Directory.CreateDirectory(RootPath);
        }

        public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

        public byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(Resolve(relativePath));

        public void WriteAllBytes(string relativePath, byte[] bytes)
        {
            string path = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        public void Delete(string relativePath)
        {
            string path = Resolve(relativePath);
            if (File.Exists(path)) File.Delete(path);
        }

        public void Move(string fromRelativePath, string toRelativePath)
        {
            string from = Resolve(fromRelativePath);
            string to = Resolve(toRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
        }

        public void Copy(string fromRelativePath, string toRelativePath)
        {
            string from = Resolve(fromRelativePath);
            string to = Resolve(toRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, overwrite: true);
        }

        public IReadOnlyList<string> ListFiles(string relativeDirectory)
        {
            string dir = Resolve(relativeDirectory);
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            var names = new List<string>();
            foreach (var file in Directory.GetFiles(dir)) names.Add(Path.GetFileName(file));
            return names;
        }

        private string Resolve(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) throw new ArgumentException("Path is required.", nameof(relativePath));
            if (Path.IsPathRooted(relativePath) || relativePath.Contains("\\"))
                throw new ArgumentException("Save paths must be relative and use '/': " + relativePath, nameof(relativePath));
            foreach (var segment in relativePath.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new ArgumentException("Invalid save path: " + relativePath, nameof(relativePath));
            return Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
