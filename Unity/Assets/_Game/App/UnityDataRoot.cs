using System.IO;
using Game.Core.Results;
using Game.Data.Loading;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Resolves the repository-root Data/ folder (D-09) for the Unity side.
    /// Editor: read directly from the repository (Unity/Assets/../../Data), no copy.
    /// Player builds (A7b): the build step (Tools/Editor DataPackBuildStep) packs Data/ into a generated Resources text
    /// asset, read here through <see cref="PackedDataSource"/>; the pack is never edited nor committed.
    /// </summary>
    public static class UnityDataRoot
    {
        public const string NotPackaged = "DATA_NOT_PACKAGED";
        public const string NotFound = "DATA_ROOT_NOT_FOUND";
        /// <summary>Resources name of the generated data pack.</summary>
        public const string PackResource = "AcessoDataPack";

        public static Result<IDataSource> Resolve()
        {
            if (!Application.isEditor)
            {
                var pack = Resources.Load<TextAsset>(PackResource);
                if (pack == null)
                    return Result<IDataSource>.Fail(NotPackaged, "The data pack is missing from this build (Resources/" + PackResource + ").");
                return Result<IDataSource>.Ok(PackedDataSource.FromPack(pack.text, "data pack in the build"));
            }

            // Application.dataPath = <repo>/Unity/Assets
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string root = DataRoot.Find(projectRoot);
            return root == null
                ? Result<IDataSource>.Fail(NotFound, "Data/ not found above " + projectRoot)
                : Result<IDataSource>.Ok(new DirectoryDataSource(root));
        }
    }
}
