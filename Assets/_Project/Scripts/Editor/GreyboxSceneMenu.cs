using System.Collections.Generic;
using MahjongNight.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MahjongNight.EditorTools
{
    /// <summary>Menu items for setting up and opening the greybox scene.</summary>
    public static class GreyboxSceneMenu
    {
        private const string Folder = "Assets/_Project/Scenes";
        private const string ScenePath = Folder + "/Greybox.unity";

        [MenuItem("Mahjong Night/Create Greybox Scene")]
        public static void CreateGreyboxScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                && !EditorUtility.DisplayDialog("Greybox scene already exists",
                    $"Replace {ScenePath}?", "Replace", "Cancel"))
                return;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Greybox Game").AddComponent<GreyboxGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log($"Created {ScenePath}. Press Play to start a game against three bots.");
        }

        [MenuItem("Mahjong Night/Open Greybox Scene")]
        public static void OpenGreyboxScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                CreateGreyboxScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
