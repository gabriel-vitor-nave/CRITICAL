using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Critical.Editor
{
    /// <summary>Moves the existing assets with their GUIDs; never regenerates scenes or prefabs.</summary>
    public static class AssetOrganization
    {
        private const string OwnPath = "Assets/CRITICAL/UI/Editor/AssetOrganization.cs";
        private const string MenuPath = "Assets/CRITICAL/Scenes/Menu.unity";
        private const string GamePath = "Assets/CRITICAL/Scenes/Exploration.unity";
        private const string ReportPath = "Library/CRITICAL/AssetOrganization.json";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        [Serializable]
        private sealed class Move
        {
            public string from, to;
            public Move(string source, string target) { from = source; to = target; }
        }

        [Serializable]
        private sealed class Report
        {
            public bool success;
            public string completedAtUtc, error;
            public int movedPaths, rewrittenScripts;
            public Move[] moves;
            public string[] scripts;
        }

        // Keep the original sources here: Apply can also finish a partly organized checkout.
        private static readonly Move[] Moves =
        {
            new Move("Assets/Scenes", "Assets/CRITICAL/Scenes"),
            new Move("Assets/CRITICAL/Procedural/Scenes/Exploration.unity", GamePath),
            new Move("Assets/Arte", "Assets/CRITICAL/Art"),
            new Move("Assets/Importado/free-2d-top-down-pixel-dungeon-asset-pack", "Assets/ThirdParty/DungeonPack")
        };

        [MenuItem("CRITICAL/Projeto/Organizar assets existentes")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Organize os assets em Edit Mode, com a compilação concluída.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Há uma cena com alterações não salvas: " + SceneManager.GetSceneAt(index).name);

            // Inspect every destination before the first write, including targets inside a pending folder move.
            var pending = new List<Move>();
            for (int index = 0; index < Moves.Length; index++)
            {
                Move move = Moves[index];
                bool sourceExists = Exists(move.from);
                string projectedTarget = BeforePendingMoves(move.to, index);
                bool targetExists = Exists(move.to) || Exists(projectedTarget);
                if (sourceExists && targetExists)
                    throw new IOException("Destino já ocupado; nada foi movido: " + move.to);
                if (!sourceExists && !targetExists)
                    throw new FileNotFoundException("Origem e destino ausentes; nada foi movido: " + move.from);
                if (sourceExists)
                {
                    if (!File.Exists(move.from + ".meta") || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(move.from)))
                        throw new IOException("A origem precisa estar importada com seu .meta: " + move.from);
                    if (File.Exists(move.to + ".meta"))
                        throw new IOException("Existe um .meta órfão no destino; nada foi movido: " + move.to);
                    pending.Add(move);
                }
            }
            if (!File.Exists(BeforePendingMoves(MenuPath, Moves.Length)) || !File.Exists(BeforePendingMoves(GamePath, Moves.Length)))
                throw new FileNotFoundException("Menu e Exploration precisam existir antes da organização; nada foi movido.");

            var originalSources = new Dictionary<string, string>();
            var rewrittenSources = new Dictionary<string, string>();
            foreach (string pathOnDisk in Directory.GetFiles("Assets/CRITICAL", "*.cs", SearchOption.AllDirectories))
            {
                string path = pathOnDisk.Replace('\\', '/');
                if (path == OwnPath) continue;
                string source = File.ReadAllText(path);
                string replacement = RewritePaths(source);
                if (replacement == source) continue;
                originalSources.Add(path, source);
                rewrittenSources.Add(path, replacement);
            }

            var completed = new List<Move>();
            EditorBuildSettingsScene[] previousBuildScenes = EditorBuildSettings.scenes;
            SceneAsset previousStartScene = EditorSceneManager.playModeStartScene;
            AssetDatabase.SaveAssets();
            try
            {
                foreach (var source in rewrittenSources) File.WriteAllText(source.Key, source.Value, Utf8);
                foreach (Move move in pending)
                {
                    EnsureFolder(Path.GetDirectoryName(move.to).Replace('\\', '/'));
                    string error = AssetDatabase.MoveAsset(move.from, move.to);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(move.from + " → " + move.to + ": " + error);
                    completed.Add(move);
                    // Only the scene-folder move has a dependent destination. Never force a
                    // recursive reimport of moved scripts or loaded prefab variants.
                    if (move.from == "Assets/Scenes")
                        AssetDatabase.ImportAsset(move.to, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            catch (Exception error)
            {
                var rollbackErrors = new List<string>();
                for (int index = completed.Count - 1; index >= 0; index--)
                {
                    Move move = completed[index];
                    string rollback = AssetDatabase.MoveAsset(move.to, move.from);
                    if (!string.IsNullOrEmpty(rollback)) rollbackErrors.Add(rollback);
                    else if (move.from == "Assets/Scenes")
                        AssetDatabase.ImportAsset(move.from, ImportAssetOptions.ForceSynchronousImport);
                }
                foreach (var source in originalSources) File.WriteAllText(source.Key, source.Value, Utf8);
                EditorBuildSettings.scenes = previousBuildScenes;
                EditorSceneManager.playModeStartScene = previousStartScene;
                WriteReport(false, completed, originalSources, error + (rollbackErrors.Count == 0 ? "" : "\nRollback: " + string.Join("\n", rollbackErrors)));
                throw;
            }
            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true)
            };
            var included = new HashSet<string> { MenuPath, GamePath };
            foreach (EditorBuildSettingsScene scene in previousBuildScenes)
            {
                string path = RewritePaths(scene.path);
                if (included.Add(path) && File.Exists(path)) buildScenes.Add(new EditorBuildSettingsScene(path, scene.enabled));
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
            if (menu == null) throw new IOException("O Menu movido não foi importado: " + MenuPath);
            EditorSceneManager.playModeStartScene = menu;
            WriteReport(true, completed, rewrittenSources, "");
            Debug.Log("CRITICAL: assets organizados, GUIDs preservados; " + completed.Count + " caminhos movidos e " + rewrittenSources.Count + " scripts atualizados. Relatório: " + ReportPath);
        }

        private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

        private static string BeforePendingMoves(string path, int beforeIndex)
        {
            for (int index = beforeIndex - 1; index >= 0; index--)
            {
                Move move = Moves[index];
                if (Exists(move.from) && (path == move.to || path.StartsWith(move.to + "/", StringComparison.Ordinal)))
                    path = move.from + path.Substring(move.to.Length);
            }
            return path;
        }

        private static string RewritePaths(string source)
        {
            foreach (Move move in Moves) source = source.Replace(move.from, move.to);
            // The existing scene builder shares its Root constant with its prefabs and tiles.
            source = source.Replace("Root + \"/Scenes/Exploration.unity\"", "\"" + GamePath + "\"");
            source = source.Replace("EnsureFolders(Root + \"/Scenes\")", "EnsureFolders(\"Assets/CRITICAL/Scenes\")");
            return source;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (Directory.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.IsValidFolder(path)) return;
                throw new IOException("A pasta existe, mas sua importação não forneceu um GUID: " + path);
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Não foi possível criar a pasta: " + path);
        }

        private static void WriteReport(bool success, List<Move> completed, Dictionary<string, string> scripts, string error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report
            {
                success = success, completedAtUtc = DateTime.UtcNow.ToString("O"), error = error,
                movedPaths = completed.Count, rewrittenScripts = scripts.Count,
                moves = completed.ToArray(), scripts = new List<string>(scripts.Keys).ToArray()
            }, true), Utf8);
        }
    }
}
