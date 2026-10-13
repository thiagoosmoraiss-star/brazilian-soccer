using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>
    /// Build step (A7, X-62): the match builds its placeholder art from primitives at runtime, so no asset in the build
    /// references the render pipeline's lit shader and the player strips it (everything renders magenta). Before the
    /// build this writes a material with that shader into the generated Resources folder, which
    /// <see cref="Game.Presentation.MaterialCache"/> loads as its template. Deleted after the build together with the
    /// data pack (<see cref="DataPackBuildStep"/>); never committed.
    /// </summary>
    public sealed class RuntimeMaterialBuildStep : IPreprocessBuildWithReport
    {
        private const string Folder = "Assets/_Generated/Resources";
        private const string LitShader = "Universal Render Pipeline/Lit";

        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report) => WriteMaterial();

        [MenuItem("ACESSO/Dados/Gerar material de runtime (teste)")]
        public static void WriteMaterial()
        {
            var shader = Shader.Find(LitShader) ?? throw new BuildFailedException(LitShader + " not found: is the project on URP (X-05)?");
            Directory.CreateDirectory(Folder);
            string path = Folder + "/" + Game.Presentation.MaterialCache.TemplateResource + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
            Debug.Log("ACESSO: runtime material written to " + path);
        }
    }
}
