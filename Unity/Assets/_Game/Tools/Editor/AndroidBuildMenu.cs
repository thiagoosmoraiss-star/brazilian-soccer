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
            bool inputChanged = UseInputSystemOnly();
            AssetDatabase.SaveAssets();
            Debug.Log("ACESSO: Android configurado (IL2CPP, ARM64, paisagem, só Input System)." +
                      (inputChanged ? " Reinicie o Unity para aplicar a troca de input." : ""));
        }

        /// <summary>Active Input Handling = Input System only (CLAUDE.md §3; "Both" is unsupported on Android). There is
        /// no public API for it, so the serialized player setting is edited. Returns whether it changed.</summary>
        private static bool UseInputSystemOnly()
        {
            const int InputSystemOnly = 1;
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            var property = new SerializedObject(settings[0]).FindProperty("activeInputHandler");
            if (property == null || property.intValue == InputSystemOnly) return false;
            property.intValue = InputSystemOnly;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
