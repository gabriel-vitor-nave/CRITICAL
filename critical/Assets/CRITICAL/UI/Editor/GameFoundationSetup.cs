using System;
using System.IO;
using System.Linq;
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
    public static class GameFoundationSetup
    {
        private const string GamePath = "Assets/CRITICAL/Scenes/Exploration.unity";
        private const string PlayerPrefab = "Assets/CRITICAL/Procedural/Prefabs/ExplorationPlayer.prefab";
        private const string EnemyPath = "Assets/CRITICAL/Gameplay/Prefabs/Rat.prefab";
        private const string HudPath = "Assets/CRITICAL/UI/Prefabs/RunHUD.prefab";

        [MenuItem("Tools/CRITICAL/Install Game Foundation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Instale a base em Edit Mode após a compilação.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Há uma cena com alterações não salvas. A instalação preservou a cena.");

            MenuGameSetup.Apply();
            ItemDefinition[] catalog = CreateCatalog();
            ConfigurePlayerPrefab(catalog);
            DemoEnemy enemy = CreateEnemyPrefab();
            GameObject hudPrefab = CreateHudPrefab();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(GamePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
            try
            {
                var flow = Find<ExplorationFlow>(scene);
                if (flow == null || flow.player == null) throw new InvalidOperationException("A cena deve conter o ExplorationFlow já configurado.");
                GameObject player = flow.player.gameObject;
                var health = Get<PlayerHealth>(player);
                health.enabled = true;
                health.HP = health.maxHP = 6;
                health.hpText = null;
                Get<Player_Combat>(player);
                var run = Get<RunProgression>(player);
                run.itemCatalog = catalog;
                var encounter = Get<RoomEncounter>(flow.gameObject);
                encounter.flow = flow;
                encounter.enemyPrefab = enemy;
                var hud = Find<GameHud>(scene);
                if (hud == null)
                {
                    var hudObject = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab, scene);
                    hud = hudObject.GetComponent<GameHud>();
                }
                hud.progression = run;
                hud.playerHealth = health;
                hud.flow = flow;
                if (flow.runStatsPlaceholder != null) flow.runStatsPlaceholder.gameObject.SetActive(false);
                foreach (Component component in new Component[] { health, run, encounter, hud })
                {
                    EditorUtility.SetDirty(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Não foi possível salvar a base de Exploration.");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuGameSetup.MenuPath);
                AssetDatabase.SaveAssets();
                Debug.Log("CRITICAL: base instalada. Menu → run; combate, encontros, itens, XP, upgrades, HUD e pausa.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static ItemDefinition[] CreateCatalog()
        {
            const string folder = "Assets/CRITICAL/Progression/Items";
            Folder(folder);
            string[] icons = {
                "Assets/CRITICAL/Art/frasco de pocao vazio.png", "Assets/CRITICAL/Art/Banana-recolored.png",
                "Assets/CRITICAL/Art/bastao quebrado recolored.png", "Assets/CRITICAL/Art/oculos  da perdicao remaked.png",
                "Assets/CRITICAL/Art/tanga do barbaro recolored.png"
            };
            ItemDefinition[] defaults = RunProgression.CreateDefaultCatalog();
            var catalog = new ItemDefinition[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                string path = folder + "/" + defaults[i].Id + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (existing == null)
                {
                    existing = defaults[i];
                    AssetDatabase.CreateAsset(existing, path);
                }
                else UnityEngine.Object.DestroyImmediate(defaults[i]);
                if (existing.Icon == null) existing.Icon = ItemSprite(icons[i]);
                if (existing.Icon == null) throw new InvalidOperationException("Ícone da equipe não encontrado: " + icons[i]);
                EditorUtility.SetDirty(existing);
                catalog[i] = existing;
            }
            return catalog;
        }

        private static void ConfigurePlayerPrefab(ItemDefinition[] catalog)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                var health = Get<PlayerHealth>(root);
                health.enabled = true;
                health.HP = health.maxHP = 6;
                health.hpText = null;
                Get<Player_Combat>(root);
                Get<RunProgression>(root).itemCatalog = catalog;
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static DemoEnemy CreateEnemyPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DemoEnemy>(EnemyPath);
            if (existing != null) return existing;
            Folder(Path.GetDirectoryName(EnemyPath).Replace('\\', '/'));
            var root = new GameObject("Rat");
            try
            {
                var body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var collider = root.AddComponent<CircleCollider2D>();
                collider.radius = 0.22f;
                collider.offset = new Vector2(0, -0.14f);
                var visual = new GameObject("RatVisual");
                visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 6;
                var enemy = root.AddComponent<DemoEnemy>();
                enemy.idleFrames = Sprites("Assets/CRITICAL/Art/Personagens/Rato/RatIdle.png");
                enemy.walkFrames = Sprites("Assets/CRITICAL/Art/Personagens/Rato/RatWalking.png");
                enemy.attackFrames = Sprites("Assets/CRITICAL/Art/Personagens/Rato/RatAttack.png");
                renderer.sprite = enemy.idleFrames.FirstOrDefault();
                if (renderer.sprite == null) throw new InvalidOperationException("Sprites existentes do rato não foram encontrados.");
                return PrefabUtility.SaveAsPrefabAsset(root, EnemyPath).GetComponent<DemoEnemy>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject CreateHudPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (existing != null) return existing;
            Folder(Path.GetDirectoryName(HudPath).Replace('\\', '/'));
            var root = new GameObject("GameHUD", typeof(GameHud));
            try { return PrefabUtility.SaveAsPrefabAsset(root, HudPath); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static T Get<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();
        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static Sprite[] Sprites(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        private static Sprite ItemSprite(string path)
        {
            Sprite sprite = Sprites(path).FirstOrDefault();
            if (sprite != null) return sprite;
            // These item textures were marked Multiple but had no slices at all.
            // Import the whole existing icon without changing pixels or its GUID.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return Sprites(path).FirstOrDefault();
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
