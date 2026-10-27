using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Critical.Procedural.Editor
{
    public static class ExplorationSceneSetup
    {
        private const string Root = "Assets/CRITICAL/Procedural";
        private const string ScenePath = Root + "/Scenes/Exploration.unity";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string TileFolder = Root + "/Tiles";
        private const string AnimationFolder = Root + "/Animations";
        private const string OriginalPlayer = "Assets/_Scripts/Prefabs/Player.prefab";
        private const string AtlasTiles = "Assets/Arte/Tilemap/DungeonTiles/DungeonMapV3_";
        private const string DoorTexture = "Assets/Arte/DungeonDoorAnimationV1.png";
        private const string DiceAnimation = "Assets/Arte/Animações/D20 roll.anim";
        private const string LitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("Tools/CRITICAL/Build Exploration Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Execute o montador fora do Play Mode e após a compilação.");
            Required<GameObject>(OriginalPlayer);
            Required<AnimationClip>(DiceAnimation);
            Material lit = Required<Material>(LitMaterial);
            Material unlit = Required<Material>(UnlitMaterial);
            Sprite[] doorSprites = AssetDatabase.LoadAllAssetsAtPath(DoorTexture).OfType<Sprite>().ToArray();
            if (doorSprites.Length != 10)
                throw new InvalidOperationException("A porta original precisa manter os dez sprites de 32 x 32 já recortados pela equipe.");
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null) font = Required<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            EnsureFolders(PrefabFolder);
            EnsureFolders(TileFolder);
            EnsureFolders(AnimationFolder);
            EnsureFolders(Root + "/Scenes");
            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool createdHere = false;
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Exploration já tem alterações não salvas. O montador não vai sobrescrevê-las.");
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    createdHere = true;
                }
                else scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }
            try
            {
                SceneManager.SetActiveScene(scene);
                RoomTilemapView roomPrefab = CreateRoomPrefab(scene, lit);
                CatacombDoor doorPrefab = CreateDoorPrefab(scene, doorSprites, lit);
                GameObject playerPrefab = CreatePlayerVariant(scene, lit);
                GameObject overlayPrefab = CreateDiceOverlayPrefab(scene, font);
                ComposeScene(scene, playerPrefab, roomPrefab, doorPrefab, overlayPrefab, unlit, font);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Não foi possível salvar a nova cena Exploration.");

                var buildScenes = EditorBuildSettings.scenes.ToList();
                int existing = buildScenes.FindIndex(item => item.path == ScenePath);
                if (existing >= 0) buildScenes[existing].enabled = true;
                else buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
                EditorSceneManager.playModeStartScene = Required<SceneAsset>(ScenePath);
                Debug.Log("Exploration criada em modo aditivo. MainGame e os assets originais não foram salvos nem substituídos.");
            }
            catch
            {
                if (createdHere && scene.IsValid() && scene.isLoaded)
                {
                    try
                    {
                        const string backupFolder = "Library/CRITICAL";
                        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath), backupFolder));
                        string backup = backupFolder + "/FailedBuild_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".unity";
                        if (EditorSceneManager.SaveScene(scene, backup, true))
                        {
                            if (!EditorSceneManager.CloseScene(scene, true))
                                Debug.LogError("A cópia da tentativa falha foi salva, mas a cena nova não pôde ser fechada: " + backup);
                        }
                        else Debug.LogError("Não foi possível salvar a cópia da tentativa falha; a cena nova foi mantida aberta.");
                    }
                    catch (Exception backupError)
                    {
                        Debug.LogError("A cena nova foi mantida aberta porque o backup da tentativa falha não terminou: " + backupError.Message);
                    }
                }
                throw;
            }
            finally
            {
                // Restore the user's active editing scene while keeping the new scene loaded.
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
            }
        }

        private static RoomTilemapView CreateRoomPrefab(Scene scene, Material material)
        {
            GameObject temporary = new GameObject("RoomGrid", typeof(Grid), typeof(RoomTilemapView));
            SceneManager.MoveGameObjectToScene(temporary, scene);
            try
            {
                temporary.GetComponent<Grid>().cellSize = new Vector3(1f, 1f, 0f);
                RoomTilemapView view = temporary.GetComponent<RoomTilemapView>();
                view.ground = CreateMap(temporary.transform, "N-Colisão-Baixa", 0, false, material);
                view.collisionLow = CreateMap(temporary.transform, "Colisão-Baixa", 0, true, material);
                view.collisionHigh = CreateMap(temporary.transform, "Colisão-Alta", 10, true, material);
                view.decorationHigh = CreateMap(temporary.transform, "N-Colisão-Alta", 10, false, material);
                view.decorations = CreateMap(temporary.transform, "Decorações", 2, false, material);
                // Flat stone cells verified in the source atlas; raised circles are excluded.
                view.floorTiles = new TileBase[] { Required<Tile>(AtlasTiles + "11.asset"), Required<Tile>(AtlasTiles + "13.asset"), Required<Tile>(AtlasTiles + "50.asset"), Required<Tile>(AtlasTiles + "51.asset") };
                view.northWall = WallTile("North", 1);
                view.southWall = WallTile("South", 57);
                view.westWall = WallTile("West", 8);
                view.eastWall = WallTile("East", 15);
                view.northWestCorner = WallTile("NorthWest", 0);
                view.northEastCorner = WallTile("NorthEast", 7);
                view.southWestCorner = WallTile("SouthWest", 56);
                view.southEastCorner = WallTile("SouthEast", 61);
                view.voidTile = VoidTile();
                return SavePrefab(temporary, PrefabFolder + "/RoomGrid.prefab").GetComponent<RoomTilemapView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static Tilemap CreateMap(Transform parent, string name, int order, bool collision, Material material)
        {
            var child = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            child.transform.SetParent(parent, false);
            child.layer = 0;
            TilemapRenderer renderer = child.GetComponent<TilemapRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = order;
            renderer.sharedMaterial = material;
            if (collision) child.AddComponent<TilemapCollider2D>();
            return child.GetComponent<Tilemap>();
        }

        private static Tile WallTile(string name, int originalIndex)
        {
            string path = TileFolder + "/" + name + ".asset";
            Tile source = Required<Tile>(AtlasTiles + originalIndex + ".asset");
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            bool fresh = tile == null;
            if (fresh) tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = source.sprite;
            tile.color = source.color;
            tile.transform = source.transform;
            tile.flags = source.flags;
            tile.colliderType = Tile.ColliderType.Grid;
            if (fresh) AssetDatabase.CreateAsset(tile, path);
            else { EditorUtility.SetDirty(tile); AssetDatabase.SaveAssetIfDirty(tile); }
            return tile;
        }

        private static Tile VoidTile()
        {
            const string name = "CollidableVoid";
            string path = TileFolder + "/" + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            bool fresh = tile == null;
            if (fresh) tile = ScriptableObject.CreateInstance<Tile>();
            Tile original = AssetDatabase.LoadAssetAtPath<Tile>(AtlasTiles + "63.asset");
            tile.name = name;
            tile.sprite = original != null ? original.sprite : null;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            if (fresh) AssetDatabase.CreateAsset(tile, path);
            else { EditorUtility.SetDirty(tile); AssetDatabase.SaveAssetIfDirty(tile); }
            return tile;
        }

        private static CatacombDoor CreateDoorPrefab(Scene scene, Sprite[] sprites, Material material)
        {
            GameObject temporary = new GameObject("CatacombDoor", typeof(CatacombDoor));
            SceneManager.MoveGameObjectToScene(temporary, scene);
            try
            {
                CatacombDoor door = temporary.GetComponent<CatacombDoor>();
                door.leftFrames = new Sprite[5];
                door.rightFrames = new Sprite[5];
                for (int frame = 0; frame < 5; frame++)
                {
                    door.leftFrames[frame] = NamedSprite(sprites, "DungeonDoorAnimationV1_" + frame * 2);
                    door.rightFrames[frame] = NamedSprite(sprites, "DungeonDoorAnimationV1_" + (frame * 2 + 1));
                }
                door.leftHalf = DoorHalf(temporary.transform, "LeftHalf", -0.5f, door.leftFrames[0], material);
                door.rightHalf = DoorHalf(temporary.transform, "RightHalf", 0.5f, door.rightFrames[0], material);
                BoxCollider2D blocker = temporary.AddComponent<BoxCollider2D>();
                blocker.size = new Vector2(2f, 1f);
                door.blocker = blocker;
                door.passageTrigger = temporary.AddComponent<BoxCollider2D>();
                door.passageTrigger.size = new Vector2(2f, 1f);
                door.passageTrigger.isTrigger = true;
                door.passageTrigger.enabled = false;
                door.frameDuration = 0.08f;
                return SavePrefab(temporary, PrefabFolder + "/CatacombDoor.prefab").GetComponent<CatacombDoor>();
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static SpriteRenderer DoorHalf(Transform parent, string name, float x, Sprite sprite, Material material)
        {
            GameObject half = new GameObject(name, typeof(SpriteRenderer));
            half.transform.SetParent(parent, false);
            half.transform.localPosition = new Vector3(x, 0f, 0f);
            SpriteRenderer renderer = half.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 8;
            return renderer;
        }

        private static GameObject CreatePlayerVariant(Scene scene, Material material)
        {
            GameObject temporary = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>(OriginalPlayer), scene);
            try
            {
                temporary.name = "ExplorationPlayer";
                PlayerMovement movement = temporary.GetComponent<PlayerMovement>();
                if (movement == null) throw new InvalidOperationException("O prefab original não contém PlayerMovement.");
                PlayerHealth health = temporary.GetComponent<PlayerHealth>();
                if (health != null) health.enabled = false;
                foreach (SpriteRenderer renderer in temporary.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.sortingOrder = 5;
                    renderer.sortingLayerName = "Default";
                    renderer.sharedMaterial = material;
                }
                if (health != null) PrefabUtility.RecordPrefabInstancePropertyModifications(health);
                foreach (SpriteRenderer renderer in temporary.GetComponentsInChildren<SpriteRenderer>(true))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                GameObject lantern = new GameObject("PlayerLight", typeof(Light2D));
                lantern.transform.SetParent(temporary.transform, false);
                lantern.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                Light2D light = lantern.GetComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(1f, 0.77f, 0.44f);
                light.intensity = 0.9f;
                light.pointLightInnerRadius = 1.2f;
                light.pointLightOuterRadius = 5f;
                light.falloffIntensity = 0.65f;
                return SavePrefab(temporary, PrefabFolder + "/ExplorationPlayer.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static GameObject CreateDiceOverlayPrefab(Scene scene, TMP_FontAsset font)
        {
            AnimationClip source = Required<AnimationClip>(DiceAnimation);
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(source);
            EditorCurveBinding originalBinding = bindings.FirstOrDefault(binding => binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite");
            if (originalBinding.type != typeof(SpriteRenderer))
                throw new InvalidOperationException("Não foi encontrada a curva SpriteRenderer.m_Sprite do D20 original.");
            ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(source, originalBinding);
            if (frames == null || frames.Length == 0) throw new InvalidOperationException("A animação original do D20 não contém os quadros de SpriteRenderer esperados.");
            string clipPath = AnimationFolder + "/D20_UI.anim";
            AnimationClip uiClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool fresh = uiClip == null;
            if (fresh) uiClip = new AnimationClip();
            uiClip.name = "D20 UI";
            uiClip.frameRate = source.frameRate;
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(uiClip))
                AnimationUtility.SetObjectReferenceCurve(uiClip, binding, null);
            AnimationUtility.SetObjectReferenceCurve(uiClip,
                EditorCurveBinding.PPtrCurve("", typeof(UnityEngine.UI.Image), "m_Sprite"), frames);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(source);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(uiClip, clipSettings);
            if (fresh) AssetDatabase.CreateAsset(uiClip, clipPath);
            else { EditorUtility.SetDirty(uiClip); AssetDatabase.SaveAssetIfDirty(uiClip); }

            string controllerPath = AnimationFolder + "/D20_UI.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "D20 Roll");
            if (state == null) state = machine.AddState("D20 Roll");
            state.motion = uiClip;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);

            GameObject temporary = CanvasObject("DarknessOverlay", scene);
            try
            {
                CanvasGroup group = temporary.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.blocksRaycasts = true;
                UnityEngine.UI.Image black = temporary.AddComponent<UnityEngine.UI.Image>();
                black.color = Color.black;
                var imageObject = new GameObject("D20", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Animator));
                imageObject.transform.SetParent(temporary.transform, false);
                RectTransform imageRect = imageObject.GetComponent<RectTransform>();
                imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = new Vector2(0f, 45f);
                imageRect.sizeDelta = new Vector2(260f, 260f);
                UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
                image.sprite = frames[0].value as Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                Animator animator = imageObject.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                TMP_Text number = AddText(temporary.transform, "PLACEHOLDER_DiceResult", "", font, 64f);
                RectTransform numberRect = number.rectTransform;
                numberRect.anchorMin = numberRect.anchorMax = new Vector2(0.5f, 0.5f);
                numberRect.anchoredPosition = new Vector2(0f, -155f);
                numberRect.sizeDelta = new Vector2(600f, 100f);
                DiceRoller roller = temporary.AddComponent<DiceRoller>();
                roller.animator = animator;
                roller.resultText = number;
                roller.rollDuration = Mathf.Max(0.1f, source.length + 1f / Mathf.Max(1f, source.frameRate));
                roller.resultHold = 0.8f;
                return SavePrefab(temporary, PrefabFolder + "/DiceOverlay.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static void ComposeScene(Scene scene, GameObject playerPrefab, RoomTilemapView roomPrefab,
            CatacombDoor doorPrefab, GameObject overlayPrefab, Material unlit, TMP_FontAsset font)
        {
            GameObject player = Instance(scene, "ExplorationPlayer", playerPrefab);
            player.transform.position = new Vector3(0f, -3f, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            GameObject cameraObject = SceneRoot(scene, "Exploration Camera");
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null) camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, -3f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            GameObject roomLight = SceneRoot(scene, "RoomLight");
            // MainGame can remain loaded for editing with its own global light.
            // The exploration flow enables this light only in its play-mode scene.
            Light2D ambient = roomLight.GetComponent<Light2D>();
            if (ambient != null) ambient.lightType = Light2D.LightType.Point;
            roomLight.SetActive(true);
            if (ambient == null) ambient = roomLight.AddComponent<Light2D>();
            ambient.intensity = 0.45f;
            ambient.color = new Color(0.65f, 0.72f, 1f);
            // URP 14 initializes the sorting-layer array in Awake on the active object.
            // Deregister the point light before changing it to a global light.
            roomLight.SetActive(false);
            ambient.lightType = Light2D.LightType.Global;

            GameObject outside = SceneRoot(scene, "OutsideArea");
            Transform grass = outside.transform.Find("PLACEHOLDER_OutsideGround");
            if (grass == null)
            {
                var ground = new GameObject("PLACEHOLDER_OutsideGround", typeof(SpriteRenderer));
                ground.transform.SetParent(outside.transform, false);
                grass = ground.transform;
            }
            grass.position = new Vector3(0f, -3f, 0f);
            SpriteRenderer groundRenderer = grass.GetComponent<SpriteRenderer>();
            groundRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (groundRenderer.sprite == null) throw new InvalidOperationException("O sprite interno para o cenário externo provisório não foi encontrado.");
            Vector3 nativeSize = groundRenderer.sprite.bounds.size;
            if (nativeSize.x <= 0f || nativeSize.y <= 0f)
                throw new InvalidOperationException("O sprite externo provisório tem dimensões inválidas.");
            grass.localScale = new Vector3(24f / nativeSize.x, 18f / nativeSize.y, 1f);
            groundRenderer.color = new Color(0.15f, 0.32f, 0.18f);
            groundRenderer.sharedMaterial = unlit;
            groundRenderer.sortingOrder = -20;

            GameObject statisticsCanvas;
            Transform statsRoot = outside.transform.Find("OutsideStatistics");
            if (statsRoot == null)
            {
                statisticsCanvas = CanvasObject("OutsideStatistics", scene);
                statisticsCanvas.transform.SetParent(outside.transform, false);
            }
            else statisticsCanvas = statsRoot.gameObject;
            TMP_Text stats = statisticsCanvas.GetComponentInChildren<TMP_Text>(true);
            if (stats == null) stats = AddText(statisticsCanvas.transform, "PLACEHOLDER_RunStats", "ESTATÍSTICAS DE RUNS\nEspaço reservado", font, 24f);
            stats.rectTransform.anchorMin = new Vector2(0.02f, 1f);
            stats.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            stats.rectTransform.pivot = new Vector2(0.5f, 1f);
            stats.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            stats.rectTransform.sizeDelta = new Vector2(0f, 90f);

            CatacombDoor entrance = Instance(scene, "OutsideDoor", doorPrefab.gameObject).GetComponent<CatacombDoor>();
            entrance.transform.position = new Vector3(0f, 2f, 0f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(entrance.transform);
            GameObject overlay = Instance(scene, "DarknessOverlay", overlayPrefab);
            overlay.GetComponent<Canvas>().sortingOrder = 50;
            overlay.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(overlay.GetComponent<Canvas>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(overlay);
            GameObject flowObject = SceneRoot(scene, "ExplorationFlow");
            ExplorationFlow flow = flowObject.GetComponent<ExplorationFlow>();
            if (flow == null) flow = flowObject.AddComponent<ExplorationFlow>();
            flow.player = player.GetComponent<PlayerMovement>();
            flow.playerBody = player.GetComponent<Rigidbody2D>();
            flow.camera = camera;
            flow.roomPrefab = roomPrefab;
            flow.doorPrefab = doorPrefab;
            flow.outsideDoor = entrance;
            flow.outsideArea = outside;
            flow.darknessOverlay = overlay;
            flow.overlayGroup = overlay.GetComponent<CanvasGroup>();
            flow.diceRoller = overlay.GetComponent<DiceRoller>();
            flow.playerLight = player.transform.Find("PlayerLight").gameObject;
            flow.roomLight = roomLight;
            flow.runStatsPlaceholder = stats;
            EditorUtility.SetDirty(flow);
            GameObject events = SceneRoot(scene, "Exploration EventSystem");
            if (events.GetComponent<EventSystem>() == null) events.AddComponent<EventSystem>();
            if (events.GetComponent<StandaloneInputModule>() == null) events.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CanvasObject(string name, Scene scene)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject;
        }

        private static TMP_Text AddText(Transform parent, string name, string value, TMP_FontAsset font, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static GameObject Instance(Scene scene, string name, GameObject prefab)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            return instance;
        }

        private static GameObject SceneRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static GameObject SavePrefab(GameObject contents, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
            if (!success || prefab == null) throw new InvalidOperationException("Não foi possível salvar o prefab próprio: " + path);
            return prefab;
        }

        private static Sprite NamedSprite(Sprite[] sprites, string name)
        {
            Sprite sprite = sprites.FirstOrDefault(candidate => candidate.name == name);
            if (sprite == null) throw new InvalidOperationException("Quadro original da porta não encontrado: " + name);
            return sprite;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Asset original necessário não encontrado: " + path);
            return asset;
        }

        private static void EnsureFolders(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
