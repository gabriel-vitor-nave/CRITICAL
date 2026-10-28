using System;
using System.Collections.Generic;
using System.IO;
using Critical.Gameplay;
using Critical.Procedural;
using Critical.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Critical.Progression.Editor
{
    public static class GameLoopChecks
    {
        [Serializable] private sealed class Step { public string name, detail; public double elapsedSeconds; }
        [Serializable] private sealed class Report
        {
            public bool passed, running;
            public string startedAtUtc, completedAtUtc, error;
            public double elapsedSeconds;
            public Step[] steps;
            public string[] unexpectedErrors;
        }
        private enum Phase
        {
            Menu, LoadRun, Inventory, CloseInventory, Pause, ClosePause, Upgrade,
            CloseUpgrade, HistoryReview, DropPickup, OutsideDoor, RevealRoom, Attack, Rewards, RoomPickup, Exit, Death, Retry, ReturnMenu
        }
        public static bool IsRunning { get; private set; }
        private const string ReportPath = "Library/CRITICAL/GameLoopChecks.json";
        private static readonly List<Step> steps = new List<Step>();
        private static readonly List<string> errors = new List<string>();
        private static Report report;
        private static Phase phase;
        private static double started, phaseStarted;
        private static int runInstance, goldBeforeRoom, levelBeforeRoom, attackDamage;
        private static GameHud hud;
        private static RunProgression run;
        private static PlayerHealth health;
        private static Player_Combat combat;
        private static ExplorationFlow flow;
        private static RoomEncounter encounter;
        private static DemoEnemy attackedEnemy;
        private static bool attackIssued, enemyDefeatedByAttack;
        private static WorldItemPickup droppedItem, roomItem;
        private static int dropStage, damageBeforeDrop, expectedLootCount;
        private static double dropMovedAt;
        private static string lootId;
        private static bool pauseReferenceCaptured, historyReviewed;
        private static string captureName;
        private static double captureReadyAt;
        private static int captureFrame;

        [MenuItem("Tools/CRITICAL/Run Game Loop Checks (Play Mode from Menu)")]
        public static void StartChecks()
        {
            if (IsRunning) throw new InvalidOperationException("Game loop checks are already running.");
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != SceneTransition.MenuScene)
                throw new InvalidOperationException("Start a fresh Play Mode session from Menu before game loop checks.");
            steps.Clear(); errors.Clear();
            hud = null; run = null; health = null; combat = null; flow = null; encounter = null; attackedEnemy = null;
            runInstance = goldBeforeRoom = levelBeforeRoom = 0;
            attackIssued = enemyDefeatedByAttack = false;
            droppedItem = roomItem = null;
            dropStage = 0;
            pauseReferenceCaptured = historyReviewed = false;
            captureName = null;
            captureFrame = -1;
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), running = true, error = "" };
            started = phaseStarted = EditorApplication.timeSinceStartup;
            phase = Phase.Menu;
            IsRunning = true;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            WriteReport();
        }

        private static void Tick()
        {
            if (!IsRunning) return;
            try
            {
                Require(EditorApplication.isPlaying, "Play Mode stopped during smoke checks.");
                Require(errors.Count == 0, "Unexpected Unity error: " + (errors.Count > 0 ? errors[0] : ""));
                Require(EditorApplication.timeSinceStartup - started < 150d, "Game loop checks exceeded 150 seconds.");
                Require(EditorApplication.timeSinceStartup - phaseStarted < 30d, "Timed out in phase " + phase + ".");
                switch (phase)
                {
                    case Phase.Menu:
                        var actions = Object.FindObjectOfType<MenuGameActions>();
                        if (actions == null || SceneTransition.IsLoading) return;
                        Require(actions.startButton != null && actions.startButton.onClick.GetPersistentEventCount() > 0, "Start button must have a persistent action.");
                        actions.StartRun();
                        Next(Phase.LoadRun, "menu-start", "MenuGameActions.StartRun requested Exploration without physical input.");
                        break;
                    case Phase.LoadRun:
                        if (!ReadyRun()) return;
                        Require(run.Inventory.Count > 0 && run.Inventory[0].Item.Id == "potion", "Run must start with a healing item.");
                        Require(run.Catalog.Count >= 5, "All five item definitions must be installed.");
                        foreach (var item in run.Catalog) Require(item != null && item.Icon != null, "Every catalog item must have its existing art assigned.");
                        Require(health.enabled && !health.IsDead && combat.enabled && flow.player.enabled, "Player components must be active.");
                        CheckMovementRebinding();
                        Require(Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1, "Gameplay requires exactly one EventSystem.");
                        runInstance = run.GetInstanceID();
                        hud.ToggleInventory();
                        Next(Phase.Inventory, "game-loaded", "Exploration, HUD, player components and all five item icons are available.");
                        break;
                    case Phase.Inventory:
                        Require(hud.InventoryOpen && GameHud.IsInputBlocked && Time.timeScale == 0f, "Inventory must pause gameplay and own input.");
                        Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null,
                            "Inventory must retain keyboard focus on a modal control.");
                        if (!CaptureStable("inventory")) return;
                        int beforeCount = run.Inventory[0].Count;
                        int beforeHP = health.HP;
                        health.ChangeHP(-2);
                        Require(health.HP < beforeHP, "Damage must decrease player health.");
                        Require(run.UseItem(0), "The first potion must be usable while the inventory is open.");
                        Require(health.HP == beforeHP && run.Inventory[0].Count == beforeCount - 1, "Potion must restore health and consume exactly one unit.");
                        hud.Resume();
                        Next(Phase.CloseInventory, "inventory-consume", "Inventory pauses; damage and potion consumption update health and counts.");
                        break;
                    case Phase.CloseInventory:
                        if (hud.IsPaused) return;
                        Require(!GameHud.IsInputBlocked && Time.timeScale == 1f, "Closing inventory must restore time and input.");
                        hud.TogglePause();
                        Next(Phase.Pause, "inventory-resume", "Inventory close animation restores the previous time scale.");
                        break;
                    case Phase.Pause:
                        Require(hud.IsPaused && GameHud.IsInputBlocked && Time.timeScale == 0f, "Pause must stop time and block attacks.");
                        if (!pauseReferenceCaptured)
                        {
                            if (!CaptureStable("pause")) return;
                            pauseReferenceCaptured = true;
                            InvokeHudButton("Controls");
                            return;
                        }
                        if (!CaptureStable("controls")) return;
                        var instructions = HudText("ControlsList");
                        Require(instructions != null && instructions.gameObject.activeInHierarchy, "The ESC controls button must expose its instructions.");
                        foreach (string key in new[] { "WASD", "ESPAÇO", "Z / C", "Q", "G", "I / TAB", "ESC", "1 / 2 / 3", "ENTER", "PASSE SOBRE" })
                            Require(instructions.text.Contains(key), "ESC instructions must include: " + key);
                        // Reproduce a live restored instance losing its serialized references.
                        // Dispatch the real physics callback without injecting any input.
                        flow.player.rb = null;
                        flow.player.anim = null;
                        flow.playerBody.velocity = new Vector2(0.5f, 0.25f);
                        flow.player.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
                        Require(flow.player.rb == flow.playerBody && flow.player.anim != null,
                            "A paused FixedUpdate must recover missing component references.");
                        Require(flow.playerBody.velocity == Vector2.zero && errors.Count == 0,
                            "A paused physics tick must stop the body without reference exceptions.");
                        Record("movement-paused-tick", "The real paused FixedUpdate restores null references and stops velocity without errors.");
                        Require(!combat.TryAttack(), "Attacks must be rejected during pause.");
                        hud.Resume();
                        Next(Phase.ClosePause, "pause", "Pause owns time and combat rejects attacks.");
                        break;
                    case Phase.ClosePause:
                        if (hud.IsPaused) return;
                        Require(Time.timeScale == 1f && !GameHud.IsInputBlocked, "Resume must restore gameplay.");
                        int damage = combat.damage;
                        Require(run.AddItem(run.Catalog[2]) && combat.damage == damage + 1, "Passive collection must increase attack damage immediately.");
                        run.GrantExperience(run.ExperienceToNextLevel - run.Experience);
                        Next(Phase.Upgrade, "passive-and-xp", "Collecting the staff changes damage; XP reaches the next level.");
                        break;
                    case Phase.Upgrade:
                        if (!hud.UpgradeOpen) return;
                        Require(Time.timeScale == 0f && run.PendingUpgrades > 0 && run.GetUpgradeChoices().Count == 3, "A level must show three choices while paused.");
                        if (!CaptureStable("upgrades")) return;
                        ChooseUpgradeButton();
                        Require(run.PendingUpgrades == 0, "Selecting an upgrade must spend exactly one pending choice.");
                        Next(Phase.CloseUpgrade, "level-up", "The wired upgrade button consumes the choice.");
                        break;
                    case Phase.CloseUpgrade:
                        if (hud.IsPaused) return;
                        Require(!hud.UpgradeOpen && !GameHud.IsInputBlocked && Time.timeScale == 1f, "Last upgrade must return to the game.");
                        Require(run.AppliedUpgrades.Count == 1, "The selected upgrade must be present in pause history.");
                        if (!historyReviewed)
                        {
                            hud.TogglePause();
                            Next(Phase.HistoryReview, "upgrade-resume", "Upgrade exit restores control and its chosen improvement is retained.");
                            break;
                        }
                        int staffIndex = InventoryIndex("broken-staff");
                        Require(staffIndex >= 0 && run.SelectItem(staffIndex), "The item carousel must select the acquired staff.");
                        CheckHudFocus();
                        var fixedWeapon = run.EquippedWeapon;
                        run.CycleSelectedItem(1);
                        run.CycleSelectedItem(-1);
                        Require(run.SelectedItemIndex == staffIndex && run.EquippedWeapon == fixedWeapon,
                            "Cycling items must wrap and leave the fixed weapon unchanged.");
                        damageBeforeDrop = combat.damage;
                        Require(run.DropSelectedItem(), "The selected item must drop into the world.");
                        foreach (var pickup in Object.FindObjectsOfType<WorldItemPickup>())
                            if (pickup.WasDropped && pickup.Item.Id == "broken-staff") droppedItem = pickup;
                        Require(droppedItem != null && InventoryIndex("broken-staff") < 0 && combat.damage == damageBeforeDrop - 1,
                            "Dropping a passive must remove one item and its bonus, leaving a world pickup.");
                        Next(Phase.DropPickup, "item-carousel", "Item selection cycles independently from the fixed weapon; the selected passive can be dropped.");
                        break;
                    case Phase.HistoryReview:
                        Require(hud.IsPaused, "Reopening ESC must pause the run for upgrade review.");
                        var history = HudText("AppliedUpgradeList");
                        Require(history != null && history.gameObject.activeInHierarchy && history.text.Contains(run.AppliedUpgrades[0].DisplayName),
                            "ESC must display the upgrade actually chosen in this run.");
                        if (!CaptureStable("pause-earned")) return;
                        historyReviewed = true;
                        hud.Resume();
                        Next(Phase.CloseUpgrade, "upgrade-history", "The pause panel lists the chosen upgrade and resumes without losing it.");
                        break;
                    case Phase.DropPickup:
                        if (dropStage == 0)
                        {
                            if (EditorApplication.timeSinceStartup - phaseStarted < 0.55d) return;
                            Require(droppedItem != null && InventoryIndex("broken-staff") < 0,
                                "A dropped item must not recollect while the player is still standing on it, even after its grace period.");
                            MovePlayerCenter((Vector2)droppedItem.transform.position + Vector2.right * 1.5f);
                            dropMovedAt = EditorApplication.timeSinceStartup;
                            dropStage = 1;
                            return;
                        }
                        if (dropStage == 1)
                        {
                            if (EditorApplication.timeSinceStartup - dropMovedAt < 0.12d) return;
                            Require(droppedItem != null && !droppedItem.RequiresOwnerExit, "Leaving the pickup must release its drop gate.");
                            MovePlayerCenter(droppedItem.GetComponent<Collider2D>().bounds.center);
                            dropStage = 2;
                            return;
                        }
                        if (InventoryIndex("broken-staff") < 0) return;
                        Require(combat.damage == damageBeforeDrop && CountItem("broken-staff") == 1,
                            "Walking over a dropped passive must restore exactly one copy and its bonus exactly once.");
                        Next(Phase.OutsideDoor, "drop-and-walkover", "Drop removes the passive; leaving and walking back recollects it without input or duplicate bonuses.");
                        break;
                    case Phase.OutsideDoor:
                        if (!flow.outsideDoor.CanTraverse) return;
                        var collider = flow.player.GetComponent<Collider2D>();
                        Vector3 correction = flow.outsideDoor.passageTrigger.bounds.center - collider.bounds.center;
                        flow.player.transform.position += correction;
                        flow.playerBody.position = flow.player.transform.position;
                        flow.playerBody.velocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                        Require(flow.outsideDoor.TryTraverse(collider), "An overlapping player must traverse the opened outside door.");
                        Next(Phase.RevealRoom, "physical-door", "The player's real collider overlaps and traverses the outside door trigger.");
                        break;
                    case Phase.RevealRoom:
                        if (flow.IsTransitioning || flow.CurrentRoom == null || flow.CurrentRoomIndex != 1) return;
                        if (!CaptureStable("room-hud")) return;
                        encounter = flow.GetComponent<RoomEncounter>();
                        Require(encounter != null && encounter.EnemiesRemaining > 0, "The generated room must spawn an encounter.");
                        Require(flow.playerBody.simulated && flow.player.enabled && !flow.darknessOverlay.activeSelf, "Room reveal must restore player physics and visibility.");
                        Require(!ExitDoor().IsOpen, "Exit must remain sealed while enemies live.");
                        goldBeforeRoom = run.Gold;
                        levelBeforeRoom = run.Level;
                        var enemies = Object.FindObjectsOfType<DemoEnemy>();
                        Require(enemies.Length > 0, "Enemy instances must exist.");
                        attackedEnemy = enemies[0];
                        attackedEnemy.speed = 0f;
                        attackedEnemy.enabled = false;
                        attackDamage = combat.damage;
                        combat.damage = 100;
                        attackedEnemy.Defeated += OnAttackDefeated;
                        Vector2 origin = flow.player.GetComponent<Collider2D>().bounds.center;
                        var enemyBody = attackedEnemy.GetComponent<Rigidbody2D>();
                        enemyBody.position = origin + combat.AimDirection * 0.65f;
                        attackedEnemy.transform.position = enemyBody.position;
                        enemyBody.velocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                        Next(Phase.Attack, "room-encounter", "D20 room reveal spawns threats and seals the exit.");
                        break;
                    case Phase.Attack:
                        if (!attackIssued)
                        {
                            if (!combat.TryAttack()) return;
                            attackIssued = true;
                        }
                        Require(enemyDefeatedByAttack, "TryAttack must find and defeat the enemy via the physics overlap.");
                        combat.damage = attackDamage;
                        foreach (var enemy in Object.FindObjectsOfType<DemoEnemy>())
                            if (enemy.IsAlive) enemy.TakeDamage(1000, Vector2.right);
                        Require(encounter.EnemiesRemaining == 0, "Defeat callbacks must clear the encounter.");
                        Next(Phase.Rewards, "combat", "TryAttack hit a real enemy collider; all defeat callbacks completed.");
                        break;
                    case Phase.Rewards:
                        if (run.PendingUpgrades > 0)
                        {
                            if (!hud.UpgradeOpen) return;
                            ChooseUpgradeButton();
                            return;
                        }
                        if (hud.IsPaused) return;
                        Require(run.Gold > goldBeforeRoom && (run.Level > levelBeforeRoom || run.Experience > 0), "Defeats and room completion must award gold and XP.");
                        foreach (var pickup in Object.FindObjectsOfType<WorldItemPickup>())
                            if (!pickup.WasDropped) { roomItem = pickup; break; }
                        Require(roomItem != null, "Room loot must remain in the world until walked over.");
                        lootId = roomItem.Item.Id;
                        expectedLootCount = CountItem(lootId) + roomItem.Count;
                        MovePlayerCenter(roomItem.GetComponent<Collider2D>().bounds.center);
                        Next(Phase.RoomPickup, "room-rewards", "Gold and XP arrive from the encounter; its item remains on the floor.");
                        break;
                    case Phase.RoomPickup:
                        if (CountItem(lootId) < expectedLootCount) return;
                        Require(CountItem(lootId) == expectedLootCount, "Walking over room loot must collect its exact count once.");
                        Next(Phase.Exit, "loot-walkover", "The room item is collected through the real player trigger overlap, without an interaction key.");
                        break;
                    case Phase.Exit:
                        if (!ExitDoor().IsOpen) return;
                        Require(encounter.EnemiesRemaining == 0 && ExitDoor().CanTraverse, "Cleared room must unlock a traversable exit.");
                        health.invulnerabilityDuration = 0f;
                        if (EditorApplication.timeSinceStartup - phaseStarted < 0.8d) return;
                        health.ChangeHP(-health.maxHP - 1);
                        Next(Phase.Death, "room-exit", "Room exit opens only after the encounter has been cleared.");
                        break;
                    case Phase.Death:
                        if (!health.IsDead || !GameHud.IsInputBlocked || Time.timeScale != 0f) return;
                        Require(!flow.player.enabled && !combat.TryAttack(), "Death must stop movement and combat.");
                        if (!CaptureStable("death")) return;
                        InvokeHudButton("Retry");
                        Next(Phase.Retry, "death", "Death opens the paused result screen and its Retry button starts a new run.");
                        break;
                    case Phase.Retry:
                        if (SceneTransition.IsLoading || !ReadyRun()) return;
                        if (run.GetInstanceID() == runInstance) return;
                        Require(health.HP == health.maxHP && !health.IsDead && run.Level == 1 && run.Gold == 0, "Retry must reset player health and run progression.");
                        Require(Time.timeScale == 1f && !GameHud.IsInputBlocked && flow.CurrentRoomIndex == 0, "Retry must restore time and clear stale modal state.");
                        hud.TogglePause();
                        InvokeHudButton("MainMenu");
                        Next(Phase.ReturnMenu, "retry", "Retry created a healthy new run and reset progression, time and modal state.");
                        break;
                    case Phase.ReturnMenu:
                        if (SceneTransition.IsLoading || SceneManager.GetActiveScene().name != SceneTransition.MenuScene) return;
                        Require(Time.timeScale == 1f && RunProgression.Instance == null && GameHud.Instance == null, "Returning to Menu must release run singletons and paused time.");
                        Record("return-menu", "Menu return releases run/HUD state and restores time.");
                        Finish(true, "");
                        break;
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void CheckMovementRebinding()
        {
            PlayerMovement movement = flow.player;
            Rigidbody2D expectedBody = movement.GetComponent<Rigidbody2D>();
            Animator expectedAnimator = movement.GetComponentInChildren<Animator>(true);
            Require(Time.timeScale == 1f && !GameHud.IsInputBlocked,
                "Movement recovery must be checked while gameplay is unpaused.");
            Require(expectedBody != null && movement.rb == expectedBody && expectedBody == flow.playerBody,
                "PlayerMovement must reference the player's actual Rigidbody2D.");
            Require(expectedAnimator != null && movement.anim == expectedAnimator && expectedAnimator.runtimeAnimatorController != null,
                "PlayerMovement must reference the configured child Animator and controller.");

            movement.rb = null;
            movement.anim = null;
            movement.enabled = false;
            movement.enabled = true;
            Require(movement.rb == expectedBody && movement.anim == expectedAnimator,
                "OnEnable must rebind both references after a live script restoration.");
            movement.SendMessage("FixedUpdate", SendMessageOptions.RequireReceiver);
            Require(movement.enabled && errors.Count == 0,
                "The real unpaused FixedUpdate must execute without reference exceptions.");
            Record("movement-rebind", "Null Rigidbody/Animator references recover on re-enable; the real unpaused physics callback runs without errors.");
        }

        private static bool ReadyRun()
        {
            if (SceneTransition.IsLoading || SceneManager.GetActiveScene().name != SceneTransition.GameScene) return false;
            hud = GameHud.Instance;
            run = RunProgression.Instance;
            flow = Object.FindObjectOfType<ExplorationFlow>();
            if (hud == null || run == null || flow == null) return false;
            health = run.GetComponent<PlayerHealth>();
            combat = run.GetComponent<Player_Combat>();
            return health != null && combat != null && hud.GetComponentInChildren<Canvas>() != null;
        }

        private static int InventoryIndex(string id)
        {
            for (int i = 0; i < run.Inventory.Count; i++) if (run.Inventory[i].Item.Id == id) return i;
            return -1;
        }

        private static TMPro.TMP_Text HudText(string name)
        {
            foreach (var label in hud.GetComponentsInChildren<TMPro.TMP_Text>(true)) if (label.name == name) return label;
            return null;
        }

        private static int CountItem(string id)
        {
            int index = InventoryIndex(id);
            return index >= 0 ? run.Inventory[index].Count : 0;
        }

        private static void MovePlayerCenter(Vector2 center)
        {
            var collider = flow.player.GetComponent<Collider2D>();
            Vector3 correction = (Vector3)center - collider.bounds.center;
            correction.z = 0;
            flow.player.transform.position += correction;
            flow.playerBody.position = flow.player.transform.position;
            flow.playerBody.velocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private static CatacombDoor ExitDoor()
        {
            foreach (var door in Object.FindObjectsOfType<CatacombDoor>())
                if (door != flow.outsideDoor && !door.IsEntry) return door;
            throw new InvalidOperationException("Generated exit door was not found.");
        }

        private static void ChooseUpgradeButton() => InvokeHudButton("Upgrade0");

        private static void CheckHudFocus()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            Require(eventSystem != null && eventSystem.currentSelectedGameObject == null,
                "Returning to gameplay must release the modal keyboard focus.");
            var buttons = hud.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            UnityEngine.UI.Button next = null, previous = null;
            foreach (var button in buttons)
            {
                bool permanent = button.name == "WeaponSlot" || button.name == "DiceSlot" || button.name.StartsWith("QuickSlot", StringComparison.Ordinal);
                if (!permanent) continue;
                Require(button.navigation.mode == UnityEngine.UI.Navigation.Mode.None,
                    "Movement arrows must not navigate the persistent HUD: " + button.name);
                if (button.name == "QuickSlot0") next = button;
                if (button.name == "QuickSlot2") previous = button;
            }
            Require(next != null && previous != null && next.interactable && previous.interactable,
                "The carousel must expose usable next and previous controls.");
            int selected = run.SelectedItemIndex;
            var weapon = run.EquippedWeapon;
            eventSystem.SetSelectedGameObject(next.gameObject);
            next.onClick.Invoke();
            Require(run.SelectedItemIndex == (selected + 1) % run.Inventory.Count && eventSystem.currentSelectedGameObject == null,
                "Using the next carousel slot must advance selection and release Submit focus before attacking.");
            eventSystem.SetSelectedGameObject(previous.gameObject);
            previous.onClick.Invoke();
            Require(run.SelectedItemIndex == selected && run.EquippedWeapon == weapon && eventSystem.currentSelectedGameObject == null,
                "Using the previous slot must restore selection without moving the weapon or retaining Submit focus.");
            Record("hud-keyboard-focus", "Carousel actions clear persistent HUD focus; arrows/Space remain available to gameplay and modal controls retain keyboard focus.");
        }

        private static bool CaptureStable(string name)
        {
            // A -nographics batch has no game render to capture. Keep all gameplay checks;
            // its driver pumps the player loop and the modal state is verified above.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return true;
            // Capture only the Unity game render, after the panel's unscaled entrance animation.
            // Let two rendered frames finish before changing the captured state.
            if (captureName != name)
            {
                captureName = name;
                captureReadyAt = EditorApplication.timeSinceStartup + 0.35d;
                captureFrame = -1;
            }
            if (captureFrame < 0)
            {
                if (EditorApplication.timeSinceStartup >= captureReadyAt)
                {
                    if (Application.isBatchMode)
                    {
                        Critical.Editor.HudReferenceCapture.Capture(name);
                        return true;
                    }
                    Directory.CreateDirectory("Library/CRITICAL/Verification");
                    ScreenCapture.CaptureScreenshot("Library/CRITICAL/Verification/" + name + ".png");
                    captureFrame = Time.frameCount;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                return false;
            }
            if (Time.frameCount <= captureFrame + 1)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                return false;
            }
            return true;
        }

        private static void InvokeHudButton(string name)
        {
            foreach (var button in hud.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name == name) { Require(button.interactable, name + " must be interactable."); button.onClick.Invoke(); return; }
            throw new InvalidOperationException("Active HUD button not found: " + name);
        }

        private static void OnAttackDefeated(DemoEnemy enemy)
        {
            enemyDefeatedByAttack = true;
            enemy.Defeated -= OnAttackDefeated;
        }

        private static void Next(Phase next, string name, string detail)
        {
            Record(name, detail);
            phase = next;
            phaseStarted = EditorApplication.timeSinceStartup;
        }

        private static void Record(string name, string detail)
        {
            steps.Add(new Step { name = name, detail = detail, elapsedSeconds = EditorApplication.timeSinceStartup - started });
            WriteReport();
        }

        private static void OnLog(string condition, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(condition + "\n" + trace);
        }

        private static void Finish(bool passed, string error)
        {
            IsRunning = false;
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            report.running = false;
            report.passed = passed;
            report.error = error;
            report.completedAtUtc = DateTime.UtcNow.ToString("O");
            WriteReport();
            if (passed) Debug.Log("CRITICAL: game loop checks passed. Report: " + ReportPath);
            else Debug.LogWarning("CRITICAL: game loop checks failed in " + phase + ". " + error);
        }

        private static void WriteReport()
        {
            report.elapsedSeconds = EditorApplication.timeSinceStartup - started;
            report.steps = steps.ToArray();
            report.unexpectedErrors = errors.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
