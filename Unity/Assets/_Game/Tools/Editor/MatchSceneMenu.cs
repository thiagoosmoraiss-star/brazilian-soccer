using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>Editor menu (A7b): creates the Match scene (TECHNICAL_SPEC §15; REPOSITORY_STRUCTURE: Assets/Scenes/Match)
    /// with the default camera and light plus the <see cref="Game.App.MatchBootstrap"/>, and puts it first in the build
/// (unless the career scenes are in the build: then Boot stays first, C1).</summary>
    public static class MatchSceneMenu
    {
        public const string ScenePath = "Assets/Scenes/Match.unity";

        [MenuItem("ACESSO/Criar cena Match")]
        public static void CreateMatchScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("ACESSO", ScenePath + " já existe. Recriar?", "Recriar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Match").AddComponent<Game.App.MatchBootstrap>();
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            // With the career scenes (C1) the build starts at Boot: the Match scene keeps its place (or goes last).
            bool career = false, listed = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == CareerScenesMenu.BootPath) career = true;
                if (s.path == ScenePath) listed = true;
            }
            var scenes = new List<EditorBuildSettingsScene>();
            if (!career) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (career || s.path != ScenePath) scenes.Add(s);
            if (career && !listed) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log(career
                ? "ACESSO: " + ScenePath + " criada; o build continua começando pela Boot."
                : "ACESSO: " + ScenePath + " criada e colocada em primeiro no build.");
        }
    }
}
