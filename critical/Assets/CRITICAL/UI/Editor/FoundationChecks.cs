using System;
using System.Collections.Generic;
using System.IO;
using Critical.Gameplay;
using Critical.Procedural;
using Critical.Progression;
using Critical.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Critical.Editor
{
    public static class FoundationChecks
    {
        private const string MenuPath = "Assets/CRITICAL/Scenes/Menu.unity";
        private const string GamePath = "Assets/CRITICAL/Scenes/Exploration.unity";
        private const string ReportPath = "Library/CRITICAL/FoundationChecks.json";

        [Serializable]
        private sealed class Check
        {
            public string name, detail;
            public bool passed;
        }

        [Serializable]
        private sealed class Report
        {
            public bool passed;
            public string completedAtUtc, error;
            public int prefabsChecked, dependenciesChecked;
            public Check[] checks;
        }

        [MenuItem("CRITICAL/Verificar base instalada")]
        public static void Run()
        {
            var checks = new List<Check>();
            var openedHere = new List<Scene>();
            Scene previous = SceneManager.GetActiveScene();
            var report = new Report();
            try
            {
                RequireCleanEditor();
                Assert(checks, "menu-guid", AssetDatabase.AssetPathToGUID(MenuPath) == "b1c31156812ee4d4593f95140a9b5c30", "O GUID do Menu original foi preservado.");
                Assert(checks, "exploration-guid", AssetDatabase.AssetPathToGUID(GamePath) == "a08e2a15728e0344d82ab5caecef15b5", "O GUID de Exploration foi preservado.");
                EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
                Assert(checks, "build-order", buildScenes.Length >= 2 && buildScenes[0].enabled && buildScenes[0].path == MenuPath
                    && buildScenes[1].enabled && buildScenes[1].path == GamePath, "Menu é a cena 0; Exploration é a cena 1, ambas habilitadas.");
                Assert(checks, "play-mode-start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == MenuPath, "Play Mode começa pelo Menu canônico.");

                Scene menu = Open(MenuPath, openedHere);
                Scene game = Open(GamePath, openedHere);
                CheckMissingScripts(checks, menu);
                CheckMissingScripts(checks, game);
                CheckMenu(checks, menu);
                CheckGame(checks, game);

                string[] requiredPrefabs =
                {
                    "Assets/_Scripts/Prefabs/Player.prefab",
                    "Assets/CRITICAL/Procedural/Prefabs/ExplorationPlayer.prefab",
                    "Assets/CRITICAL/Procedural/Prefabs/RoomGrid.prefab",
                    "Assets/CRITICAL/Procedural/Prefabs/CatacombDoor.prefab",
                    "Assets/CRITICAL/Procedural/Prefabs/DiceOverlay.prefab",
                    "Assets/CRITICAL/Gameplay/Prefabs/Rat.prefab",
                    "Assets/CRITICAL/UI/Prefabs/RunHUD.prefab"
                };
                foreach (string path in requiredPrefabs)
                    Assert(checks, "required-prefab", AssetDatabase.LoadAssetAtPath<GameObject>(path) != null, path);
                var dependencyIds = new HashSet<int>();
                string[] prefabPaths = Directory.GetFiles("Assets/CRITICAL", "*.prefab", SearchOption.AllDirectories);
                Array.Sort(prefabPaths, StringComparer.Ordinal);
                foreach (string diskPath in prefabPaths)
                {
                    string path = diskPath.Replace('\\', '/');
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert(checks, "prefab-load", prefab != null, path);
                    CheckMissingScripts(checks, prefab, path);
                    foreach (UnityEngine.Object dependency in EditorUtility.CollectDependencies(new UnityEngine.Object[] { prefab }))
                        if (dependency != null) dependencyIds.Add(dependency.GetInstanceID());
                    report.prefabsChecked++;
                }
                report.dependenciesChecked = dependencyIds.Count;
                report.passed = true;
                report.error = "";
                Debug.Log("CRITICAL: base verificada; " + checks.Count + " verificações, " + report.prefabsChecked + " prefabs sem scripts ausentes.");
            }
            catch (Exception error)
            {
                report.passed = false;
                report.error = error.ToString();
                throw;
            }
            finally
            {
                for (int index = openedHere.Count - 1; index >= 0; index--)
                {
                    Scene scene = openedHere[index];
                    if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                report.completedAtUtc = DateTime.UtcNow.ToString("O");
                report.checks = checks.ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            }
        }

        private static void CheckMenu(List<Check> checks, Scene menu)
        {
            MenuTransition transition = Find<MenuTransition>(menu);
            MenuGameActions actions = Find<MenuGameActions>(menu);
            var start = Find<UnityEngine.UI.Button>(menu, "Botao start");
            var exit = Find<UnityEngine.UI.Button>(menu, "Botao exit");
            var options = Find<UnityEngine.UI.Button>(menu, "Botao options");
            var back = Find<UnityEngine.UI.Button>(menu, "back");
            Assert(checks, "menu-actions", transition != null && actions != null && actions.enabled && actions.menuTransition == transition,
                "MenuGameActions referencia a transição existente.");
            Assert(checks, "menu-start", start != null && actions.startButton == start && HasListener(start, actions, nameof(MenuGameActions.StartRun)),
                "Start chama MenuGameActions.StartRun por conexão persistente.");
            Assert(checks, "menu-exit", exit != null && HasListener(exit, actions, nameof(MenuGameActions.QuitGame)),
                "Exit chama MenuGameActions.QuitGame por conexão persistente.");
            Assert(checks, "menu-options", options != null && back != null && HasListener(options, transition, nameof(MenuTransition.AbrirOpcoes))
                && HasListener(back, transition, nameof(MenuTransition.FecharOpcoes)), "Options e Voltar preservam as conexões existentes.");
        }

        private static void CheckGame(List<Check> checks, Scene game)
        {
            ExplorationFlow flow = Find<ExplorationFlow>(game);
            Assert(checks, "exploration-flow", flow != null && flow.enabled, "ExplorationFlow está ativo na cena.");
            Assert(checks, "flow-references", flow.player != null && flow.playerBody != null && flow.camera != null && flow.roomPrefab != null
                && flow.doorPrefab != null && flow.outsideDoor != null && flow.outsideArea != null && flow.darknessOverlay != null
                && flow.overlayGroup != null && flow.diceRoller != null && flow.playerLight != null && flow.roomLight != null,
                "Jogador, física, câmera, sala, portas, área externa, overlay, dado e luzes estão atribuídos.");
            PlayerHealth health = flow.player.GetComponent<PlayerHealth>();
            RunProgression run = flow.player.GetComponent<RunProgression>();
            Assert(checks, "player-health", health != null && health.enabled && health.maxHP > 0 && health.HP > 0 && health.HP <= health.maxHP,
                "Vida está habilitada com valores iniciais válidos.");
            Assert(checks, "player-body", flow.playerBody.gameObject == flow.player.gameObject, "O Rigidbody do fluxo pertence ao jogador.");
            Assert(checks, "movement-body", flow.player.rb != null && flow.player.rb == flow.playerBody,
                "PlayerMovement referencia o Rigidbody persistido do jogador.");
            Animator playerAnimator = flow.player.GetComponentInChildren<Animator>(true);
            Assert(checks, "movement-animator", playerAnimator != null && flow.player.anim == playerAnimator
                && playerAnimator.runtimeAnimatorController != null,
                "PlayerMovement referencia o Animator do filho, com controller atribuído.");
            Assert(checks, "player-combat", flow.player.GetComponent<Player_Combat>() != null, "O jogador possui o componente de combate.");
            Assert(checks, "run-catalog", run != null && run.enabled && run.itemCatalog != null && run.itemCatalog.Length == 5,
                "A progressão contém o catálogo dos cinco itens da demo.");
            var itemIds = new HashSet<string>();
            foreach (ItemDefinition item in run.itemCatalog)
                Assert(checks, "item-icon", item != null && !string.IsNullOrEmpty(item.Id) && itemIds.Add(item.Id) && item.Icon != null,
                    item != null ? item.Id + ": ícone atribuído e ID único." : "Item nulo no catálogo.");
            GameHud hud = Find<GameHud>(game);
            Assert(checks, "hud-references", hud != null && hud.enabled && hud.transform.parent == null && hud.playerHealth == health
                && hud.progression == run && hud.flow == flow, "HUD na raiz referencia exatamente o jogador, a progressão e o fluxo desta cena.");
            RoomEncounter encounter = Find<RoomEncounter>(game);
            Assert(checks, "encounter-references", encounter != null && encounter.enabled && encounter.flow == flow && encounter.enemyPrefab != null,
                "Encontros possuem o fluxo correto e o prefab de inimigo.");
        }

        public static void CleanEmptyFolders()
        {
            RequireCleanEditor();
            int deleted = 0;
            string[] knownEmptyFolders = { "Assets/CRITICAL/Scenes 1", "Assets/CRITICAL/Procedural/Scenes", "Assets/Importado" };
            foreach (string path in knownEmptyFolders)
            {
                if (!Directory.Exists(path) || Directory.GetFileSystemEntries(path).Length != 0) continue;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.IsValidFolder(path) || !AssetDatabase.DeleteAsset(path))
                    throw new IOException("Não foi possível remover a pasta vazia conhecida: " + path);
                deleted++;
            }
            Debug.Log("CRITICAL: " + deleted + " pastas antigas confirmadas vazias foram removidas.");
        }

        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("A verificação exige Edit Mode e compilação concluída.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Cena com alterações não salvas preservada: " + SceneManager.GetSceneAt(index).name);
        }

        private static Scene Open(string path, List<Scene> openedHere)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded) return scene;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            openedHere.Add(scene);
            return scene;
        }

        private static T Find<T>(Scene scene, string name = null) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T candidate in root.GetComponentsInChildren<T>(true))
                    if (name == null || candidate.name == name) return candidate;
            return null;
        }

        private static bool HasListener(UnityEngine.UI.Button button, UnityEngine.Object target, string method)
        {
            for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
                if (button.onClick.GetPersistentTarget(index) == target && button.onClick.GetPersistentMethodName(index) == method) return true;
            return false;
        }

        private static void CheckMissingScripts(List<Check> checks, Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) CheckMissingScripts(checks, root, scene.path);
        }

        private static void CheckMissingScripts(List<Check> checks, GameObject root, string path)
        {
            int missing = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            Assert(checks, "missing-scripts", missing == 0, path + "/" + root.name + ": " + missing + " scripts ausentes.");
        }

        private static void Assert(List<Check> checks, string name, bool passed, string detail)
        {
            checks.Add(new Check { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
    }
}
