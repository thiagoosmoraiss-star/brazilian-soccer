using System.IO;
using Game.App;
using Game.Data.Loading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 0 acceptance (Unity side): in the editor, the loader reads the repository-root Data/ folder
    /// directly - the same files the .NET solution reads - with no copy under Assets.
    /// In player builds, Data/ packaging does not exist yet (ROADMAP: added by the first stage whose Android
    /// build reads Data/), so resolution must fail explicitly instead of guessing a path.
    /// Run: Unity -batchmode -projectPath Unity -runTests -testPlatform PlayMode
    /// </summary>
    public class DataRootEditorTests
    {
        [Test]
        [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor)]
        public void Editor_ResolvesRepositoryRootData_AndLoads()
        {
            var resolved = UnityDataRoot.Resolve();
            Assert.IsTrue(resolved.IsSuccess, resolved.ToString());

            var source = (DirectoryDataSource)resolved.Value;
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            Assert.AreEqual(Path.Combine(repositoryRoot, "Data"), source.RootPath);

            // Same files and pipeline as the .NET tests (Newtonsoft, D-11).
            var db = GameDataLoader.Load(source);
            Assert.IsTrue(db.IsSuccess, db.ToString());
        }

        [Test]
        [UnityPlatform(exclude = new[] { RuntimePlatform.WindowsEditor, RuntimePlatform.OSXEditor, RuntimePlatform.LinuxEditor })]
        public void Player_ReportsDataNotPackaged_UntilPackagingExists()
        {
            var resolved = UnityDataRoot.Resolve();
            Assert.IsFalse(resolved.IsSuccess);
            Assert.AreEqual(UnityDataRoot.NotPackaged, resolved.Errors[0].Code);
        }
    }
}
