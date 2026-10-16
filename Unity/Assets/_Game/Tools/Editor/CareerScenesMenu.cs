using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tools
{
    /// <summary>Editor menu (C1): creates the Boot and Shell scenes (TECHNICAL_SPEC §15; REPOSITORY_STRUCTURE
    /// Assets/Scenes: Boot, Shell, Match) and sets the build order Boot → Shell → Match. The Match scene comes from
    /// "ACESSO/Criar cena Match" (created too if missing).</summary>
    public static class CareerScenesMenu
    {
        public const string BootPath = "Assets/Scenes/Boot.unity";
        public const string ShellPath = "Assets/Scenes/Shell.unity";

        [MenuItem("ACESSO/Criar cenas da carreira (Boot, Shell)")]
        public static void CreateCareerScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(MatchSceneMenu.ScenePath)) MatchSceneMenu.CreateMatchScene();
            Create(BootPath, "Boot", go => go.AddComponent<Game.App.BootLoader>());
            Create(ShellPath, "Shell", go => go.AddComponent<Game.App.ShellScreen>());

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(ShellPath, true),
                new EditorBuildSettingsScene(MatchSceneMenu.ScenePath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != BootPath && s.path != ShellPath && s.path != MatchSceneMenu.ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(BootPath);
            Debug.Log("ACESSO: cenas Boot e Shell criadas; ordem do build: Boot, Shell, Match. Dê Play na cena Boot.");
        }

        private static void Create(string path, string name, System.Action<GameObject> add)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            add(new GameObject(name));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
