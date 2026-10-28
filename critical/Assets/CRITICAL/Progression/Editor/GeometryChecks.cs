using System;
using System.IO;
using Critical.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Critical.Progression.Editor
{
    public static class GeometryChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int cases;
            public string completedAtUtc, error;
        }

        [MenuItem("Tools/CRITICAL/Run Combat Geometry Checks")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Combat geometry checks require Edit Mode.");
            var report = new Report { error = "" };
            GameObject character = null, terrain = null;
            try
            {
                Vector2 start = new Vector2(9998f, 10000f);
                Vector2 end = new Vector2(10002f, 10000f);
                character = new GameObject("Geometry check character (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                character.transform.position = new Vector3(10000f, 10000f);
                Physics2D.SyncTransforms();
                Check(CombatVisibility.IsClear(start, end), "Empty path is clear.", report);

                var characterCollider = character.AddComponent<BoxCollider2D>();
                characterCollider.size = Vector2.one;
                Physics2D.SyncTransforms();
                Check(!CombatVisibility.IsClear(start, end), "Solid geometry blocks a melee line.", report);

                characterCollider.isTrigger = true;
                Physics2D.SyncTransforms();
                Check(CombatVisibility.IsClear(start, end), "Triggers do not block combat visibility.", report);

                characterCollider.isTrigger = false;
                character.AddComponent<PlayerMovement>();
                Physics2D.SyncTransforms();
                Check(CombatVisibility.IsClear(start, end), "A player collider does not hide solid terrain.", report);

                terrain = new GameObject("Geometry check wall (temporary)") { hideFlags = HideFlags.HideAndDontSave };
                terrain.transform.position = new Vector3(10001f, 10000f);
                var terrainCollider = terrain.AddComponent<BoxCollider2D>();
                terrainCollider.size = new Vector2(0.3f, 1f);
                Physics2D.SyncTransforms();
                Check(!CombatVisibility.IsClear(start, end), "Terrain behind an ignored character still blocks the line.", report);

                terrain.transform.position = new Vector3(10001f, 10000.5f);
                terrainCollider.size = new Vector2(0.3f, 0.2f);
                Physics2D.SyncTransforms();
                Check(CombatVisibility.IsClear(start, end), "Offset terrain leaves the center line clear.", report);
                Check(!CombatVisibility.HasClearance(start, end, 0.5f), "Movement clearance rejects a wall at the body's edge.", report);
                Check(CombatVisibility.HasClearance(start, end, 0f), "Zero-radius clearance matches the unobstructed center line.", report);
                report.passed = true;
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                throw;
            }
            finally
            {
                if (character != null) Object.DestroyImmediate(character);
                if (terrain != null) Object.DestroyImmediate(terrain);
                Physics2D.SyncTransforms();
                report.completedAtUtc = DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory("Library/CRITICAL");
                File.WriteAllText("Library/CRITICAL/GeometryChecks.json", JsonUtility.ToJson(report, true));
            }
            Debug.Log("CRITICAL: " + report.cases + " combat geometry checks passed (solid walls, triggers, characters and body clearance).");
        }

        private static void Check(bool condition, string message, Report report)
        {
            report.cases++;
            if (!condition) throw new InvalidOperationException("Combat geometry check failed: " + message);
        }
    }
}
