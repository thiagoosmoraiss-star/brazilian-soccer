using System.IO;
using Game.Data.Loading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>
    /// Build/packaging step for player builds (A7b; D-09, ROADMAP "a primeira etapa cuja build Android precisar ler
    /// Data/ inclui a etapa de build/empacotamento"): packs the repository's Data/ into a generated Resources text asset
    /// before the build and deletes it afterwards. Data/ stays the only source of truth; the pack is never edited or
    /// committed (Unity/Assets/_Generated is git-ignored).
    /// </summary>
    public sealed class DataPackBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string Folder = "Assets/_Generated/Resources";
        private static string PackPath => Folder + "/" + Game.App.UnityDataRoot.PackResource + ".json";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => WritePack();

        public void OnPostprocessBuild(BuildReport report) => DeletePack();

        [MenuItem("ACESSO/Dados/Gerar pacote de dados (teste)")]
        public static void WritePack()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dataRoot = DataRoot.Find(projectRoot) ?? throw new BuildFailedException("Data/ not found above " + projectRoot);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PackPath, PackedDataSource.Pack(dataRoot));
            AssetDatabase.ImportAsset(PackPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("ACESSO: data pack written to " + PackPath);
        }

        [MenuItem("ACESSO/Dados/Apagar pacote de dados")]
        public static void DeletePack()
        {
            if (AssetDatabase.DeleteAsset("Assets/_Generated")) Debug.Log("ACESSO: data pack removed.");
        }
    }
}
