using System.IO;
using Game.App;
using Game.Data.Loading;
using NUnit.Framework;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 0 acceptance (Unity side): in the editor, the loader reads the repository-root Data/ folder
    /// directly - the same files the .NET solution reads - with no copy under Assets.
    /// Run: Unity -batchmode -projectPath Unity -runTests -testPlatform PlayMode
    /// </summary>
    public class DataRootEditorTests
    {
        [Test]
        public void Editor_ResolvesRepositoryRootData_AndLoads()
        {
            var resolved = UnityDataRoot.Resolve();
            Assert.IsTrue(resolved.IsSuccess, resolved.ToString());

            var source = (DirectoryDataSource)resolved.Value;
            string repositoryRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
            Assert.AreEqual(Path.Combine(repositoryRoot, "Data"), source.RootPath);

            // Same file and pipeline as the .NET tests (Newtonsoft, D-11).
            var definitions = GameDataLoader.LoadEffectDefinitions(source);
            Assert.IsTrue(definitions.IsSuccess, definitions.ToString());
        }
    }
}
