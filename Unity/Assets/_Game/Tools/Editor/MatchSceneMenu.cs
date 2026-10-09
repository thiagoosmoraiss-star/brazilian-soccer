using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>Editor menu (A7b): creates the Match scene (TECHNICAL_SPEC §15; REPOSITORY_STRUCTURE: Assets/Scenes/Match)
    /// with the default camera and light plus the <see cref="Game.App.MatchBootstrap"/>, and puts it first in the build.</summary>
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

            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("ACESSO: " + ScenePath + " criada e colocada em primeiro no build.");
        }
    }
}
