using System;
using System.Collections.Generic;
using System.IO;
using Critical.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Critical.Editor
{
    /// <summary>Patches the authored menu without rebuilding its visual hierarchy.</summary>
    public static class MenuGameSetup
    {
        public const string MenuPath = "Assets/CRITICAL/Scenes/Menu.unity";
        public const string GamePath = "Assets/CRITICAL/Scenes/Exploration.unity";

        [MenuItem("CRITICAL/UI/Conectar menu à partida")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Conecte o menu fora do Play Mode.");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(MenuPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
                throw new InvalidOperationException("O Menu tem alterações não salvas. Salve a cena antes de conectar os botões.");
            if (openedHere) scene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);
            try
            {
                MenuTransition transition = Find<MenuTransition>(scene, null);
                var start = Find<UnityEngine.UI.Button>(scene, "Botao start");
                var exit = Find<UnityEngine.UI.Button>(scene, "Botao exit");
                if (transition == null || start == null || exit == null)
                    throw new InvalidOperationException("O Menu exige MenuTransition, Botao start e Botao exit existentes.");
                var actions = transition.GetComponent<MenuGameActions>();
                if (actions == null) actions = transition.gameObject.AddComponent<MenuGameActions>();
                actions.menuTransition = transition;
                actions.startButton = start;
                Wire(start, actions, nameof(MenuGameActions.StartRun), actions.StartRun);
                Wire(exit, actions, nameof(MenuGameActions.QuitGame), actions.QuitGame);
                EditorUtility.SetDirty(actions);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new IOException("Não foi possível salvar as conexões do Menu.");
                ConfigureBuildSettings();
                Debug.Log("CRITICAL: Start → Exploration; Exit → sair da build. Menu e partida configurados na build.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        public static void ConfigureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true)
            };
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.path != MenuPath && scene.path != GamePath && File.Exists(scene.path)) scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T Find<T>(Scene scene, string objectName) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T candidate in root.GetComponentsInChildren<T>(true))
                    if (objectName == null || candidate.name == objectName) return candidate;
            return null;
        }

        private static void Wire(UnityEngine.UI.Button button, MenuGameActions actions, string method, UnityAction callback)
        {
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                if (button.onClick.GetPersistentTarget(index) == actions && button.onClick.GetPersistentMethodName(index) == method)
                    return;
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            EditorUtility.SetDirty(button);
        }
    }
}
