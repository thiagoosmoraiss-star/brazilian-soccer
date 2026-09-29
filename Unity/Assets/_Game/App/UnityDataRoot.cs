using System.IO;
using Game.Core.Results;
using Game.Data.Loading;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Resolves the repository-root Data/ folder (D-09) for the Unity side.
    /// Editor: read directly from the repository (Unity/Assets/../../Data), no copy.
    /// Player builds: the build/packaging step that copies Data/ into the app is created by the first stage
    /// whose Android build needs data (ROADMAP); until then this reports a failure instead of guessing.
    /// </summary>
    public static class UnityDataRoot
    {
        public const string NotPackaged = "DATA_NOT_PACKAGED";
        public const string NotFound = "DATA_ROOT_NOT_FOUND";

        public static Result<IDataSource> Resolve()
        {
            if (!Application.isEditor)
                return Result<IDataSource>.Fail(NotPackaged,
                    "Data/ packaging for player builds is not defined yet (first stage whose Android build reads Data/).");

            // Application.dataPath = <repo>/Unity/Assets
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = DataRoot.Find(projectRoot);
            return root == null
                ? Result<IDataSource>.Fail(NotFound, "Data/ not found above " + projectRoot)
                : Result<IDataSource>.Ok(new DirectoryDataSource(root));
        }
    }
}
