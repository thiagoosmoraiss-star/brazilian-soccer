using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>Editor menu (A7b): applies the decided Android player settings (CLAUDE.md §3: ARM64, IL2CPP; landscape match).
    /// The build itself is the usual File → Build Profiles (Android), with the Match scene first (ACESSO → Criar cena Match).</summary>
    public static class AndroidBuildMenu
    {
        [MenuItem("ACESSO/Configurar build Android (IL2CPP, ARM64)")]
        public static void Configure()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.companyName = "ACESSO";
            PlayerSettings.productName = "ACESSO VS";
            AssetDatabase.SaveAssets();
            Debug.Log("ACESSO: Android configurado (IL2CPP, ARM64, paisagem).");
        }
    }
}
