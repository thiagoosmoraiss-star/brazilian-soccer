using System.Collections.Generic;

namespace Game.Save.IO
{
    /// <summary>
    /// Raw file access for the save system (TECHNICAL_SPEC §14: <c>persistentDataPath/saves/</c> via
    /// <c>IFileStore</c>). Pure: does not reference the Unity engine. The App layer supplies the real root
    /// directory (Unity's <c>Application.persistentDataPath</c>); tests supply a temp directory.
    /// </summary>
    public interface IFileStore
    {
        bool Exists(string relativePath);

        byte[] ReadAllBytes(string relativePath);

        /// <summary>Overwrites the file, creating its directory if needed.</summary>
        void WriteAllBytes(string relativePath, byte[] bytes);

        /// <summary>No-op if the file does not exist.</summary>
        void Delete(string relativePath);

        /// <summary>Overwrites the destination if it exists.</summary>
        void Move(string fromRelativePath, string toRelativePath);

        /// <summary>Overwrites the destination if it exists.</summary>
        void Copy(string fromRelativePath, string toRelativePath);

        /// <summary>File names directly inside the given relative directory (not recursive); empty if the directory does not exist.</summary>
        IReadOnlyList<string> ListFiles(string relativeDirectory);
    }
}
