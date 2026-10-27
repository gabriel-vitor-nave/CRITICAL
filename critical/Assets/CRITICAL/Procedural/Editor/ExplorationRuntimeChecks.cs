using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Critical.Procedural.Editor
{
    public static class ExplorationRuntimeChecks
    {
        [Serializable]
        private class Stage
        {
            public string name, detail;
            public bool passed;
            public int room;
            public double elapsedSeconds;
        }

        [Serializable]
        private class RoomResult
        {
            public int index, seed, roll, width, height, floorCells, wallCells, voidCells;
            public string entry, exit;
            public bool diceAnimationAdvanced;
        }

        [Serializable]
        private class Report
        {
            public bool passed;
            public string startedAtUtc, error;
            public double elapsedSeconds;
            public int physicalTraversals, rollEvents, fixedRollsChecked, failureCount;
            public Stage[] stages;
            public RoomResult[] rooms;
            public string[] observedOrientations, limitations;
        }

        private enum Phase { Initial, DoorOpening, Traversal, Reveal }
        private static readonly List<Stage> Stages = new List<Stage>();
        private static readonly List<RoomResult> Rooms = new List<RoomResult>();
        private static readonly HashSet<DoorDirection> Orientations = new HashSet<DoorDirection>();
        private static Report report;
        private static ExplorationFlow flow;
        private static CatacombDoor watchedDoor;
        private static RoomLayout previousLayout;
        private static RoomResult pendingRoom;
        private static GameObject temporaryDiceRoot;
        private static Phase phase;
        private static double started, phaseStarted;
        private static float animationTime;
        private static bool animationAdvanced, subscribed;
        public static bool IsRunning { get; private set; }

        public static void StartChecks()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("ExplorationRuntimeChecks requires Play Mode in the Exploration scene.");
            if (IsRunning) throw new InvalidOperationException("Exploration runtime checks are already running.");
            Stages.Clear();
            Rooms.Clear();
            Orientations.Clear();
            flow = null;
            watchedDoor = null;
            previousLayout = null;
            pendingRoom = null;
            subscribed = false;
            report = new Report { startedAtUtc = DateTime.UtcNow.ToString("O"), error = "" };
            started = phaseStarted = EditorApplication.timeSinceStartup;
            phase = Phase.Initial;
            IsRunning = true;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!IsRunning) return;
            try
            {
                Require(EditorApplication.isPlaying, "Play Mode ended before the checks completed.");
                Require(EditorApplication.timeSinceStartup - started < 70d, "Exploration runtime checks timed out after 70 seconds.");
                if (phase == Phase.Initial)
                {
                    flow = Object.FindObjectOfType<ExplorationFlow>();
                    if (flow == null) return;
                    Require(flow.enabled, "ExplorationFlow is disabled; inspect required references/Console.");
                    Require(flow.CurrentRoomIndex == 0 && flow.CurrentRoom == null, "Start from a fresh Exploration Play Mode session.");
                    Require(flow.player != null && flow.playerBody != null && flow.outsideArea != null && flow.outsideDoor != null
                        && flow.diceRoller != null, "The external area/player/door/D20 references must exist.");
                    if (!flow.outsideArea.activeInHierarchy || !flow.player.isActiveAndEnabled) return;
                    Require(flow.playerBody.simulated, "The player must be physically active outside the catacomb.");
                    Require(ActiveViews().Length == 0, "No generated room should exist before the external door is crossed.");
                    Pass("external_area_ready", "External area, player and door exist before any room generation.");
                    flow.RoomGenerated += OnGenerated;
                    flow.RoomRevealed += OnRevealed;
                    flow.diceRoller.RollCompleted += OnRollCompleted;
                    subscribed = true;
                    watchedDoor = flow.outsideDoor;
                    SetPhase(Phase.DoorOpening);
                }
                else if (phase == Phase.DoorOpening)
                {
                    Require(watchedDoor != null, "The next exit door disappeared.");
                    if (!watchedDoor.CanTraverse)
                    {
                        Require(EditorApplication.timeSinceStartup - phaseStarted < 8d, "Door opening did not finish.");
                        return;
                    }
                    Require(watchedDoor.leftHalf.sprite == watchedDoor.leftFrames[4]
                        && watchedDoor.rightHalf.sprite == watchedDoor.rightFrames[4], "The open door must display the final animation frames.");
                    watchedDoor.Traversed += OnPhysicalTraversal;
                    SetPhase(Phase.Traversal);
                    MovePlayerInto(watchedDoor);
                }
                else if (phase == Phase.Traversal)
                {
                    Require(EditorApplication.timeSinceStartup - phaseStarted < 5d, "Moving the Rigidbody into the trigger did not produce a real traversal.");
                }
                else if (phase == Phase.Reveal)
                {
                    if (flow.diceRoller.animator.enabled)
                    {
                        float current = flow.diceRoller.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        if (Mathf.Abs(current - animationTime) > 0.01f) animationAdvanced = true;
                    }
                    Require(EditorApplication.timeSinceStartup - phaseStarted < 15d, "D20 generation/reveal did not complete.");
                }
            }
            catch (Exception error) { Finish(error); }
        }

        private static void MovePlayerInto(CatacombDoor door)
        {
            Collider2D playerCollider = PlayerCollider();
            Vector2 offset = (Vector2)playerCollider.bounds.center - flow.playerBody.position;
            flow.playerBody.velocity = Vector2.zero;
            flow.playerBody.position = (Vector2)door.passageTrigger.bounds.center - offset;
            Physics2D.SyncTransforms();
            Require(door.passageTrigger.Distance(playerCollider).isOverlapped, "The test body must overlap the actual passage trigger.");
        }

        private static void OnPhysicalTraversal(CatacombDoor door)
        {
            Observe(() =>
            {
                Require(door == watchedDoor && phase == Phase.Traversal, "An unexpected door was traversed.");
                watchedDoor.Traversed -= OnPhysicalTraversal;
                report.physicalTraversals++;
                Require(flow.IsTransitioning && flow.CurrentRoom == null, "The old room must be cleared before the roll starts.");
                Require(flow.darknessOverlay.activeInHierarchy && flow.overlayGroup.alpha >= 0.99f, "The transition must be fully black.");
                Require(!flow.player.enabled && !flow.playerBody.simulated, "Movement and player physics must be disabled at the black gate.");
                Require(!flow.playerLight.activeInHierarchy && !flow.roomLight.activeInHierarchy, "Both lights must be off before generation.");
                Require(!flow.outsideArea.activeInHierarchy && !flow.outsideDoor.gameObject.activeInHierarchy, "The external area cannot remain accessible.");
                RoomTilemapView[] views = ActiveViews();
                Require(views.Length <= 1, "Transitions cannot accumulate room views.");
                if (previousLayout != null)
                {
                    Require(views.Length == 1, "The previous room view must remain available for a clear/rebuild.");
                    for (int y = 0; y < previousLayout.Height; y++)
                        for (int x = 0; x < previousLayout.Width; x++)
                        {
                            var cell = new Vector3Int(x, y, 0);
                            Require(!views[0].ground.HasTile(cell) && !views[0].collisionLow.HasTile(cell)
                                && !views[0].collisionHigh.HasTile(cell), "Previous-room tiles survived into the black gate.");
                        }
                }
                Require(flow.diceRoller.animator.enabled && flow.diceRoller.animator.runtimeAnimatorController != null,
                    "The real D20 Animator must be active during the black gate.");
                animationTime = flow.diceRoller.animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                animationAdvanced = false;
                pendingRoom = null;
                Pass("physical_door_black_gate", "Native trigger entered; old room cleared, player locked, overlay black and lights off.");
                SetPhase(Phase.Reveal);
            });
        }

        private static void OnRollCompleted(int result)
        {
            Observe(() =>
            {
                Require(phase == Phase.Reveal && result >= 1 && result <= 20, "The real roll must finish with a D20 result.");
                Require(flow.diceRoller.LastRoll == result && flow.diceRoller.resultText.text == result.ToString(), "D20 text/event/result must agree.");
                Require(!flow.diceRoller.animator.enabled, "The D20 Animator must stop before its result is displayed.");
                report.rollEvents++;
                Pass("dice_result", "Real roll event and final text match result " + result + ".");
            });
        }

        private static void OnGenerated(RoomLayout layout)
        {
            Observe(() =>
            {
                Require(phase == Phase.Reveal && report.rollEvents == Rooms.Count + 1, "Room generation must follow the completed D20 roll.");
                Require(layout != null && !ReferenceEquals(layout, previousLayout) && layout.Roll == flow.diceRoller.LastRoll,
                    "The new room must use fresh data and the displayed roll.");
                Require(ActiveViews().Length == 1, "Exactly one generated room view should exist.");
                Require(flow.darknessOverlay.activeInHierarchy && !flow.player.enabled && !flow.playerBody.simulated,
                    "Generation must finish while the player is still locked behind the black overlay.");
                if (Rooms.Count > 0)
                {
                    DoorDirection previousExit = (DoorDirection)Enum.Parse(typeof(DoorDirection), Rooms[Rooms.Count - 1].exit);
                    Require(layout.Entry.Direction == (DoorDirection)(((int)previousExit + 2) % 4), "The entry must face opposite the previous exit.");
                    Require(layout.Seed != Rooms[Rooms.Count - 1].seed, "The next room must receive a new layout seed.");
                }
                pendingRoom = new RoomResult
                {
                    index = flow.CurrentRoomIndex, seed = layout.Seed, roll = layout.Roll, width = layout.Width, height = layout.Height,
                    entry = layout.Entry.Direction.ToString(), exit = layout.Exit.Direction.ToString()
                };
                Pass("room_generated_after_roll", "Room " + pendingRoom.index + ": " + layout.Width + "x" + layout.Height + ", seed " + layout.Seed + ".");
            });
        }

        private static void OnRevealed(RoomLayout layout)
        {
            Observe(() =>
            {
                Require(pendingRoom != null && flow.CurrentRoom == layout && !flow.IsTransitioning, "Reveal must finish the current generated room.");
                Require(flow.player.isActiveAndEnabled && flow.playerBody.simulated && !flow.darknessOverlay.activeInHierarchy,
                    "Player control/physics must return only after reveal.");
                Require(LightIsOn(flow.playerLight) && LightIsOn(flow.roomLight), "Player and room lights must be active after reveal.");
                Require(animationAdvanced, "The real D20 animation did not visibly advance before its result.");
                pendingRoom.diceAnimationAdvanced = animationAdvanced;
                RoomTilemapView[] views = ActiveViews();
                Require(views.Length == 1, "Room views accumulated across transitions.");
                ValidateTilemaps(views[0], layout, pendingRoom);
                CatacombDoor next = null, entry = null;
                foreach (CatacombDoor door in Object.FindObjectsOfType<CatacombDoor>())
                {
                    if (door.IsEntry) { Require(entry == null, "More than one entry door exists."); entry = door; }
                    else { Require(next == null, "More than one exit door exists."); next = door; }
                }
                Require(entry != null && next != null, "The generated room must have exactly two active doors.");
                ValidateDoor(views[0], entry, layout.Entry, true);
                ValidateDoor(views[0], next, layout.Exit, false);
                ValidatePlayerSpawn(views[0]);
                Rooms.Add(pendingRoom);
                previousLayout = layout;
                Pass("room_revealed_and_collidable", "Room validated: floor/wall/void layers, physical collision, door footprints, lights and free player spawn.");
                if (Rooms.Count >= 3)
                {
                    CheckAllDiceResults();
                    Finish(null);
                    return;
                }
                watchedDoor = next;
                SetPhase(Phase.DoorOpening);
            });
        }

        private static void ValidateTilemaps(RoomTilemapView view, RoomLayout layout, RoomResult result)
        {
            TilemapCollider2D low = view.collisionLow.GetComponent<TilemapCollider2D>();
            TilemapCollider2D high = view.collisionHigh.GetComponent<TilemapCollider2D>();
            Require(low != null && high != null && low.enabled && high.enabled && !low.isTrigger && !high.isTrigger,
                "Both collision Tilemaps need active solid TilemapCollider2D components.");
            Require(view.ground.gameObject.layer == 0 && low.gameObject.layer == 0 && high.gameObject.layer == 0,
                "Ground and collision maps must remain on the configured Default physics layer.");
            Require(!Physics2D.GetIgnoreLayerCollision(flow.player.gameObject.layer, low.gameObject.layer), "Player/world layers must physically collide.");
            Require(view.ground.GetComponent<Collider2D>() == null && view.decorationHigh.GetComponent<Collider2D>() == null
                && view.decorations.GetComponent<Collider2D>() == null, "Ground/decorations cannot add unintended collision.");
            SpriteRenderer playerRenderer = flow.player.GetComponentInChildren<SpriteRenderer>();
            Require(playerRenderer != null, "The player prefab must contain its sprite renderer.");
            Require(view.collisionLow.GetComponent<TilemapRenderer>().sortingOrder < playerRenderer.sortingOrder
                && view.collisionHigh.GetComponent<TilemapRenderer>().sortingOrder > playerRenderer.sortingOrder,
                "Low/high collision art must render behind/in front of the player respectively.");
            Require(view.decorationHigh.GetComponent<TilemapRenderer>().sortingOrder > playerRenderer.sortingOrder
                && view.decorations.GetComponent<TilemapRenderer>().sortingOrder < playerRenderer.sortingOrder,
                "Decoration maps must preserve their configured low/high rendering order.");
            Physics2D.SyncTransforms();
            for (int y = 0; y < layout.Height; y++)
                for (int x = 0; x < layout.Width; x++)
                {
                    var tile = new Vector3Int(x, y, 0);
                    Vector2 center = view.CellCenter(new Vector2Int(x, y));
                    RoomCell value = layout[x, y];
                    bool floor = value == RoomCell.Floor || value == RoomCell.Door;
                    Require(view.ground.HasTile(tile) == floor, "Ground does not match Floor/Door data.");
                    bool expectedLow = value == RoomCell.Void || (value == RoomCell.Wall && y != 0);
                    bool expectedHigh = value == RoomCell.Wall && y == 0;
                    Require(view.collisionLow.HasTile(tile) == expectedLow && view.collisionHigh.HasTile(tile) == expectedHigh,
                        "Wall/void tile was painted into the wrong collision map.");
                    if (expectedLow || expectedHigh)
                    {
                        Collider2D expected = expectedLow ? low : high;
                        bool found = false;
                        foreach (Collider2D hit in Physics2D.OverlapPointAll(center, 1 << expected.gameObject.layer))
                            if (hit == expected) found = true;
                        Require(found && Vector2.Distance(expected.ClosestPoint(center), center) < 0.001f,
                            "A solid wall/void tile has no physical collision at its center.");
                    }
                    else if (value == RoomCell.Floor)
                        Require(!low.OverlapPoint(center) && !high.OverlapPoint(center), "A floor cell is blocked by Tilemap collision.");
                    if (value == RoomCell.Floor) result.floorCells++;
                    else if (value == RoomCell.Wall) result.wallCells++;
                    else if (value == RoomCell.Void) result.voidCells++;
                }
        }

        private static void ValidateDoor(RoomTilemapView view, CatacombDoor door, DoorPlacement placement, bool isEntry)
        {
            Require(door.Direction == placement.Direction && door.IsEntry == isEntry, "Door orientation/type does not match room data.");
            Require(Vector3.Distance(door.transform.position, view.DoorCenter(placement)) < 0.001f, "Door is not centered on its two-cell footprint.");
            float angle = placement.Direction == DoorDirection.North ? 90f : placement.Direction == DoorDirection.West ? 180f
                : placement.Direction == DoorDirection.South ? 270f : 0f;
            Require(Mathf.Abs(Mathf.DeltaAngle(door.transform.eulerAngles.z, angle)) < 0.01f, "Door transform rotation is incorrect.");
            Require(isEntry ? !door.IsOpen && !door.CanTraverse && door.blocker.enabled && !door.passageTrigger.enabled
                : door.IsOpen && door.CanTraverse && !door.blocker.enabled && door.passageTrigger.enabled, "Entry must stay sealed and exit must open after its animation.");
            Bounds bounds = (isEntry ? door.blocker : door.passageTrigger).bounds;
            bool vertical = placement.Direction == DoorDirection.North || placement.Direction == DoorDirection.South;
            Require(Mathf.Abs(bounds.size.x - (vertical ? 1f : 2f)) < 0.01f && Mathf.Abs(bounds.size.y - (vertical ? 2f : 1f)) < 0.01f,
                "Door collider footprint must be N/S 1x2 or E/W 2x1.");
            if (isEntry) Require(door.blocker.OverlapPoint(view.DoorCenter(placement)), "The return door must physically block its passage.");
            Orientations.Add(placement.Direction);
        }

        private static void ValidatePlayerSpawn(RoomTilemapView view)
        {
            Collider2D player = PlayerCollider();
            Vector2 center = player.bounds.center;
            foreach (Collider2D solid in new[] { view.collisionLow.GetComponent<Collider2D>(), view.collisionHigh.GetComponent<Collider2D>() })
                Require(!solid.OverlapPoint(center) && !solid.Distance(player).isOverlapped, "Player spawned inside wall/void collision.");
        }

        private sealed class FixedRandom : System.Random
        {
            private readonly int value;
            public FixedRandom(int value) { this.value = value; }
            public override int Next(int minValue, int maxValue)
            {
                Require(minValue == 1 && maxValue == 21, "DiceRoller requested a range other than D20.");
                return value;
            }
        }

        private static void CheckAllDiceResults()
        {
            temporaryDiceRoot = new GameObject("CRITICAL_TEMP_DICE_CHECKS", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            temporaryDiceRoot.hideFlags = HideFlags.DontSave;
            temporaryDiceRoot.GetComponent<CanvasGroup>().alpha = 0f;
            temporaryDiceRoot.GetComponent<CanvasGroup>().blocksRaycasts = false;
            var actor = new GameObject("Dice", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Animator));
            actor.transform.SetParent(temporaryDiceRoot.transform, false);
            var textObject = new GameObject("Result", typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(temporaryDiceRoot.transform, false);
            DiceRoller isolated = actor.AddComponent<DiceRoller>();
            isolated.animator = actor.GetComponent<Animator>();
            isolated.animator.runtimeAnimatorController = flow.diceRoller.animator.runtimeAnimatorController;
            isolated.resultText = textObject.AddComponent<TextMeshProUGUI>();
            for (int roll = 1; roll <= 20; roll++)
            {
                int events = 0, value = 0;
                Action<int> completed = result => { events++; value = result; };
                isolated.RollCompleted += completed;
                IEnumerator routine = isolated.Roll(new FixedRandom(roll));
                try
                {
                    Require(routine.MoveNext() && isolated.LastRoll == roll && isolated.resultText.text == "" && isolated.animator.enabled,
                        "D20 must begin with animation and no final result text.");
                    Require(!routine.MoveNext() && isolated.resultText.text == roll.ToString() && events == 1 && value == roll
                        && !isolated.animator.enabled, "Fixed D20 result/event/text do not agree.");
                    report.fixedRollsChecked++;
                }
                finally
                {
                    isolated.RollCompleted -= completed;
                    (routine as IDisposable)?.Dispose();
                }
            }
            Object.DestroyImmediate(temporaryDiceRoot);
            temporaryDiceRoot = null;
            Pass("all_d20_faces", "Twenty isolated roll enumerators produced matching final text and one event each; no real-time wait was skipped in the actual flow.");
        }

        private static RoomTilemapView[] ActiveViews() => Object.FindObjectsOfType<RoomTilemapView>();
        private static bool LightIsOn(GameObject go)
        {
            Light2D light = go.GetComponentInChildren<Light2D>();
            return go.activeInHierarchy && light != null && light.enabled && light.intensity > 0f;
        }

        private static Collider2D PlayerCollider()
        {
            foreach (Collider2D collider in flow.player.GetComponentsInChildren<Collider2D>())
                if (collider.enabled && !collider.isTrigger) return collider;
            throw new InvalidOperationException("The active player has no solid Collider2D.");
        }

        private static void SetPhase(Phase next) { phase = next; phaseStarted = EditorApplication.timeSinceStartup; }
        private static void Observe(Action action)
        {
            if (!IsRunning) return;
            try { action(); }
            catch (Exception error) { Finish(error); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Pass(string name, string detail)
        {
            Stages.Add(new Stage { name = name, passed = true, detail = detail, room = flow != null ? flow.CurrentRoomIndex : 0,
                elapsedSeconds = EditorApplication.timeSinceStartup - started });
        }

        private static void Finish(Exception error)
        {
            if (!IsRunning) return;
            IsRunning = false;
            EditorApplication.update -= Tick;
            if (watchedDoor != null) watchedDoor.Traversed -= OnPhysicalTraversal;
            if (subscribed && flow != null)
            {
                flow.RoomGenerated -= OnGenerated;
                flow.RoomRevealed -= OnRevealed;
                if (flow.diceRoller != null) flow.diceRoller.RollCompleted -= OnRollCompleted;
            }
            if (temporaryDiceRoot != null) Object.DestroyImmediate(temporaryDiceRoot);
            temporaryDiceRoot = null;
            report.passed = error == null;
            report.error = error?.ToString() ?? "";
            report.failureCount = error == null ? 0 : 1;
            report.elapsedSeconds = EditorApplication.timeSinceStartup - started;
            if (error != null) Stages.Add(new Stage { name = phase.ToString(), passed = false, detail = error.Message,
                elapsedSeconds = report.elapsedSeconds, room = flow != null ? flow.CurrentRoomIndex : 0 });
            report.stages = Stages.ToArray();
            report.rooms = Rooms.ToArray();
            var observed = new List<string>();
            foreach (DoorDirection direction in Enum.GetValues(typeof(DoorDirection)))
                if (Orientations.Contains(direction)) observed.Add(direction.ToString());
            report.observedOrientations = observed.ToArray();
            report.limitations = observed.Count < 4
                ? new[] { "Only " + observed.Count + " door orientations appeared naturally in these three rooms; generation checks cover all four data orientations." }
                : new string[0];
            Directory.CreateDirectory("Library/CRITICAL");
            File.WriteAllText("Library/CRITICAL/ExplorationRuntimeChecks.json", JsonUtility.ToJson(report, true));
            string message = "CRITICAL Exploration Runtime Checks: " + (report.passed ? "PASS" : "FAIL") + " — " + Rooms.Count
                + " rooms, " + report.physicalTraversals + " native door traversals, " + report.fixedRollsChecked + " isolated D20 faces.";
            if (report.passed) Debug.Log(message);
            else Debug.LogError(message + "\n" + error);
        }
    }
}
