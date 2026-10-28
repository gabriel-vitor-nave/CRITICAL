using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Critical.Procedural.Editor
{
    // Local Editor-only automation. It is inert unless the temporary Library sentinel exists.
    [InitializeOnLoad]
    internal static class ExplorationAutomation
    {
        [Serializable] private sealed class Command { public string id; public string action; }
        [Serializable] private sealed class Result
        {
            public string id, action, message, scene;
            public bool success, playing, sceneDirty;
        }

        private const string Folder = "Library/CRITICAL/ProceduralAutomation";
        private const string EnabledFile = Folder + "/enabled";
        private const string CommandFile = Folder + "/command.json";
        private static double nextPoll;

        static ExplorationAutomation()
        {
            if (!File.Exists(EnabledFile)) return;
            Write("ready.json", Snapshot(null, "ready", true, "Editor automation ready."));
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!File.Exists(EnabledFile)) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(CommandFile)) return;

            Command command = null;
            try
            {
                command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandFile));
                File.Delete(CommandFile);
                if (command == null || string.IsNullOrEmpty(command.id))
                    throw new InvalidOperationException("A command requires an id and a supported action.");
                switch (command.action)
                {
                    case "state": break;
                    case "refresh":
                        AssetDatabase.Refresh();
                        break;
                    case "setup-game": Critical.Editor.GameFoundationSetup.Apply(); break;
                    case "organize-assets": Critical.Editor.AssetOrganization.Apply(); break;
                    case "progression-checks": Critical.Progression.Editor.ProgressionChecks.Run(); break;
                    case "geometry-checks": Critical.Progression.Editor.GeometryChecks.Run(); break;
                    case "foundation-checks": Critical.Editor.FoundationChecks.Run(); break;
                    case "tidy-empty-folders": Critical.Editor.FoundationChecks.CleanEmptyFolders(); break;
                    case "reimport-menu-scripts":
                        if (EditorApplication.isPlayingOrWillChangePlaymode)
                            throw new InvalidOperationException("Script binding repair requires Edit Mode.");
                        foreach (string scriptPath in Directory.GetFiles("Assets/Menu/Scripts", "*.cs"))
                            AssetDatabase.ImportAsset(scriptPath.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
                        break;
                    case "reimport-player-scripts":
                        foreach (string scriptPath in Directory.GetFiles("Assets/_Scripts", "*.cs"))
                            AssetDatabase.ImportAsset(scriptPath.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                        break;
                    case "inspect-scripts":
                        var bindings = new List<string>();
                        var bindingPaths = new List<string>(Directory.GetFiles("Assets/Menu/Scripts", "*.cs"));
                        bindingPaths.AddRange(Directory.GetFiles("Assets/_Scripts", "*.cs"));
                        foreach (string bindingPath in bindingPaths)
                        {
                            var bindingScript = AssetDatabase.LoadAssetAtPath<MonoScript>(bindingPath.Replace('\\', '/'));
                            bindings.Add(bindingPath + "|" + (bindingScript == null ? "NO SCRIPT" : bindingScript.GetClass()?.FullName ?? "NO CLASS"));
                        }
                        File.WriteAllText("Library/CRITICAL/ScriptBindings.txt", string.Join("\n", bindings));
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, string.Join("\n", bindings)));
                        return;
                    case "inspect-player":
                        var details = new List<string>();
                        foreach (string guid in new[] { "eff90601fbd5e774ebdbefb281cb2baf", "5e4e99ed02e24ae45a7dd46c82586f6a", "29d70568809d4e94991f9d101ece1b21", "93570d6bd4694dd99458ca04e5e28289" })
                        {
                            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                            details.Add(scriptPath + "|" + (script == null ? "NO SCRIPT" : script.GetClass()?.FullName ?? "NO CLASS"));
                        }
                        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CRITICAL/Procedural/Prefabs/ExplorationPlayer.prefab");
                        foreach (Component component in playerPrefab.GetComponents<Component>())
                            details.Add(component == null ? "MISSING COMPONENT" : component.GetType().FullName);
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, string.Join("\n", details)));
                        return;
                    case "game-loop-checks": Critical.Progression.Editor.GameLoopChecks.StartChecks(); break;
                    case "open-menu":
                        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open Menu requires Edit Mode.");
                        for (int i = 0; i < SceneManager.sceneCount; i++)
                            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("An open scene has unsaved changes.");
                        EditorSceneManager.OpenScene(Critical.Editor.MenuGameSetup.MenuPath);
                        break;
                    case "scenes":
                        var scenes = new List<string>();
                        for (int i = 0; i < SceneManager.sceneCount; i++)
                        {
                            var candidate = SceneManager.GetSceneAt(i);
                            var names = new List<string>();
                            foreach (var root in candidate.GetRootGameObjects()) names.Add(root.name);
                            scenes.Add(candidate.name + "|" + candidate.path + "|dirty=" + candidate.isDirty + "|roots=" + string.Join(",", names));
                        }
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, string.Join("\n", scenes)));
                        return;
                    case "build": ExplorationSceneSetup.Build(); break;
                    case "recover-failed-build":
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, RecoverFailedBuild()));
                        return;
                    case "generation-checks": RoomGeneratorChecks.Run(); break;
                    case "runtime-checks": ExplorationRuntimeChecks.StartChecks(); break;
                    case "capture":
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("A captura exige Play Mode.");
                        ScreenCapture.CaptureScreenshot(Folder + "/game.png");
                        break;
                    case "play":
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, "Play mode requested."));
                        EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
                        return;
                    case "stop":
                        Write(command.id + ".json", Snapshot(command.id, command.action, true, "Stop requested."));
                        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
                        return;
                    default: throw new InvalidOperationException("Unknown automation action: " + command.action);
                }
                Write(command.id + ".json", Snapshot(command.id, command.action, true, "Completed."));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (command != null && !string.IsNullOrEmpty(command.id))
                    Write(command.id + ".json", Snapshot(command.id, command.action, false, error.ToString()));
            }
        }

        private static Result Snapshot(string id, string action, bool success, string message)
        {
            var scene = EditorSceneManager.GetActiveScene();
            return new Result { id = id, action = action, success = success, message = message,
                scene = scene.path, sceneDirty = scene.isDirty, playing = EditorApplication.isPlaying };
        }

        private static string RecoverFailedBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("A recuperação da cena temporária exige Edit Mode.");
            var matches = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (IsFailedBuildScene(candidate)) matches.Add(candidate);
            }
            if (matches.Count != 1)
                throw new InvalidOperationException("A recuperação exige exatamente uma cena sem caminho com apenas ExplorationPlayer, Exploration Camera e RoomLight, sem ExplorationFlow. Encontradas: " + matches.Count);

            Scene target = matches[0];
            Scene previouslyActive = SceneManager.GetActiveScene();
            Directory.CreateDirectory(Folder);
            string backup = Folder + "/FailedBuild_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
                + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".unity";
            if (!EditorSceneManager.SaveScene(target, backup, true))
                throw new IOException("O backup falhou; nenhuma cena foi fechada: " + backup);
            if (!IsFailedBuildScene(target))
                throw new InvalidOperationException("A cena mudou durante o backup e foi mantida aberta: " + backup);
            if (!EditorSceneManager.CloseScene(target, true))
                throw new InvalidOperationException("O backup foi salvo, mas a cena temporária não pôde ser fechada: " + backup);
            if (previouslyActive.IsValid() && previouslyActive.isLoaded)
                SceneManager.SetActiveScene(previouslyActive);
            return "Cópia salva e somente a cena temporária validada foi fechada: " + backup;
        }

        private static bool IsFailedBuildScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !string.IsNullOrEmpty(scene.path)) return false;
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length != 3) return false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject root in roots)
            {
                if (root.GetComponentInChildren<ExplorationFlow>(true) != null) return false;
                names.Add(root.name);
            }
            return names.SetEquals(new[] { "ExplorationPlayer", "Exploration Camera", "RoomLight" });
        }

        private static void Write(string name, Result result)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, name), JsonUtility.ToJson(result, true));
        }
    }
}
