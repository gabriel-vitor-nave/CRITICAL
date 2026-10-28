using System;
using System.Collections.Generic;
using System.IO;
using Critical.Progression.Editor;
using Critical.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Critical.Editor
{
    /// <summary>Batch-only project verification. Never operates a desktop Editor.</summary>
    [InitializeOnLoad]
    public static class HeadlessValidation
    {
        private const string ActiveKey = "CRITICAL.HeadlessValidation.Active";
        private const string StageKey = "CRITICAL.HeadlessValidation.Stage";
        private const string ReportPath = "Library/CRITICAL/HeadlessValidation.json";
        private const string SmokePath = "Library/CRITICAL/GameLoopChecks.json";
        private const string MenuPath = "Assets/CRITICAL/Scenes/Menu.unity";
        private const double TimeoutSeconds = 180d;
        private enum Stage { None, AwaitPlay, Smoke, Ending }

        [Serializable] private sealed class ScriptBinding { public string path, className; public bool valid; }
        [Serializable] private sealed class Report
        {
            public bool running, passed, staticPassed, smokePassed;
            public string startedAtUtc, completedAtUtc, stage, error;
            public ScriptBinding[] scripts;
        }
        [Serializable] private sealed class SmokeReport { public bool passed, running; public string error; }
        private static Report report;
        private static bool subscribed;

        static HeadlessValidation()
        {
            if (Application.isBatchMode && SessionState.GetBool(ActiveKey, false))
                EditorApplication.delayCall += Resume;
        }

        // Invoke without -quit. The driver exits its own batch process after Play Mode checks.
        public static void Run()
        {
            RequireBatch();
            if (SessionState.GetBool(ActiveKey, false))
                throw new InvalidOperationException("Headless validation is already active in this batch session.");
            report = NewReport();
            SessionState.SetBool(ActiveKey, true);
            try
            {
                ValidateStatic();
                EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);
                SetStage(Stage.AwaitPlay);
                Subscribe();
                EditorApplication.isPlaying = true;
            }
            catch (Exception error) { Fail(error); }
        }

        // Synchronous entry, suitable for a batch invocation using -quit.
        public static void RunStatic()
        {
            RequireBatch();
            report = NewReport();
            try
            {
                ValidateStatic();
                report.passed = true;
                report.running = false;
                report.stage = "static-complete";
                report.completedAtUtc = DateTime.UtcNow.ToString("O");
                WriteReport();
            }
            catch (Exception error)
            {
                report.running = false;
                report.passed = false;
                report.error = error.ToString();
                report.completedAtUtc = DateTime.UtcNow.ToString("O");
                WriteReport();
                throw;
            }
        }

        private static Report NewReport()
        {
            return new Report { running = true, startedAtUtc = DateTime.UtcNow.ToString("O"), stage = "static", error = "" };
        }

        private static void ValidateStatic()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start headless validation outside Play Mode.");
            MenuGameSetup.ConfigureBuildSettings();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
            var paths = new List<string>();
            if (Directory.Exists("Assets/Menu/Scripts"))
                paths.AddRange(Directory.GetFiles("Assets/Menu/Scripts", "*.cs", SearchOption.TopDirectoryOnly));
            else throw new DirectoryNotFoundException("The original Menu script folder is missing: Assets/Menu/Scripts");
            paths.AddRange(new[] { "Assets/_Scripts/PlayerMovement.cs", "Assets/_Scripts/PlayerHealth.cs", "Assets/_Scripts/Player_Combat.cs" });
            paths.Sort(StringComparer.Ordinal);
            var bindings = new List<ScriptBinding>();
            foreach (string diskPath in paths)
            {
                string path = diskPath.Replace('\\', '/');
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Type type = script != null ? script.GetClass() : null;
                if (script != null && type == null)
                {
                    // Repair the importer record after a GUID-preserving move. Import only
                    // this script, before any scene or prefab is loaded for validation.
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    type = script != null ? script.GetClass() : null;
                }
                bindings.Add(new ScriptBinding { path = path, className = type?.FullName ?? "", valid = type != null });
            }
            report.scripts = bindings.ToArray();
            WriteReport();
            foreach (var binding in bindings)
                if (!binding.valid) throw new InvalidOperationException("MonoScript has no bound class: " + binding.path);
            ProgressionChecks.Run();
            GeometryChecks.Run();
            FoundationChecks.Run();
            report.staticPassed = true;
            WriteReport();
        }

        private static void Resume()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportPath));
                if (report == null) throw new InvalidOperationException("The headless validation report could not be restored.");
                Subscribe();
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(ActiveKey, false)) return;
            try
            {
                if (DateTime.UtcNow - DateTime.Parse(report.startedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind)
                    > TimeSpan.FromSeconds(TimeoutSeconds))
                    throw new TimeoutException("Headless validation exceeded 180 seconds in " + report.stage + ".");
                EditorApplication.QueuePlayerLoopUpdate();
                Stage stage = (Stage)SessionState.GetInt(StageKey, 0);
                if (stage == Stage.AwaitPlay)
                {
                    if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != SceneTransition.MenuScene
                        || UnityEngine.Object.FindObjectOfType<MenuGameActions>() == null) return;
                    Application.runInBackground = true;
                    GameLoopChecks.StartChecks();
                    SetStage(Stage.Smoke);
                }
                else if (stage == Stage.Smoke)
                {
                    if (GameLoopChecks.IsRunning) return;
                    var smoke = JsonUtility.FromJson<SmokeReport>(File.ReadAllText(SmokePath));
                    if (smoke == null || smoke.running) throw new InvalidOperationException("The Play Mode check stopped without a terminal report.");
                    if (!smoke.passed) throw new InvalidOperationException("Game loop verification failed: " + smoke.error);
                    report.smokePassed = true;
                    SetStage(Stage.Ending);
                    EditorApplication.isPlaying = false;
                }
                else if (stage == Stage.Ending && !EditorApplication.isPlayingOrWillChangePlaymode)
                    Complete();
            }
            catch (Exception error) { Fail(error); }
        }

        private static void SetStage(Stage stage)
        {
            SessionState.SetInt(StageKey, (int)stage);
            report.stage = stage.ToString();
            WriteReport();
        }

        private static void Fail(Exception error)
        {
            RequireBatch();
            if (report == null) report = NewReport();
            report.error = error.ToString();
            report.passed = false;
            SetStage(Stage.Ending);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Subscribe();
                EditorApplication.isPlaying = false;
            }
            else Complete();
        }

        private static void Complete()
        {
            RequireBatch();
            report.running = false;
            report.passed = report.staticPassed && report.smokePassed && string.IsNullOrEmpty(report.error);
            report.completedAtUtc = DateTime.UtcNow.ToString("O");
            report.stage = "complete";
            WriteReport();
            SessionState.SetBool(ActiveKey, false);
            SessionState.EraseInt(StageKey);
            EditorApplication.update -= Tick;
            subscribed = false;
            Debug.Log("CRITICAL headless validation " + (report.passed ? "passed" : "failed") + ". Report: " + ReportPath);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        private static void WriteReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }

        private static void RequireBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("HeadlessValidation only operates its own Unity batch process.");
        }
    }
}
