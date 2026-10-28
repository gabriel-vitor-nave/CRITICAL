using System;
using System.Collections.Generic;
using Critical.Gameplay;
using UnityEngine;

namespace Critical.Progression
{
    [DisallowMultipleComponent]
    public sealed class RunProgression : MonoBehaviour
    {
        public const int MaximumLevel = 50;
        public const int InventoryCapacity = 12;
        public const int MaximumStack = 99;

        public static RunProgression Instance { get; private set; }
        [Tooltip("Itens da run. Sem catálogo configurado, utiliza as cinco definições básicas.")]
        public ItemDefinition[] itemCatalog;
        [Range(0, MaximumStack)] public int startingPotionCount = 2;

        [field: SerializeField, HideInInspector] public int Level { get; private set; } = 1;
        [field: SerializeField, HideInInspector] public int Experience { get; private set; }
        public int ExperienceToNextLevel => Level >= MaximumLevel ? 0 : 40 + (Level - 1) * 20;
        [field: SerializeField, HideInInspector] public int PendingUpgrades { get; private set; }
        [field: SerializeField, HideInInspector] public int Gold { get; private set; }
        public IReadOnlyList<ItemStack> Inventory => inventory;
        public IReadOnlyList<ItemDefinition> Catalog => itemCatalog;
        public IReadOnlyList<UpgradeDefinition> AppliedUpgrades => appliedUpgrades;
        [field: SerializeField, HideInInspector] public int SelectedItemIndex { get; private set; } = -1;
        public ItemStack SelectedItem => SelectedItemIndex >= 0 && SelectedItemIndex < inventory.Count ? inventory[SelectedItemIndex] : null;
        public ItemDefinition EquippedWeapon
        {
            get
            {
                if (itemCatalog != null)
                    foreach (var item in itemCatalog)
                        if (item != null && item.Id == "broken-staff") return item;
                return null;
            }
        }

        public event Action Changed;
        public event Action<int> LevelGained;
        public event Action<string, Color> Feedback;

        [SerializeField, HideInInspector] private List<ItemStack> inventory = new List<ItemStack>();
        [SerializeField, HideInInspector] private List<UpgradeDefinition> choices = new List<UpgradeDefinition>();
        [SerializeField, HideInInspector] private List<UpgradeDefinition> appliedUpgrades = new List<UpgradeDefinition>();
        [SerializeField, HideInInspector] private List<UpgradeDefinition> consumedEffects = new List<UpgradeDefinition>();
        private readonly List<int> gainedLevels = new List<int>();
        private PlayerHealth health;
        private PlayerMovement movement;
        private Player_Combat combat;
        private bool busy;
        [SerializeField, HideInInspector] private bool initialized, ownsCatalog;
        [SerializeField, HideInInspector] private bool baseStatsCaptured;
        [SerializeField, HideInInspector] private int baseDamage, baseMaxHealth;
        [SerializeField, HideInInspector] private float baseMoveSpeed, baseAttackCooldown, baseCriticalChance;
        private readonly Color goldColor = new Color(1f, 0.78f, 0.29f);
        private readonly Color xpColor = new Color(0.33f, 0.91f, 0.9f);

        private void Awake() => Initialize();
        private void OnEnable() => Initialize();

        private void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Debug.LogError("Só pode existir uma progressão de run ativa.", this);
                return;
            }
            Instance = this;
            health = GetComponent<PlayerHealth>();
            movement = GetComponent<PlayerMovement>();
            combat = GetComponent<Player_Combat>();
            CaptureBaseStats();
            if (initialized) { NormalizeSelection(); return; }
            if (itemCatalog == null || itemCatalog.Length == 0)
            {
                itemCatalog = CreateDefaultCatalog();
                ownsCatalog = true;
            }
            foreach (var item in itemCatalog)
                if (item != null && item.Id == "potion" && startingPotionCount > 0)
                {
                    inventory.Add(new ItemStack(item, Mathf.Clamp(startingPotionCount, 1, MaximumStack)));
                    break;
                }
            initialized = true;
            NormalizeSelection();
        }

        public void GrantExperience(int amount)
        {
            if (busy || amount <= 0 || Level >= MaximumLevel) return;
            busy = true;
            try
            {
                long available = (long)Experience + amount;
                gainedLevels.Clear();
                while (Level < MaximumLevel && available >= ExperienceToNextLevel)
                {
                    available -= ExperienceToNextLevel;
                    Level++;
                    PendingUpgrades++;
                    gainedLevels.Add(Level);
                }
                Experience = Level >= MaximumLevel ? 0 : (int)available;
                if (PendingUpgrades > 0 && choices.Count == 0) BuildChoices();
                Feedback?.Invoke("+" + amount + " XP", xpColor);
                foreach (int level in gainedLevels) LevelGained?.Invoke(level);
                Changed?.Invoke();
            }
            finally { busy = false; }
        }

        public void AddGold(int amount)
        {
            if (busy || amount <= 0 || Gold == int.MaxValue) return;
            busy = true;
            try
            {
                int gained = (int)Math.Min((long)amount, (long)int.MaxValue - Gold);
                Gold += gained;
                Feedback?.Invoke("+" + gained + " OURO", goldColor);
                Changed?.Invoke();
            }
            finally { busy = false; }
        }

        public bool AddItem(ItemDefinition item, int count = 1, bool restoreDroppedItem = false)
        {
            if (busy || item == null || string.IsNullOrEmpty(item.Id) || count <= 0 || !ValidMagnitude(item.Magnitude)) return false;
            var stack = inventory.Find(entry => entry.Item != null && entry.Item.Id == item.Id);
            if ((stack == null && inventory.Count >= InventoryCapacity) || count > MaximumStack - (stack?.Count ?? 0))
            {
                busy = true;
                try { Feedback?.Invoke("INVENTÁRIO CHEIO", new Color(1f, 0.48f, 0.4f)); }
                finally { busy = false; }
                return false;
            }
            if (!item.Consumable && !CanApply(item.Effect)) return false;
            busy = true;
            try
            {
                int oldMaximum = health != null ? health.maxHP : 0;
                if (stack == null) inventory.Add(new ItemStack(item, count));
                else stack.Count += count;
                NormalizeSelection();
                if (!item.Consumable)
                {
                    RecalculateStats();
                    if (!restoreDroppedItem && item.Effect == ItemEffect.MaxHealth) HealMaximumGain(oldMaximum);
                }
                Feedback?.Invoke("+ " + item.DisplayName + (count > 1 ? " ×" + count : ""), RarityColor(item.Rarity));
                Changed?.Invoke();
                return true;
            }
            finally { busy = false; }
        }

        public bool UseItem(int inventoryIndex)
        {
            if (busy || inventoryIndex < 0 || inventoryIndex >= inventory.Count || (health != null && health.IsDead)) return false;
            var stack = inventory[inventoryIndex];
            if (stack.Item == null || stack.Count <= 0) return false;
            if (!stack.Item.Consumable)
            {
                busy = true;
                try { Feedback?.Invoke("RELÍQUIA EQUIPADA", RarityColor(stack.Item.Rarity)); }
                finally { busy = false; }
                return false;
            }
            if (!CanApply(stack.Item.Effect)) return false;
            if (stack.Item.Effect == ItemEffect.Heal && (health.HP <= 0 || health.HP >= health.maxHP))
            {
                busy = true;
                try { Feedback?.Invoke(health.HP <= 0 ? "A RUN TERMINOU" : "VIDA JÁ ESTÁ CHEIA", goldColor); }
                finally { busy = false; }
                return false;
            }
            busy = true;
            try
            {
                if (stack.Item.Effect == ItemEffect.Heal)
                    health.ChangeHP(Mathf.Clamp(Mathf.RoundToInt(stack.Item.Magnitude), 1, 10000));
                else
                {
                    int oldMaximum = health != null ? health.maxHP : 0;
                    consumedEffects.Add(new UpgradeDefinition { Id = stack.Item.Id, DisplayName = stack.Item.DisplayName,
                        Effect = stack.Item.Effect, Magnitude = stack.Item.Magnitude, Icon = stack.Item.Icon });
                    RecalculateStats();
                    if (stack.Item.Effect == ItemEffect.MaxHealth) HealMaximumGain(oldMaximum);
                }
                string label = stack.Item.DisplayName;
                RemoveUnits(inventoryIndex, 1);
                Feedback?.Invoke(label + " USADA", new Color(0.44f, 1f, 0.58f));
                Changed?.Invoke();
                return true;
            }
            finally { busy = false; }
        }

        public IReadOnlyList<UpgradeDefinition> GetUpgradeChoices() => choices;

        public bool SelectItem(int index)
        {
            if (busy || index < 0 || index >= inventory.Count) return false;
            if (SelectedItemIndex == index) return true;
            busy = true;
            try { SelectedItemIndex = index; Changed?.Invoke(); return true; }
            finally { busy = false; }
        }

        public void CycleSelectedItem(int step)
        {
            if (busy) return;
            NormalizeSelection();
            if (inventory.Count == 0 || step == 0) return;
            long index = ((long)SelectedItemIndex + step) % inventory.Count;
            SelectItem((int)(index < 0 ? index + inventory.Count : index));
        }

        public bool UseSelectedItem() => UseItem(SelectedItemIndex);
        public bool DropSelectedItem() => DropItem(SelectedItemIndex);

        public bool DropItem(int inventoryIndex, int count = 1)
        {
            if (busy || inventoryIndex < 0 || inventoryIndex >= inventory.Count || count <= 0) return false;
            var stack = inventory[inventoryIndex];
            if (stack.Item == null || count > stack.Count || (health != null && health.IsDead)) return false;
            busy = true;
            try
            {
                // The pickup must exist before ownership changes; failed placement loses nothing.
                if (WorldItemPickup.SpawnDropped(this, stack.Item, count) == null) return false;
                RemoveUnits(inventoryIndex, count);
                if (!stack.Item.Consumable) RecalculateStats();
                Feedback?.Invoke(stack.Item.DisplayName + " NO CHÃO", RarityColor(stack.Item.Rarity));
                Changed?.Invoke();
                return true;
            }
            finally { busy = false; }
        }

        private void RemoveUnits(int index, int count)
        {
            inventory[index].Count -= count;
            if (inventory[index].Count == 0)
            {
                inventory.RemoveAt(index);
                if (index < SelectedItemIndex) SelectedItemIndex--;
            }
            NormalizeSelection();
        }

        private void NormalizeSelection() => SelectedItemIndex = inventory.Count == 0 ? -1 : Mathf.Clamp(SelectedItemIndex, 0, inventory.Count - 1);

        public bool ChooseUpgrade(int index)
        {
            if (busy || PendingUpgrades <= 0 || index < 0 || index >= choices.Count) return false;
            var upgrade = choices[index];
            if (!CanApply(upgrade.Effect)) return false;
            busy = true;
            try
            {
                int oldMaximum = health != null ? health.maxHP : 0;
                // Keep what was chosen even after the next choice pool replaces these objects.
                appliedUpgrades.Add(new UpgradeDefinition
                {
                    Id = upgrade.Id, DisplayName = upgrade.DisplayName, Description = upgrade.Description,
                    Icon = upgrade.Icon, Effect = upgrade.Effect, Magnitude = upgrade.Magnitude
                });
                RecalculateStats();
                if (upgrade.Effect == ItemEffect.MaxHealth) HealMaximumGain(oldMaximum);
                PendingUpgrades--;
                choices.Clear();
                if (PendingUpgrades > 0) BuildChoices();
                Feedback?.Invoke(upgrade.DisplayName + "!", goldColor);
                Changed?.Invoke();
                return true;
            }
            finally { busy = false; }
        }

        private bool CanApply(ItemEffect effect)
        {
            switch (effect)
            {
                case ItemEffect.Heal:
                case ItemEffect.MaxHealth: return health != null;
                case ItemEffect.MoveSpeed: return movement != null;
                default: return combat != null;
            }
        }

        private static bool ValidMagnitude(float magnitude) => !float.IsNaN(magnitude) && !float.IsInfinity(magnitude) && magnitude > 0f;

        private void CaptureBaseStats()
        {
            if (baseStatsCaptured) return;
            // A component restored from the older model already has its owned bonuses.
            // Remove those once when capturing its base; future reloads use the saved base.
            bool removeOwned = initialized;
            baseDamage = combat != null ? Mathf.Max(1, combat.damage - (int)Math.Min(1000d, removeOwned ? SumEffect(ItemEffect.Damage) : 0d)) : 1;
            baseMaxHealth = health != null ? Mathf.Max(1, health.maxHP - (int)Math.Min(10000d, removeOwned ? SumEffect(ItemEffect.MaxHealth) : 0d)) : 1;
            baseMoveSpeed = movement != null ? Mathf.Max(0.1f, movement.velocidade - (float)(removeOwned ? SumEffect(ItemEffect.MoveSpeed) : 0d)) : 1f;
            baseCriticalChance = combat != null ? Mathf.Max(0f, combat.criticalChance - (float)(removeOwned ? SumEffect(ItemEffect.CriticalChance) : 0d)) : 0f;
            double multiplier = removeOwned ? AttackMultiplier() : 1d;
            baseAttackCooldown = combat != null ? Mathf.Clamp((float)(combat.attackCooldown / Math.Max(multiplier, 0.000001d)), 0.12f, 5f) : 0.42f;
            baseStatsCaptured = true;
        }

        private void RecalculateStats()
        {
            if (combat != null)
            {
                combat.damage = (int)Math.Min(1000d, Math.Max(1d, baseDamage + SumEffect(ItemEffect.Damage)));
                combat.criticalChance = (float)Math.Min(0.75d, Math.Max(0d, baseCriticalChance + SumEffect(ItemEffect.CriticalChance)));
                combat.attackCooldown = Mathf.Clamp((float)(baseAttackCooldown * AttackMultiplier()), 0.12f, 5f);
            }
            if (movement != null) movement.velocidade = (float)Math.Min(12d, Math.Max(0.1d, baseMoveSpeed + SumEffect(ItemEffect.MoveSpeed)));
            if (health == null) return;
            int maximum = (int)Math.Min(10000d, Math.Max(1d, baseMaxHealth + SumEffect(ItemEffect.MaxHealth)));
            if (maximum == health.maxHP) return;
            health.maxHP = maximum;
            health.HP = Mathf.Clamp(health.HP, 0, maximum);
            health.Refresh();
        }

        private void HealMaximumGain(int oldMaximum)
        {
            if (health != null && health.maxHP > oldMaximum) health.ChangeHP(health.maxHP - oldMaximum);
        }

        private double SumEffect(ItemEffect effect)
        {
            double total = 0;
            foreach (var stack in inventory)
                if (stack.Item != null && !stack.Item.Consumable && stack.Item.Effect == effect)
                    total += StatMagnitude(effect, stack.Item.Magnitude) * stack.Count;
            foreach (var upgrade in appliedUpgrades)
                if (upgrade != null && upgrade.Effect == effect) total += StatMagnitude(effect, upgrade.Magnitude);
            foreach (var consumed in consumedEffects)
                if (consumed != null && consumed.Effect == effect) total += StatMagnitude(effect, consumed.Magnitude);
            return total;
        }

        private static double StatMagnitude(ItemEffect effect, float magnitude)
        {
            if (!ValidMagnitude(magnitude)) return 0d;
            if (effect == ItemEffect.Damage || effect == ItemEffect.MaxHealth)
                return Math.Max(1d, Math.Min(effect == ItemEffect.Damage ? 1000d : 10000d, Math.Round(magnitude)));
            return magnitude;
        }

        private double AttackMultiplier()
        {
            double product = 1d;
            foreach (var stack in inventory)
                if (stack.Item != null && !stack.Item.Consumable && stack.Item.Effect == ItemEffect.AttackSpeed && ValidMagnitude(stack.Item.Magnitude))
                    product *= Math.Pow(1d - Mathf.Clamp01(stack.Item.Magnitude), stack.Count);
            foreach (var upgrade in appliedUpgrades)
                if (upgrade != null && upgrade.Effect == ItemEffect.AttackSpeed && ValidMagnitude(upgrade.Magnitude)) product *= 1d - Mathf.Clamp01(upgrade.Magnitude);
            foreach (var consumed in consumedEffects)
                if (consumed != null && consumed.Effect == ItemEffect.AttackSpeed && ValidMagnitude(consumed.Magnitude)) product *= 1d - Mathf.Clamp01(consumed.Magnitude);
            return product;
        }

        private void BuildChoices()
        {
            var pool = new[]
            {
                Upgrade("damage", "GOLPE PESADO", "+1 de dano em todos os ataques.", ItemEffect.Damage, 1f),
                Upgrade("health", "CASCA GROSSA", "+4 de vida máxima e recupera 4 de vida.", ItemEffect.MaxHealth, 4f),
                Upgrade("speed", "PASSOS LEVES", "+0,4 de velocidade de movimento.", ItemEffect.MoveSpeed, 0.4f),
                Upgrade("critical", "OLHAR CRÍTICO", "+8% de chance de acerto crítico.", ItemEffect.CriticalChance, 0.08f),
                Upgrade("haste", "RITMO FEROZ", "Ataques 12% mais rápidos.", ItemEffect.AttackSpeed, 0.12f)
            };
            // Stable per run level; reopening the panel never rerolls a choice.
            int start = (Level + PendingUpgrades) % pool.Length;
            for (int i = 0; i < 3; i++) choices.Add(pool[(start + i * 2) % pool.Length]);
        }

        private UpgradeDefinition Upgrade(string id, string title, string description, ItemEffect effect, float magnitude)
        {
            Sprite icon = null;
            foreach (var item in itemCatalog)
                if (item != null && item.Effect == effect && item.Icon != null) { icon = item.Icon; break; }
            return new UpgradeDefinition { Id = id, DisplayName = title, Description = description, Effect = effect, Magnitude = magnitude, Icon = icon };
        }

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Rare: return new Color(0.35f, 0.76f, 1f);
                case ItemRarity.Epic: return new Color(0.82f, 0.49f, 1f);
                case ItemRarity.Divine: return new Color(1f, 0.8f, 0.32f);
                default: return new Color(0.83f, 0.88f, 0.85f);
            }
        }

        public static ItemDefinition[] CreateDefaultCatalog()
        {
            return new[]
            {
                Item("potion", "Poção de cura", "Restaura 6 de vida. Use pelo inventário ou pela tecla Q.", ItemEffect.Heal, 6, true, ItemRarity.Common),
                Item("banana", "Banana", "Um lanche de emergência. Restaura 3 de vida.", ItemEffect.Heal, 3, true, ItemRarity.Common),
                Item("broken-staff", "Bastão quebrado", "Relíquia equipada ao coletar. +1 de dano.", ItemEffect.Damage, 1, false, ItemRarity.Rare),
                Item("doom-glasses", "Óculos da perdição", "Relíquia equipada ao coletar. +8% de chance crítica.", ItemEffect.CriticalChance, 0.08f, false, ItemRarity.Epic),
                Item("barbarian-cloth", "Tanga do bárbaro", "Relíquia equipada ao coletar. +4 de vida máxima e cura 4 de vida.", ItemEffect.MaxHealth, 4, false, ItemRarity.Rare)
            };
        }

        private static ItemDefinition Item(string id, string title, string description, ItemEffect effect, float magnitude, bool consumable, ItemRarity rarity)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = id;
            item.Id = id;
            item.DisplayName = title;
            item.Description = description;
            item.Effect = effect;
            item.Magnitude = magnitude;
            item.Consumable = consumable;
            item.Rarity = rarity;
            return item;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ownsCatalog && itemCatalog != null)
                foreach (var item in itemCatalog)
                    if (item != null)
                    {
                        if (Application.isPlaying) Destroy(item);
                        else DestroyImmediate(item);
                    }
        }
    }
}
