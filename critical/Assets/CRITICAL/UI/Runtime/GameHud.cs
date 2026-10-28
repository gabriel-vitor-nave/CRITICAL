using System;
using System.Collections.Generic;
using System.Text;
using Critical.Procedural;
using Critical.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Critical.UI
{
    /// <summary>Run UI follows the supplied sketch; the authored main menu stays separate.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        public RunProgression progression;
        public PlayerHealth playerHealth;
        public ExplorationFlow flow;
        public static GameHud Instance { get; private set; }
        public static bool IsInputBlocked => SceneTransition.IsLoading || (Instance != null && Instance.isActiveAndEnabled &&
            (Instance.pauseOwned || Instance.modal != Modal.None));
        public bool IsPaused => pauseOwned;
        public bool InventoryOpen => modal == Modal.Inventory;
        public bool UpgradeOpen => modal == Modal.Upgrade;
        private enum Modal { None, Pause, Inventory, Upgrade, Death }
        private static readonly Color Ink = new Color32(17, 17, 17, 249);
        private static readonly Color Panel = new Color32(22, 22, 22, 249);
        private static readonly Color White = new Color32(218, 218, 213, 255);
        private static readonly Color Muted = new Color32(162, 162, 158, 255);
        private static readonly Color Red = new Color32(231, 107, 109, 255);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private const string LayoutMarker = "ReferenceLayoutV2";
        private const int HealthPipLimit = 14;

        // Rebind runtime caches from the owned tree after a Unity domain reload.
        [NonSerialized] private Queue<KeyValuePair<string, Color>> notifications = new Queue<KeyValuePair<string, Color>>();
        [NonSerialized] private Canvas canvas;
        [NonSerialized] private RectTransform xpFill, experienceTop, healthRoot, diceRoot, toastRect, modalPanel;
        [NonSerialized] private CanvasGroup modalGroup, toastGroup;
        [NonSerialized] private UnityEngine.UI.Image flash, modalDim, weaponIcon;
        [NonSerialized] private TMP_Text healthValue, xpText, goldText, roomText, riskText, toastText, weaponName, weaponInitials, diceValue, diceCaption;
        [NonSerialized] private TMP_Text modalTitle, modalSubtitle, itemName, itemDescription, itemRarity, itemEffect, deathStats, appliedUpgradeList, controlsList;
        [NonSerialized] private GameObject pauseContent, inventoryContent, upgradeContent, deathContent, statusRoot;
        [NonSerialized] private UnityEngine.UI.Button useButton, dropButton, closeButton;
        [NonSerialized] private UiSketchGraphic[] healthPips = new UiSketchGraphic[HealthPipLimit];
        [NonSerialized] private UnityEngine.UI.Button[] slots = new UnityEngine.UI.Button[12], upgradeButtons = new UnityEngine.UI.Button[3], quickButtons = new UnityEngine.UI.Button[3];
        [NonSerialized] private TMP_Text[] slotNames = new TMP_Text[12], slotCounts = new TMP_Text[12], slotInitials = new TMP_Text[12];
        [NonSerialized] private TMP_Text[] upgradeNames = new TMP_Text[3], upgradeDescriptions = new TMP_Text[3], upgradeInitials = new TMP_Text[3];
        [NonSerialized] private TMP_Text[] quickCounts = new TMP_Text[3], quickInitials = new TMP_Text[3];
        [NonSerialized] private UnityEngine.UI.Image[] slotIcons = new UnityEngine.UI.Image[12], upgradeIcons = new UnityEngine.UI.Image[3], quickIcons = new UnityEngine.UI.Image[3];
        [NonSerialized] private UiSketchGraphic[] slotMarks = new UiSketchGraphic[12];
        [NonSerialized] private Modal modal;
        [NonSerialized] private bool pauseOwned, built, subscribed, showControls;
        [NonSerialized] private float previousTimeScale = 1f, toastTime, flashStrength, badgePulse, healthPulse;
        [NonSerialized] private int previousHP, previousLevel, lastUseFrame = -1, lastDropFrame = -1;
        [NonSerialized] private float healthTarget = 1f, healthShown = 1f, xpTarget, xpShown;

        private void Start() => Initialize();
        private bool Initialize()
        {
            if (!Application.isPlaying) return false;
            if (Instance != null && Instance != this) { enabled = false; return false; }
            if (progression == null) progression = RunProgression.Instance;
            if (progression == null) progression = FindObjectOfType<RunProgression>();
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
            if (flow == null) flow = FindObjectOfType<ExplorationFlow>();
            if (progression == null || playerHealth == null) return false;
            Instance = this;
            if (notifications == null) notifications = new Queue<KeyValuePair<string, Color>>();
            EnsureArrays();
            Transform existing = transform.Find("RunHUD");
            if (existing != null && existing.Find(LayoutMarker) != null && BindTree(existing)) canvas.enabled = true;
            else
            {
                // Retire only our earlier generated layout. No authored scene object is replaced.
                if (existing != null) { existing.gameObject.SetActive(false); existing.name = "PreviousRunHUD"; }
                Build();
            }
            WireActions(); Subscribe(); built = true;
            previousHP = playerHealth.HP; previousLevel = progression.Level;
            Refresh(); healthShown = healthTarget; xpShown = xpTarget; SetContent();
            modalGroup.alpha = 0f; modalGroup.blocksRaycasts = modalGroup.interactable = false;
            return true;
        }
        private void EnsureArrays()
        {
            if (healthPips == null || healthPips.Length != HealthPipLimit) healthPips = new UiSketchGraphic[HealthPipLimit];
            if (slots == null || slots.Length != 12) slots = new UnityEngine.UI.Button[12];
            if (slotNames == null || slotNames.Length != 12) slotNames = new TMP_Text[12];
            if (slotCounts == null || slotCounts.Length != 12) slotCounts = new TMP_Text[12];
            if (slotInitials == null || slotInitials.Length != 12) slotInitials = new TMP_Text[12];
            if (slotIcons == null || slotIcons.Length != 12) slotIcons = new UnityEngine.UI.Image[12];
            if (slotMarks == null || slotMarks.Length != 12) slotMarks = new UiSketchGraphic[12];
            if (upgradeButtons == null || upgradeButtons.Length != 3) upgradeButtons = new UnityEngine.UI.Button[3];
            if (upgradeNames == null || upgradeNames.Length != 3) upgradeNames = new TMP_Text[3];
            if (upgradeDescriptions == null || upgradeDescriptions.Length != 3) upgradeDescriptions = new TMP_Text[3];
            if (upgradeInitials == null || upgradeInitials.Length != 3) upgradeInitials = new TMP_Text[3];
            if (upgradeIcons == null || upgradeIcons.Length != 3) upgradeIcons = new UnityEngine.UI.Image[3];
            if (quickButtons == null || quickButtons.Length != 3) quickButtons = new UnityEngine.UI.Button[3];
            if (quickCounts == null || quickCounts.Length != 3) quickCounts = new TMP_Text[3];
            if (quickInitials == null || quickInitials.Length != 3) quickInitials = new TMP_Text[3];
            if (quickIcons == null || quickIcons.Length != 3) quickIcons = new UnityEngine.UI.Image[3];
        }
        private void Build()
        {
            var root = new GameObject("RunHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false); canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            modalDim = Fullscreen("WorldDim", root.transform, Clear, false);
            RectTransform frame = Sketch("ScreenFrame", root.transform, Vector2.zero, Vector2.zero, Vector2.zero, Clear);
            Stretch(frame); frame.offsetMin = new Vector2(8f, 8f); frame.offsetMax = new Vector2(-8f, -8f);
            healthRoot = Rect("HealthPips", root.transform, new Vector2(410, 110), new Vector2(32, -24), new Vector2(0, 1));
            for (int i = 0; i < HealthPipLimit; i++)
            {
                RectTransform pip = Sketch("HealthPip" + i, healthRoot, new Vector2(40, 40), new Vector2(i % 7 * 56, -(i / 7) * 52), new Vector2(0, 1), new Color32(111, 42, 42, 195), UiSketchGraphic.OutlineShape.Circle, Red);
                healthPips[i] = pip.GetComponent<UiSketchGraphic>();
            }
            healthValue = Label("HealthValue", healthRoot, "", 16, Muted, new Vector2(396, 26), new Vector2(2, -108), new Vector2(0, 1));
            experienceTop = Rect("ExperienceTop", root.transform, new Vector2(0, 78), new Vector2(0, -3), new Vector2(0.5f, 1));
            experienceTop.anchorMin = new Vector2(0.265f, 1f); experienceTop.anchorMax = new Vector2(0.735f, 1f);
            xpText = Label("ExperienceValue", experienceTop, "", 25, White, new Vector2(0, 30), new Vector2(0, -1), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
            xpText.rectTransform.anchorMin = new Vector2(0, 1); xpText.rectTransform.anchorMax = new Vector2(1, 1);
            RectTransform track = Sketch("XPTrack", experienceTop, new Vector2(0, 20), new Vector2(0, -39), new Vector2(0.5f, 1), Clear);
            track.anchorMin = new Vector2(0, 1); track.anchorMax = new Vector2(1, 1);
            RectTransform inset = Rect("Inset", track, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(inset); inset.offsetMin = new Vector2(5, 5); inset.offsetMax = new Vector2(-5, -5);
            xpFill = Image("XP", inset, Vector2.zero, Vector2.zero, Vector2.zero, new Color32(208, 208, 203, 210)).rectTransform; Stretch(xpFill);
            statusRoot = Rect("RoomStatus", root.transform, new Vector2(900, 26), new Vector2(0, -79), new Vector2(0.5f, 1)).gameObject;
            roomText = Label("Room", statusRoot.transform, "", 15, Muted, new Vector2(290, 24), Vector2.zero, new Vector2(0, 1));
            goldText = Label("Gold", statusRoot.transform, "", 15, Muted, new Vector2(220, 24), Vector2.zero, new Vector2(0.5f, 1), TextAlignmentOptions.Center);
            riskText = Label("Risk", statusRoot.transform, "", 15, Muted, new Vector2(290, 24), Vector2.zero, new Vector2(1, 1), TextAlignmentOptions.Right);
            var weapon = Button("WeaponSlot", root.transform, "", new Vector2(244, 244), new Vector2(-146, -24), new Vector2(1, 1), OpenEquipment);
            weaponIcon = Image("Icon", weapon.transform, new Vector2(112, 112), new Vector2(0, -54), new Vector2(0.5f, 1), White); weaponIcon.preserveAspect = true;
            weaponInitials = Label("WeaponInitials", weapon.transform, "ARMA", 34, White, new Vector2(220, 112), new Vector2(0, -54), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
            weaponName = Label("WeaponName", weapon.transform, "", 20, White, new Vector2(210, 54), new Vector2(0, -178), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
            string[] roles = { "PRÓXIMO\nITEM", "ITEM\nSELECIONADO", "ITEM\nANTERIOR" };
            for (int i = 0; i < 3; i++)
            {
                bool selected = i == 1; float size = selected ? 76f : 52f;
                Vector2 position = i == 0 ? new Vector2(-59, -46) : i == 1 ? new Vector2(-47, -111) : new Vector2(-59, -205);
                int index = i;
                quickButtons[i] = Button("QuickSlot" + i, root.transform, "", new Vector2(size, size), position, new Vector2(1, 1), () => UseCarousel(index));
                TMP_Text role = Label("Role", quickButtons[i].transform, roles[i], selected ? 12 : 10, White, new Vector2(size - 6, selected ? 27 : 22), new Vector2(3, -3), new Vector2(0, 1), TextAlignmentOptions.Center);
                role.enableAutoSizing = true; role.fontSizeMin = 8f; role.fontSizeMax = selected ? 12f : 10f;
                float iconSize = selected ? 30f : 21f;
                quickIcons[i] = Image("Icon", quickButtons[i].transform, new Vector2(iconSize, iconSize), new Vector2(0, selected ? -31 : -24), new Vector2(0.5f, 1), White); quickIcons[i].preserveAspect = true;
                quickInitials[i] = Label("Initials", quickButtons[i].transform, "", selected ? 18 : 14, Muted, new Vector2(size - 8, iconSize), new Vector2(0, selected ? -31 : -24), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
                quickCounts[i] = Label("Count", quickButtons[i].transform, "", 12, White, new Vector2(size - 8, 17), new Vector2(-4, 3), new Vector2(1, 0), TextAlignmentOptions.Right);
            }
            var dice = Button("DiceSlot", root.transform, "", new Vector2(320, 320), new Vector2(-36, 24), new Vector2(1, 0), ShowDiceInfo, UiSketchGraphic.OutlineShape.Diamond);
            diceRoot = dice.GetComponent<RectTransform>();
            diceValue = Label("DiceValue", diceRoot, "D20", 29, White, new Vector2(230, 80), Vector2.zero, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            diceCaption = Label("DiceCaption", diceRoot, "", 16, Muted, new Vector2(210, 45), new Vector2(0, -62), new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            Label("ControlsHint", root.transform, "[ESC] PAUSA / CONTROLES    [I] INVENTÁRIO", 16, Muted, new Vector2(700, 32), new Vector2(30, 27), Vector2.zero);
            flash = Fullscreen("DamageFlash", root.transform, new Color(Red.r, Red.g, Red.b, 0f), false);
            toastRect = Sketch("Toast", root.transform, new Vector2(760, 66), new Vector2(0, -138), new Vector2(0.5f, 1), Ink);
            toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
            toastText = Label("Message", toastRect, "", 21, White, new Vector2(716, 56), new Vector2(22, -5), new Vector2(0, 1), TextAlignmentOptions.Center);
            var modalRoot = Fullscreen("ModalBackdrop", root.transform, new Color(0f, 0f, 0f, 0.001f), true);
            modalGroup = modalRoot.gameObject.AddComponent<CanvasGroup>(); modalGroup.alpha = 0; modalGroup.blocksRaycasts = modalGroup.interactable = false;
            modalPanel = Sketch("ModalPanel", modalRoot.transform, new Vector2(900, 720), new Vector2(0, -48), new Vector2(0.5f, 0.5f), Ink);
            modalTitle = Label("Title", modalPanel, "", 34, White, new Vector2(1008, 64), new Vector2(56, -34), new Vector2(0, 1));
            modalSubtitle = Label("Subtitle", modalPanel, "", 18, Muted, new Vector2(1008, 48), new Vector2(56, -101), new Vector2(0, 1));
            closeButton = Button("ClosePause", modalPanel, "×", new Vector2(62, 62), new Vector2(-15, -12), new Vector2(1, 1), Resume);
            closeButton.GetComponent<UiSketchGraphic>().Configure(UiSketchGraphic.OutlineShape.Rectangle, Clear, new Color(0, 0, 0, 0.005f), 0f);
            TMP_Text closeLabel = closeButton.GetComponentInChildren<TMP_Text>(); closeLabel.fontSize = 58;
            closeLabel.rectTransform.sizeDelta = new Vector2(62, 62); closeLabel.overflowMode = TextOverflowModes.Overflow;
            pauseContent = Content("Pause", modalPanel); inventoryContent = Content("Inventory", modalPanel);
            upgradeContent = Content("Upgrade", modalPanel); deathContent = Content("Death", modalPanel);
            BuildPause(); BuildInventory(); BuildUpgrades(); BuildDeath();
            closeButton.transform.SetAsLastSibling(); new GameObject(LayoutMarker).transform.SetParent(root.transform, false);
        }
        private void BuildPause()
        {
            Button("Retry", pauseContent.transform, "REINICIAR RUN", new Vector2(620, 86), new Vector2(0, -130), new Vector2(0.5f, 1), Retry);
            Button("AppliedUpgrades", pauseContent.transform, "UPGRADES", new Vector2(620, 86), new Vector2(0, -232), new Vector2(0.5f, 1), ShowUpgrades);
            RectTransform details = Sketch("PauseDetails", pauseContent.transform, new Vector2(620, 294), new Vector2(0, -334), new Vector2(0.5f, 1), Clear);
            appliedUpgradeList = Label("AppliedUpgradeList", details, "", 24, White, new Vector2(570, 250), new Vector2(0, -22), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
            appliedUpgradeList.enableAutoSizing = true; appliedUpgradeList.fontSizeMin = 16; appliedUpgradeList.fontSizeMax = 24;
            controlsList = Label("ControlsList", details,
                "WASD / SETAS  —  MOVER\nESPAÇO / J  —  ATACAR\nZ / C  —  ITEM ANTERIOR / PRÓXIMO\nQ  —  USAR ITEM SELECIONADO\nG  —  SOLTAR UMA UNIDADE\nI / TAB  —  INVENTÁRIO\nESC  —  PAUSA / FECHAR JANELA\n1 / 2 / 3  —  ESCOLHER UPGRADE\nSETAS + ENTER  —  NAVEGAR / CONFIRMAR\nPASSE SOBRE OS ITENS PARA COLETAR", 19, White,
                new Vector2(574, 266), new Vector2(23, -14), new Vector2(0, 1));
            controlsList.enableAutoSizing = true; controlsList.fontSizeMin = 15; controlsList.fontSizeMax = 19;
            Button("Controls", pauseContent.transform, "CONTROLES", new Vector2(195, 38), new Vector2(-211, -660), new Vector2(0.5f, 1), ShowControls);
            Button("Inventory", pauseContent.transform, "INVENTÁRIO [I]", new Vector2(195, 38), new Vector2(0, -660), new Vector2(0.5f, 1), ToggleInventory);
            Button("MainMenu", pauseContent.transform, "VOLTAR AO MENU", new Vector2(195, 38), new Vector2(211, -660), new Vector2(0.5f, 1), MainMenu);
        }
        private void BuildInventory()
        {
            RectTransform grid = Rect("Slots", inventoryContent.transform, new Vector2(510, 394), new Vector2(56, -179), new Vector2(0, 1));
            var layout = grid.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>(); layout.cellSize = new Vector2(116, 116); layout.spacing = new Vector2(14, 14);
            layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 4;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; slots[i] = Button("Slot" + (i + 1), grid, "", new Vector2(116, 116), Vector2.zero, new Vector2(0, 1), () => SelectItem(index));
                slotMarks[i] = slots[i].GetComponent<UiSketchGraphic>();
                slotIcons[i] = Image("Icon", slots[i].transform, new Vector2(48, 48), new Vector2(34, -16), new Vector2(0, 1), White); slotIcons[i].preserveAspect = true;
                slotInitials[i] = Label("PlaceholderInitials", slots[i].transform, "", 25, White, new Vector2(74, 48), new Vector2(21, -16), new Vector2(0, 1), TextAlignmentOptions.Center);
                slotNames[i] = Label("Name", slots[i].transform, "", 14, White, new Vector2(108, 34), new Vector2(4, -66), new Vector2(0, 1), TextAlignmentOptions.Center);
                slotCounts[i] = Label("Count", slots[i].transform, "", 13, White, new Vector2(100, 20), new Vector2(8, -94), new Vector2(0, 1), TextAlignmentOptions.Right);
            }
            RectTransform detail = Sketch("ItemDetails", inventoryContent.transform, new Vector2(440, 394), new Vector2(-56, -179), new Vector2(1, 1), Clear);
            itemRarity = Label("Rarity", detail, "", 15, White, new Vector2(388, 28), new Vector2(26, -23), new Vector2(0, 1));
            itemName = Label("Name", detail, "", 27, White, new Vector2(388, 84), new Vector2(26, -58), new Vector2(0, 1));
            itemDescription = Label("Description", detail, "", 20, Muted, new Vector2(388, 142), new Vector2(26, -147), new Vector2(0, 1));
            itemEffect = Label("Effect", detail, "", 16, White, new Vector2(388, 66), new Vector2(26, -296), new Vector2(0, 1));
            useButton = Button("UseItem", inventoryContent.transform, "USAR [Q / ENTER]", new Vector2(210, 64), new Vector2(-286, -597), new Vector2(1, 1), UseSelected);
            dropButton = Button("DropItem", inventoryContent.transform, "SOLTAR [G]", new Vector2(220, 64), new Vector2(-56, -597), new Vector2(1, 1), DropSelected);
            Button("CloseInventory", inventoryContent.transform, "FECHAR [I / ESC]", new Vector2(510, 64), new Vector2(56, -597), new Vector2(0, 1), Resume);
        }
        private void BuildUpgrades()
        {
            RectTransform row = Rect("Choices", upgradeContent.transform, new Vector2(1008, 402), new Vector2(56, -184), new Vector2(0, 1));
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 24;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                int index = i; upgradeButtons[i] = Button("Upgrade" + i, row, "", new Vector2(320, 402), Vector2.zero, new Vector2(0, 1), () => ChooseUpgrade(index));
                Label("Key", upgradeButtons[i].transform, "0" + (i + 1), 17, Muted, new Vector2(100, 32), new Vector2(24, -22), new Vector2(0, 1));
                upgradeIcons[i] = Image("Icon", upgradeButtons[i].transform, new Vector2(74, 74), new Vector2(0, -68), new Vector2(0.5f, 1), White); upgradeIcons[i].preserveAspect = true;
                upgradeInitials[i] = Label("PlaceholderInitials", upgradeButtons[i].transform, "", 26, White, new Vector2(100, 74), new Vector2(0, -68), new Vector2(0.5f, 1), TextAlignmentOptions.Center);
                upgradeNames[i] = Label("Name", upgradeButtons[i].transform, "", 25, White, new Vector2(270, 80), new Vector2(24, -164), new Vector2(0, 1), TextAlignmentOptions.Center);
                upgradeDescriptions[i] = Label("Description", upgradeButtons[i].transform, "", 19, Muted, new Vector2(270, 100), new Vector2(24, -249), new Vector2(0, 1), TextAlignmentOptions.Center);
                Label("Choose", upgradeButtons[i].transform, "ESCOLHER", 16, White, new Vector2(270, 32), new Vector2(24, -360), new Vector2(0, 1), TextAlignmentOptions.Center);
            }
            Label("UpgradeHint", upgradeContent.transform, "ESCOLHA COM 1 / 2 / 3 OU SETAS + ENTER", 17, Muted, new Vector2(1008, 52), new Vector2(56, -619), new Vector2(0, 1), TextAlignmentOptions.Center);
        }
        private void BuildDeath()
        {
            deathStats = Label("RunResult", deathContent.transform, "", 25, White, new Vector2(1008, 170), new Vector2(56, -196), new Vector2(0, 1), TextAlignmentOptions.Center);
            Button("Retry", deathContent.transform, "TENTAR OUTRA VEZ", new Vector2(500, 78), new Vector2(0, -400), new Vector2(0.5f, 1), Retry);
            Button("MainMenu", deathContent.transform, "VOLTAR AO MENU", new Vector2(500, 70), new Vector2(0, -496), new Vector2(0.5f, 1), MainMenu);
        }
        private void Update()
        {
            if (!built && !Initialize()) return;
            if (progression == null || playerHealth == null || canvas == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool transitioning = flow != null && flow.IsTransitioning;
            if (!SceneTransition.IsLoading)
            {
                if (playerHealth.IsDead && modal != Modal.Death) OnDeath();
                if (modal == Modal.None && !pauseOwned && !transitioning && !playerHealth.IsDead && progression.PendingUpgrades > 0) OpenModal(Modal.Upgrade);
                if (!transitioning && !playerHealth.IsDead)
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        if (modal == Modal.Inventory || modal == Modal.Pause) Resume();
                        else if (modal == Modal.None && !pauseOwned) TogglePause();
                    }
                    else if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab)) ToggleInventory();
                    if (modal == Modal.Upgrade)
                        for (int i = 0; i < 3; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) ChooseUpgrade(i);
                    if (modal == Modal.Inventory)
                    {
                        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                        int selected = progression.SelectedItemIndex;
                        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && selected >= 0 && selected < slots.Length && focused == slots[selected].gameObject) UseSelected();
                        if (Input.GetKeyDown(KeyCode.Q)) UseSelected();
                        if (Input.GetKeyDown(KeyCode.G)) DropSelected();
                    }
                    if ((modal == Modal.None && !pauseOwned) || modal == Modal.Inventory)
                    {
                        if (Input.GetKeyDown(KeyCode.Z)) CycleItem(-1);
                        if (Input.GetKeyDown(KeyCode.C)) CycleItem(1);
                    }
                    if (modal == Modal.None && !pauseOwned)
                    {
                        if (Input.GetKeyDown(KeyCode.Q)) UseQuickItem();
                        if (Input.GetKeyDown(KeyCode.G)) DropSelected();
                    }
                }
            }
            healthShown = Mathf.Lerp(healthShown, healthTarget, 1f - Mathf.Exp(-18f * dt));
            xpShown = Mathf.Lerp(xpShown, xpTarget, 1f - Mathf.Exp(-8f * dt)); SetFill(xpFill, xpShown);
            badgePulse = Mathf.MoveTowards(badgePulse, 0f, dt * 2.2f); healthPulse = Mathf.MoveTowards(healthPulse, 0f, dt * 4f);
            experienceTop.localScale = Vector3.one * (1f + Mathf.Sin(badgePulse * Mathf.PI) * 0.035f);
            healthRoot.localScale = Vector3.one * (1f + Mathf.Sin(healthPulse * Mathf.PI) * 0.075f);
            flashStrength = Mathf.MoveTowards(flashStrength, 0f, dt * 1.5f);
            float low = playerHealth.HP > 0 && healthTarget < 0.25f ? 0.02f + Mathf.Sin(Time.unscaledTime * 4f) * 0.01f : 0f;
            flash.color = new Color(Red.r, Red.g, Red.b, flashStrength + low);
            DrawHealth(); AnimateModal(dt); AnimateToast(dt); UpdateRoomStatus();
        }
        public void Refresh()
        {
            if (!built || progression == null || playerHealth == null) return;
            healthTarget = playerHealth.maxHP > 0 ? Mathf.Clamp01((float)playerHealth.HP / playerHealth.maxHP) : 0f;
            xpTarget = progression.ExperienceToNextLevel > 0 ? Mathf.Clamp01((float)progression.Experience / progression.ExperienceToNextLevel) : 1f;
            xpText.text = progression.ExperienceToNextLevel == 0 ? "LVL " + progression.Level + " — NÍVEL MÁXIMO" :
                "LVL " + progression.Level + " - " + progression.Experience.ToString("000") + "/" + progression.ExperienceToNextLevel.ToString("000") + " XP";
            goldText.text = progression.Gold + " OURO";
            if (progression.Level != previousLevel) { xpShown = 0f; previousLevel = progression.Level; }
            RefreshInventory(); RefreshEquipment(); RefreshHistory();
            if (modal == Modal.Upgrade) { if (progression.PendingUpgrades > 0) RefreshUpgrades(); else CloseUpgrade(); }
        }
        private void DrawHealth()
        {
            int perPip = Mathf.Max(1, Mathf.CeilToInt(playerHealth.maxHP / (float)HealthPipLimit));
            int count = Mathf.Clamp(Mathf.CeilToInt(playerHealth.maxHP / (float)perPip), 1, HealthPipLimit);
            for (int i = 0; i < healthPips.Length; i++)
            {
                healthPips[i].gameObject.SetActive(i < count); if (i >= count) continue;
                float capacity = Mathf.Min(perPip, playerHealth.maxHP - i * perPip);
                float amount = Mathf.Clamp01((healthShown * playerHealth.maxHP - i * perPip) / Mathf.Max(1f, capacity));
                healthPips[i].SetHealth(amount, amount < 0.01f);
            }
            healthValue.text = perPip > 1 ? playerHealth.HP + " / " + playerHealth.maxHP + " HP" : "";
        }
        private void UpdateRoomStatus()
        {
            bool transitioning = flow != null && flow.IsTransitioning;
            int roll = flow != null && flow.diceRoller != null ? flow.diceRoller.LastRoll : 0;
            roomText.text = flow == null || flow.CurrentRoomIndex == 0 ? "ENTRADA DA CATACUMBA" : "SALA " + flow.CurrentRoomIndex.ToString("00");
            riskText.text = transitioning ? "ROLANDO O DESTINO..." : roll == 0 ? "ATRAVESSE A PORTA" : roll >= 16 ? "RISCO ALTO" : roll >= 8 ? "RISCO MÉDIO" : "RISCO BAIXO";
            riskText.color = roll >= 16 ? Red : Muted;
            diceValue.text = transitioning ? "..." : roll == 0 ? "D20" : roll.ToString();
            diceValue.fontSize = !transitioning && roll > 0 ? 56 : 29; diceCaption.text = roll > 0 && !transitioning ? "D20" : "";
            diceRoot.localScale = Vector3.one * (transitioning ? 1f + Mathf.Sin(Time.unscaledTime * 16f) * 0.025f : 1f);
            statusRoot.SetActive(modal == Modal.None);
        }
        private void RefreshEquipment()
        {
            ItemDefinition weapon = progression.EquippedWeapon;
            weaponIcon.sprite = weapon != null ? weapon.Icon : null; weaponIcon.enabled = weaponIcon.sprite != null;
            weaponInitials.text = weaponIcon.enabled ? "" : "ARMA"; weaponName.text = weapon != null ? weapon.DisplayName : "";
            int count = progression.Inventory.Count; int selected = progression.SelectedItemIndex;
            for (int i = 0; i < 3; i++)
            {
                int index = i == 1 ? selected : count > 1 && selected >= 0 ? (selected + (i == 0 ? 1 : count - 1)) % count : -1;
                ItemStack stack = index >= 0 && index < count ? progression.Inventory[index] : null;
                bool exists = stack != null && stack.Item != null; quickButtons[i].interactable = exists;
                quickIcons[i].sprite = exists ? stack.Item.Icon : null; quickIcons[i].enabled = quickIcons[i].sprite != null;
                quickInitials[i].text = exists && stack.Item.Icon == null ? Initials(stack.Item.DisplayName) : exists ? "" : "—";
                quickCounts[i].text = exists && stack.Count > 1 ? "×" + stack.Count : "";
            }
        }
        private void RefreshInventory()
        {
            int selected = progression.SelectedItemIndex;
            for (int i = 0; i < slots.Length; i++)
            {
                bool occupied = i < progression.Inventory.Count && progression.Inventory[i].Item != null; slots[i].interactable = occupied;
                if (!occupied)
                {
                    slotNames[i].text = "VAZIO"; slotNames[i].color = Muted; slotCounts[i].text = ""; slotIcons[i].enabled = false; slotInitials[i].text = "";
                    slotMarks[i].Configure(UiSketchGraphic.OutlineShape.Rectangle, new Color(0.35f, 0.35f, 0.34f), Clear); continue;
                }
                ItemStack stack = progression.Inventory[i]; slotNames[i].text = stack.Item.DisplayName; slotNames[i].color = i == selected ? White : Muted;
                slotCounts[i].text = stack.Count > 1 ? "×" + stack.Count : "";
                slotMarks[i].Configure(UiSketchGraphic.OutlineShape.Rectangle, i == selected ? White : new Color(0.5f, 0.5f, 0.48f), i == selected ? Panel : Clear);
                slotIcons[i].sprite = stack.Item.Icon; slotIcons[i].enabled = stack.Item.Icon != null;
                slotInitials[i].text = stack.Item.Icon == null ? Initials(stack.Item.DisplayName) : "";
            }
            ItemStack selectedStack = progression.SelectedItem; bool hasItem = selectedStack != null && selectedStack.Item != null;
            useButton.interactable = hasItem && selectedStack.Item.Consumable; dropButton.interactable = hasItem;
            if (!hasItem)
            {
                itemName.text = "A mochila está vazia"; itemRarity.text = "12 ESPAÇOS";
                itemDescription.text = "Vença encontros para coletar itens. Passe sobre os itens soltos no chão para recolher."; itemEffect.text = ""; return;
            }
            ItemDefinition item = selectedStack.Item; itemName.text = item.DisplayName;
            itemRarity.text = RarityName(item.Rarity) + (item.Consumable ? " • CONSUMÍVEL" : " • PASSIVO"); itemRarity.color = RunProgression.RarityColor(item.Rarity);
            itemDescription.text = item.Description;
            itemEffect.text = item.Consumable ? "Q usa uma unidade. G solta uma unidade no chão." : "Bônus ativo enquanto estiver na mochila. G solta uma unidade.";
        }
        private void RefreshHistory()
        {
            var selected = progression.AppliedUpgrades;
            if (selected.Count == 0) appliedUpgradeList.text = "TODOS OS UPGRADES ATÉ ENTÃO\nSELECIONADOS";
            else
            {
                var counts = new Dictionary<string, int>(); var definitions = new List<UpgradeDefinition>();
                foreach (UpgradeDefinition upgrade in selected)
                {
                    if (upgrade == null) continue; string key = upgrade.Id ?? upgrade.DisplayName;
                    if (counts.ContainsKey(key)) counts[key]++; else { counts.Add(key, 1); definitions.Add(upgrade); }
                }
                var text = new StringBuilder();
                foreach (UpgradeDefinition upgrade in definitions)
                {
                    if (text.Length > 0) text.Append("\n\n");
                    text.Append(upgrade.DisplayName).Append(" ×").Append(counts[upgrade.Id ?? upgrade.DisplayName]);
                }
                appliedUpgradeList.text = text.ToString();
            }
            appliedUpgradeList.gameObject.SetActive(!showControls); controlsList.gameObject.SetActive(showControls);
        }
        private void RefreshUpgrades()
        {
            IReadOnlyList<UpgradeDefinition> choices = progression.GetUpgradeChoices(); modalSubtitle.text = "ESCOLHA UMA MELHORIA. RESTANTES: " + progression.PendingUpgrades;
            for (int i = 0; i < 3; i++)
            {
                bool exists = i < choices.Count; upgradeButtons[i].gameObject.SetActive(exists); if (!exists) continue;
                upgradeNames[i].text = choices[i].DisplayName; upgradeDescriptions[i].text = choices[i].Description;
                upgradeIcons[i].sprite = choices[i].Icon; upgradeIcons[i].enabled = choices[i].Icon != null;
                upgradeInitials[i].text = choices[i].Icon == null ? Initials(choices[i].DisplayName) : "";
            }
        }
        private void OnLevelGained(int level) { badgePulse = 1f; }
        private void OnHealthChanged(int current, int maximum)
        {
            if (current != previousHP) healthPulse = 1f; if (current < previousHP) flashStrength = 0.19f;
            previousHP = current; Refresh();
        }
        private void OnDeath()
        {
            OpenModal(Modal.Death);
            deathStats.text = "NÍVEL " + progression.Level + "  •  " + progression.Gold + " OURO\n\n" +
                (flow != null ? "Você chegou à sala " + flow.CurrentRoomIndex + "." : "Sua run chegou ao fim.");
        }
        public void TogglePause() { if (!CanToggle()) return; if (modal == Modal.Pause) Resume(); else if (modal == Modal.None) OpenModal(Modal.Pause); }
        public void ToggleInventory() { if (!CanToggle()) return; if (modal == Modal.Inventory) Resume(); else if (modal == Modal.None || modal == Modal.Pause) OpenModal(Modal.Inventory); }
        private bool CanToggle() => built && playerHealth != null && !playerHealth.IsDead && !SceneTransition.IsLoading && !(flow != null && flow.IsTransitioning) && !(modal == Modal.None && pauseOwned);
        public void Resume()
        {
            if (!built || modal == Modal.Death || modal == Modal.Upgrade || SceneTransition.IsLoading) return;
            if (progression.PendingUpgrades > 0) { OpenModal(Modal.Upgrade); return; } CloseUpgrade();
        }
        public void ShowUpgrades()
        {
            if (!built || modal != Modal.Pause) return;
            if (progression.PendingUpgrades > 0) { OpenModal(Modal.Upgrade); return; } showControls = false; RefreshHistory();
        }
        public void ShowControls() { if (!built || modal != Modal.Pause) return; showControls = !showControls; RefreshHistory(); }
        private void OpenModal(Modal next)
        {
            if (!pauseOwned)
            {
                previousTimeScale = Time.timeScale; Time.timeScale = 0f; pauseOwned = true;
                var body = playerHealth.GetComponent<Rigidbody2D>(); if (body != null) body.velocity = Vector2.zero;
            }
            modal = next; if (next == Modal.Pause) showControls = false;
            modalGroup.blocksRaycasts = modalGroup.interactable = true; SetContent();
            UnityEngine.UI.Button first = next == Modal.Upgrade ? upgradeButtons[0] : next == Modal.Inventory && progression.SelectedItemIndex >= 0 && progression.SelectedItemIndex < slots.Length && progression.Inventory.Count > 0 ? slots[progression.SelectedItemIndex] :
                next == Modal.Inventory ? Find<UnityEngine.UI.Button>(inventoryContent.transform, "CloseInventory") : next == Modal.Death ? Find<UnityEngine.UI.Button>(deathContent.transform, "Retry") : Find<UnityEngine.UI.Button>(pauseContent.transform, "Retry");
            if (EventSystem.current != null && first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        private void SetContent()
        {
            pauseContent.SetActive(modal == Modal.Pause); inventoryContent.SetActive(modal == Modal.Inventory);
            upgradeContent.SetActive(modal == Modal.Upgrade); deathContent.SetActive(modal == Modal.Death);
            bool pause = modal == Modal.Pause; modalPanel.sizeDelta = pause ? new Vector2(900, 720) : new Vector2(1120, 716);
            modalTitle.gameObject.SetActive(!pause); modalSubtitle.gameObject.SetActive(!pause); closeButton.gameObject.SetActive(pause || modal == Modal.Inventory);
            modalTitle.text = modal == Modal.Inventory ? "INVENTÁRIO" : modal == Modal.Upgrade ? "NÍVEL " + progression.Level + " • FIQUE MAIS FORTE" : "O DESTINO COBROU.";
            modalSubtitle.text = modal == Modal.Inventory ? "SETAS NAVEGAM • Q / ENTER USA • G SOLTA • Z / C ALTERNA" : modal == Modal.Death ? "Cada tentativa conta uma história. Role outra vez." : "";
            RefreshHistory(); if (modal == Modal.Inventory) RefreshInventory(); if (modal == Modal.Upgrade) RefreshUpgrades();
        }
        private void AnimateModal(float dt)
        {
            float target = modal == Modal.None ? 0f : 1f; modalGroup.alpha = Mathf.MoveTowards(modalGroup.alpha, target, dt * 7f); float t = modalGroup.alpha;
            modalPanel.localScale = Vector3.one * Mathf.Lerp(0.965f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            modalPanel.anchoredPosition = new Vector2(0, (modal == Modal.Pause ? -48f : 0f) - (1f - t) * 22f); modalDim.color = new Color(0f, 0f, 0f, t * 0.64f);
            if (modal == Modal.None && pauseOwned && t <= 0f) ReleasePause();
        }
        private void ReleasePause() { if (!pauseOwned) return; Time.timeScale = previousTimeScale; pauseOwned = false; }
        private void SelectItem(int index) { if (progression == null) return; progression.SelectItem(index); RefreshInventory(); }
        private void CycleItem(int step)
        {
            if (progression == null) return; progression.CycleSelectedItem(step); Refresh();
            if (modal == Modal.Inventory && progression.SelectedItemIndex >= 0 && progression.SelectedItemIndex < slots.Length && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slots[progression.SelectedItemIndex].gameObject);
        }
        private void UseSelected() { if (modal != Modal.Inventory || lastUseFrame == Time.frameCount) return; lastUseFrame = Time.frameCount; progression.UseSelectedItem(); }
        private void UseQuickItem()
        {
            if (modal != Modal.None || pauseOwned || SceneTransition.IsLoading || lastUseFrame == Time.frameCount) return;
            lastUseFrame = Time.frameCount; if (progression.SelectedItem == null) ShowToast("Nenhum item selecionado.", Muted); else progression.UseSelectedItem();
        }
        private void DropSelected()
        {
            if ((modal != Modal.None && modal != Modal.Inventory) || SceneTransition.IsLoading || lastDropFrame == Time.frameCount) return;
            lastDropFrame = Time.frameCount; if (progression.SelectedItem == null) ShowToast("Nenhum item selecionado.", Muted); else progression.DropSelectedItem();
        }
        private void UseCarousel(int index)
        {
            if (GameHud.IsInputBlocked || playerHealth.IsDead || flow != null && flow.IsTransitioning) return;
            if (index == 0) CycleItem(1); else if (index == 2) CycleItem(-1); else UseQuickItem();
        }
        private void OpenEquipment()
        {
            if (!CanToggle()) return; ToggleInventory(); ItemDefinition weapon = progression.EquippedWeapon; if (weapon == null) return;
            for (int i = 0; i < progression.Inventory.Count; i++) if (progression.Inventory[i].Item != null && progression.Inventory[i].Item.Id == weapon.Id) { SelectItem(i); return; }
        }
        private void ShowDiceInfo() { if (!GameHud.IsInputBlocked) ShowToast("O D20 rola ao atravessar uma porta. Quanto maior o resultado, maior o risco e a recompensa.", White); }
        public void ChooseUpgrade(int index)
        {
            if (!built || modal != Modal.Upgrade || !progression.ChooseUpgrade(index)) return;
            badgePulse = 1f; if (progression.PendingUpgrades > 0) { RefreshUpgrades(); return; } CloseUpgrade();
        }
        private void CloseUpgrade() { modal = Modal.None; modalGroup.blocksRaycasts = modalGroup.interactable = false; if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
        public void ShowToast(string message, Color color)
        {
            if (string.IsNullOrEmpty(message)) return; if (notifications == null) notifications = new Queue<KeyValuePair<string, Color>>();
            if (notifications.Count >= 6) notifications.Dequeue(); notifications.Enqueue(new KeyValuePair<string, Color>(message, color));
        }
        private void AnimateToast(float dt)
        {
            if (modal != Modal.None) { toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, 0f, dt * 6f); return; }
            if (toastTime <= 0f && notifications.Count > 0) { var notification = notifications.Dequeue(); toastText.text = notification.Key; toastText.color = notification.Value; toastTime = 2.5f; }
            toastTime = Mathf.Max(0f, toastTime - dt); toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, toastTime > 0.3f ? 1f : 0f, dt * 6f);
            toastRect.anchoredPosition = new Vector2(0f, -138f + (1f - toastGroup.alpha) * 12f);
        }
        private void Retry() => SceneTransition.Load("Exploration");
        private void MainMenu() => SceneTransition.Load("Menu");
        private void OnEnable() { built = false; }
        private void OnDisable()
        {
            Unsubscribe(); ReleasePause(); modal = Modal.None; built = false;
            if (canvas != null) canvas.enabled = false;
            if (modalGroup != null) { modalGroup.alpha = 0f; modalGroup.blocksRaycasts = modalGroup.interactable = false; }
        }
        private void OnDestroy() { Unsubscribe(); ReleasePause(); if (Instance == this) Instance = null; }
        private void Subscribe()
        {
            Unsubscribe(); progression.Changed += Refresh; progression.LevelGained += OnLevelGained; progression.Feedback += ShowToast;
            playerHealth.HealthChanged += OnHealthChanged; playerHealth.Died += OnDeath; subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (progression != null) { progression.Changed -= Refresh; progression.LevelGained -= OnLevelGained; progression.Feedback -= ShowToast; }
            if (playerHealth != null) { playerHealth.HealthChanged -= OnHealthChanged; playerHealth.Died -= OnDeath; } subscribed = false;
        }
        private bool BindTree(Transform root)
        {
            canvas = root.GetComponent<Canvas>(); healthRoot = Find<RectTransform>(root, "HealthPips"); experienceTop = Find<RectTransform>(root, "ExperienceTop");
            healthValue = Find<TMP_Text>(root, "HealthPips/HealthValue"); xpText = Find<TMP_Text>(root, "ExperienceTop/ExperienceValue"); xpFill = Find<RectTransform>(root, "ExperienceTop/XPTrack/Inset/XP");
            statusRoot = root.Find("RoomStatus")?.gameObject; goldText = Find<TMP_Text>(root, "RoomStatus/Gold"); roomText = Find<TMP_Text>(root, "RoomStatus/Room"); riskText = Find<TMP_Text>(root, "RoomStatus/Risk");
            weaponIcon = Find<UnityEngine.UI.Image>(root, "WeaponSlot/Icon"); weaponName = Find<TMP_Text>(root, "WeaponSlot/WeaponName"); weaponInitials = Find<TMP_Text>(root, "WeaponSlot/WeaponInitials");
            diceRoot = Find<RectTransform>(root, "DiceSlot"); diceValue = Find<TMP_Text>(root, "DiceSlot/DiceValue"); diceCaption = Find<TMP_Text>(root, "DiceSlot/DiceCaption");
            modalDim = Find<UnityEngine.UI.Image>(root, "WorldDim"); flash = Find<UnityEngine.UI.Image>(root, "DamageFlash");
            toastRect = Find<RectTransform>(root, "Toast"); toastGroup = Find<CanvasGroup>(root, "Toast"); toastText = Find<TMP_Text>(root, "Toast/Message");
            modalGroup = Find<CanvasGroup>(root, "ModalBackdrop"); modalPanel = Find<RectTransform>(root, "ModalBackdrop/ModalPanel");
            if (canvas == null || healthRoot == null || experienceTop == null || xpFill == null || modalPanel == null || modalGroup == null || toastGroup == null) return false;
            modalTitle = Find<TMP_Text>(modalPanel, "Title"); modalSubtitle = Find<TMP_Text>(modalPanel, "Subtitle"); closeButton = Find<UnityEngine.UI.Button>(modalPanel, "ClosePause");
            pauseContent = modalPanel.Find("Pause")?.gameObject; inventoryContent = modalPanel.Find("Inventory")?.gameObject; upgradeContent = modalPanel.Find("Upgrade")?.gameObject; deathContent = modalPanel.Find("Death")?.gameObject;
            if (pauseContent == null || inventoryContent == null || upgradeContent == null || deathContent == null || closeButton == null) return false;
            appliedUpgradeList = Find<TMP_Text>(pauseContent.transform, "PauseDetails/AppliedUpgradeList"); controlsList = Find<TMP_Text>(pauseContent.transform, "PauseDetails/ControlsList");
            itemRarity = Find<TMP_Text>(inventoryContent.transform, "ItemDetails/Rarity"); itemName = Find<TMP_Text>(inventoryContent.transform, "ItemDetails/Name"); itemDescription = Find<TMP_Text>(inventoryContent.transform, "ItemDetails/Description"); itemEffect = Find<TMP_Text>(inventoryContent.transform, "ItemDetails/Effect");
            useButton = Find<UnityEngine.UI.Button>(inventoryContent.transform, "UseItem"); dropButton = Find<UnityEngine.UI.Button>(inventoryContent.transform, "DropItem"); deathStats = Find<TMP_Text>(deathContent.transform, "RunResult");
            for (int i = 0; i < HealthPipLimit; i++) { healthPips[i] = Find<UiSketchGraphic>(healthRoot, "HealthPip" + i); if (healthPips[i] == null) return false; }
            for (int i = 0; i < 12; i++)
            {
                string path = "Slots/Slot" + (i + 1); slots[i] = Find<UnityEngine.UI.Button>(inventoryContent.transform, path); if (slots[i] == null) return false;
                slotMarks[i] = slots[i].GetComponent<UiSketchGraphic>(); slotIcons[i] = Find<UnityEngine.UI.Image>(slots[i].transform, "Icon"); slotNames[i] = Find<TMP_Text>(slots[i].transform, "Name"); slotCounts[i] = Find<TMP_Text>(slots[i].transform, "Count"); slotInitials[i] = Find<TMP_Text>(slots[i].transform, "PlaceholderInitials");
                if (slotMarks[i] == null || slotIcons[i] == null || slotNames[i] == null || slotCounts[i] == null || slotInitials[i] == null) return false;
            }
            for (int i = 0; i < 3; i++)
            {
                upgradeButtons[i] = Find<UnityEngine.UI.Button>(upgradeContent.transform, "Choices/Upgrade" + i); quickButtons[i] = Find<UnityEngine.UI.Button>(root, "QuickSlot" + i);
                if (upgradeButtons[i] == null || quickButtons[i] == null) return false;
                upgradeNames[i] = Find<TMP_Text>(upgradeButtons[i].transform, "Name"); upgradeDescriptions[i] = Find<TMP_Text>(upgradeButtons[i].transform, "Description"); upgradeInitials[i] = Find<TMP_Text>(upgradeButtons[i].transform, "PlaceholderInitials"); upgradeIcons[i] = Find<UnityEngine.UI.Image>(upgradeButtons[i].transform, "Icon");
                quickIcons[i] = Find<UnityEngine.UI.Image>(quickButtons[i].transform, "Icon"); quickInitials[i] = Find<TMP_Text>(quickButtons[i].transform, "Initials"); quickCounts[i] = Find<TMP_Text>(quickButtons[i].transform, "Count");
                if (upgradeNames[i] == null || upgradeDescriptions[i] == null || upgradeInitials[i] == null || upgradeIcons[i] == null || quickIcons[i] == null || quickInitials[i] == null || quickCounts[i] == null) return false;
            }
            return healthValue != null && xpText != null && statusRoot != null && goldText != null && roomText != null && riskText != null && weaponIcon != null && weaponName != null && weaponInitials != null && diceRoot != null && diceValue != null && diceCaption != null && modalDim != null && flash != null && toastRect != null && toastText != null && modalTitle != null && modalSubtitle != null && appliedUpgradeList != null && controlsList != null && useButton != null && dropButton != null && itemRarity != null && itemName != null && itemDescription != null && itemEffect != null && deathStats != null;
        }
        private void WireActions()
        {
            Transform root = canvas.transform;
            WireHudButton(Find<UnityEngine.UI.Button>(root, "WeaponSlot"), OpenEquipment); WireHudButton(Find<UnityEngine.UI.Button>(root, "DiceSlot"), ShowDiceInfo); Wire(closeButton, Resume);
            Wire(Find<UnityEngine.UI.Button>(pauseContent.transform, "Retry"), Retry); Wire(Find<UnityEngine.UI.Button>(pauseContent.transform, "AppliedUpgrades"), ShowUpgrades);
            Wire(Find<UnityEngine.UI.Button>(pauseContent.transform, "Controls"), ShowControls); Wire(Find<UnityEngine.UI.Button>(pauseContent.transform, "Inventory"), ToggleInventory); Wire(Find<UnityEngine.UI.Button>(pauseContent.transform, "MainMenu"), MainMenu);
            Wire(useButton, UseSelected); Wire(dropButton, DropSelected); Wire(Find<UnityEngine.UI.Button>(inventoryContent.transform, "CloseInventory"), Resume);
            Wire(Find<UnityEngine.UI.Button>(deathContent.transform, "Retry"), Retry); Wire(Find<UnityEngine.UI.Button>(deathContent.transform, "MainMenu"), MainMenu);
            for (int i = 0; i < 12; i++) { int index = i; Wire(slots[i], () => SelectItem(index)); slots[i].GetComponent<JuicyButton>().Selected = () => SelectItem(index); }
            for (int i = 0; i < 3; i++) { int index = i; Wire(upgradeButtons[i], () => ChooseUpgrade(index)); WireHudButton(quickButtons[i], () => UseCarousel(index)); }
        }
        private void WireHudButton(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            var navigation = button.navigation; navigation.mode = UnityEngine.UI.Navigation.Mode.None; button.navigation = navigation;
            Wire(button, () =>
            {
                action();
                // Gameplay's arrows and Space belong to movement/attack after a HUD click.
                if (modal == Modal.None && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
        }
        private static void Wire(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action) { if (button == null) return; button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }
        private static T Find<T>(Transform parent, string path) where T : Component { Transform child = parent != null ? parent.Find(path) : null; return child != null ? child.GetComponent<T>() : null; }
        private static string RarityName(ItemRarity rarity) => rarity == ItemRarity.Divine ? "DIVINO" : rarity == ItemRarity.Epic ? "ÉPICO" : rarity == ItemRarity.Rare ? "RARO" : "COMUM";
        private static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            string[] words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 1 ? (words[0][0].ToString() + words[words.Length - 1][0]).ToUpperInvariant() : name.Substring(0, Mathf.Min(2, name.Length)).ToUpperInvariant();
        }
        private static GameObject Content(string name, Transform parent) { var rect = Rect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f)); Stretch(rect); return rect.gameObject; }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static RectTransform Sketch(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor, Color background, UiSketchGraphic.OutlineShape shape = UiSketchGraphic.OutlineShape.Rectangle, Color? border = null)
        {
            RectTransform rect = Rect(name, parent, size, position, anchor); var graphic = rect.gameObject.AddComponent<UiSketchGraphic>(); graphic.color = Color.white;
            graphic.Configure(shape, border ?? White, background); graphic.raycastTarget = false; return rect;
        }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 size, Vector2 position, Vector2 anchor, Color tint)
        {
            RectTransform rect = Rect(name, parent, size, position, anchor); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = tint; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.Image Fullscreen(string name, Transform parent, Color color, bool raycast)
        {
            var image = Image(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, color); Stretch(image.rectTransform); image.raycastTarget = raycast; return image;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void SetFill(RectTransform rect, float value) { if (rect != null) rect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f); }
        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, Vector2 dimensions, Vector2 position, Vector2 anchor, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Rect(name, parent, dimensions, position, anchor); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment;
            label.fontStyle = FontStyles.Italic; label.raycastTarget = false; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, string title, Vector2 size, Vector2 position, Vector2 anchor, UnityEngine.Events.UnityAction action, UiSketchGraphic.OutlineShape shape = UiSketchGraphic.OutlineShape.Rectangle)
        {
            var rect = Sketch(name, parent, size, position, anchor, Ink, shape); var graphic = rect.GetComponent<UiSketchGraphic>(); graphic.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = graphic;
            var colors = button.colors; colors.normalColor = new Color(0.83f, 0.83f, 0.81f); colors.highlightedColor = Color.white; colors.selectedColor = Color.white; colors.pressedColor = new Color(0.65f, 0.65f, 0.63f); colors.disabledColor = new Color(0.4f, 0.4f, 0.39f, 0.65f); colors.fadeDuration = 0.08f; button.colors = colors;
            button.onClick.AddListener(action); rect.gameObject.AddComponent<JuicyButton>();
            Label("Label", rect, title, size.y < 45f ? 16 : 21, White, size - new Vector2(20, 8), Vector2.zero, new Vector2(0.5f, 0.5f), TextAlignmentOptions.Center);
            return button;
        }
    }
}
