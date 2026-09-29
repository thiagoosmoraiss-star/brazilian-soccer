using System.IO;

namespace Game.Data.Loading
{
    /// <summary>Locates the repository-root Data/ folder (D-09) from a starting directory.</summary>
    public static class DataRoot
    {
        public const string FolderName = "Data";

        /// <summary>File whose presence identifies the Data root (guards against unrelated "Data" folders).</summary>
        public const string MarkerFile = GameDataLoader.EffectsFile;

        /// <summary>Walks up from <paramref name="startDirectory"/> until a Data/ folder with the marker is found.</summary>
        public static string Find(string startDirectory)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startDirectory));
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, FolderName);
                if (File.Exists(Path.Combine(candidate, MarkerFile.Replace('/', Path.DirectorySeparatorChar))))
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
