using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Critical.Progression.Editor
{
    public static class ProgressionChecks
    {
        [MenuItem("Tools/CRITICAL/Run Progression Checks")]
        public static void Run()
        {
            if (Application.isPlaying || RunProgression.Instance != null)
                throw new InvalidOperationException("Execute as verificações de progressão em Edit Mode, fora de uma run.");
            var root = new GameObject("Progression check (temporary)");
            root.SetActive(false);
            var temporaryItems = new List<ItemDefinition>();
            RunProgression run = null;
            try
            {
                var health = root.AddComponent<PlayerHealth>();
                health.maxHP = 10;
                health.HP = 5;
                root.AddComponent<PlayerMovement>();
                var combat = root.AddComponent<Player_Combat>();
                run = root.AddComponent<RunProgression>();
                typeof(RunProgression).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
                Require(run.Level == 1 && run.Inventory.Count == 1 && run.Inventory[0].Count == 2, "Starter potion stack.");
                int startingDamage = combat.damage;
                Require(run.AppliedUpgrades.Count == 0 && run.EquippedWeapon == run.Catalog[2] && combat.damage == startingDamage,
                    "New runs have no chosen upgrades; displaying the starting weapon does not apply a passive bonus.");
                Require(run.SelectedItemIndex == 0 && run.SelectedItem == run.Inventory[0], "The starter consumable is selected.");
                run.CycleSelectedItem(int.MaxValue);
                Require(run.SelectedItemIndex == 0 && !run.SelectItem(-1) && !run.SelectItem(1), "Single-item cycling and invalid selection are safe.");
                Require(run.UseSelectedItem() && health.HP == 10 && run.SelectedItem.Count == 1, "The selected consumable heals and spends one unit.");
                health.HP = 5;
                Require(run.UseSelectedItem() && run.Inventory.Count == 0 && run.SelectedItemIndex == -1 && run.SelectedItem == null,
                    "Consuming the final item clears the selection.");
                run.CycleSelectedItem(int.MinValue);
                Require(!run.SelectItem(0) && !run.UseSelectedItem() && !run.DropSelectedItem(), "An empty carousel cannot select, consume or drop.");
                Require(run.AddItem(run.Catalog[0], 2) && run.SelectedItemIndex == 0, "Collecting into an empty inventory selects its first stack.");
                health.HP = 5;

                int levelEvents = 0;
                run.LevelGained += _ => levelEvents++;
                run.GrantExperience(135);
                Require(run.Level == 3 && run.Experience == 35 && run.PendingUpgrades == 2 && levelEvents == 2, "XP overflow grants both levels and preserves remainder.");
                Require(run.GetUpgradeChoices().Count == 3, "Three stable upgrade choices.");
                var firstChoice = run.GetUpgradeChoices()[0];
                Require(ReferenceEquals(firstChoice, run.GetUpgradeChoices()[0]), "Opening upgrades does not reroll.");
                Require(!run.ChooseUpgrade(-1) && !run.ChooseUpgrade(3), "Invalid upgrades are rejected.");
                Require(run.AppliedUpgrades.Count == 0, "Rejected choices never create upgrade history.");
                Require(run.ChooseUpgrade(0) && run.PendingUpgrades == 1 && run.GetUpgradeChoices().Count == 3, "One choice spends exactly one level reward.");
                Require(run.AppliedUpgrades.Count == 1 && !ReferenceEquals(run.AppliedUpgrades[0], firstChoice)
                    && run.AppliedUpgrades[0].Id == firstChoice.Id && run.AppliedUpgrades[0].Magnitude == firstChoice.Magnitude,
                    "One successful choice records a separate snapshot of the selected upgrade.");
                string chosenName = run.AppliedUpgrades[0].DisplayName;
                firstChoice.DisplayName = "Changed old choice";
                Require(run.AppliedUpgrades[0].DisplayName == chosenName, "Replacing or changing an old choice cannot alter earned history.");
                Require(run.ChooseUpgrade(0) && run.PendingUpgrades == 0 && run.AppliedUpgrades.Count == 2,
                    "Queued level rewards create exactly one history entry per selection.");
                Require(!run.ChooseUpgrade(0) && run.AppliedUpgrades.Count == 2, "An extra submit cannot duplicate an already-spent reward.");

                int damageBeforeReload = combat.damage;
                typeof(RunProgression).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
                Require(run.Level == 3 && run.Experience == 35 && run.Inventory[0].Count == 2 && run.AppliedUpgrades.Count == 2
                    && combat.damage == damageBeforeReload, "Rebinding after a script reload preserves run state without reapplying effects or starter items.");

                int potionCount = run.Inventory[0].Count;
                Require(run.UseItem(0) && health.HP == 10 && run.Inventory[0].Count == potionCount - 1, "Potion heals with clamp and consumes one.");
                Require(!run.UseItem(0) && run.Inventory[0].Count == potionCount - 1, "Full health never wastes a potion.");
                int damage = combat.damage;
                Require(run.AddItem(run.Catalog[2]) && combat.damage == damage + 1, "Collecting a passive applies it once.");
                Require(run.EquippedWeapon == run.Catalog[2], "The acquired damage relic is the weapon shown by the HUD.");
                Require(run.SelectItem(1), "Carousel selects the newly collected passive.");
                run.CycleSelectedItem(1);
                Require(run.SelectedItemIndex == 0, "Carousel wraps forward.");
                run.CycleSelectedItem(-1);
                Require(run.SelectedItemIndex == 1 && run.EquippedWeapon == run.Catalog[2], "Carousel wraps backward without changing the fixed weapon.");
                Require(!run.DropItem(-1) && !run.DropItem(1, 0) && !run.DropItem(1, 2) && !run.DropItem(1)
                    && run.Inventory[1].Count == 1 && combat.damage == damage + 1, "Invalid drops preserve counts and passive stats.");
                Require(run.AddItem(run.Catalog[2]) && combat.damage == damage + 2 && run.Inventory[1].Count == 2, "Passive stacking applies only the acquired delta.");
                Require(!run.UseItem(1) && combat.damage == damage + 2, "Equipped passive cannot be reapplied by clicking.");
                health.HP = 5;
                Require(run.SelectItem(0) && run.UseSelectedItem() && run.Inventory[0].Item.Id == "broken-staff" && run.Inventory[0].Count == 2
                    && run.SelectedItemIndex == 0 && run.SelectedItem == run.Inventory[0], "Consuming the final potion shifts inventory without losing a passive stack or selection.");
                Require(!run.UseItem(0) && combat.damage == damage + 2, "Shifted indices cannot consume or reapply a passive.");

                var restoredHealthItem = ScriptableObject.CreateInstance<ItemDefinition>();
                restoredHealthItem.Id = "dropped-health-check";
                restoredHealthItem.Effect = ItemEffect.MaxHealth;
                restoredHealthItem.Magnitude = 4;
                temporaryItems.Add(restoredHealthItem);
                health.HP = 3;
                int maximumBeforeRestore = health.maxHP;
                Require(run.AddItem(restoredHealthItem, restoreDroppedItem: true) && health.maxHP == maximumBeforeRestore + 4 && health.HP == 3,
                    "Recollecting a dropped maximum-health passive restores its cap without healing.");

                run.AddGold(int.MaxValue);
                run.AddGold(1);
                Require(run.Gold == int.MaxValue, "Gold saturates without integer overflow.");
                int changedCalls = 0;
                run.Changed += () => { changedCalls++; run.AddGold(50); run.GrantExperience(100); };
                int xp = run.Experience;
                run.AddItem(run.Catalog[1]);
                Require(changedCalls == 1 && run.Experience == xp, "Notification listeners cannot recursively mutate progression.");

                Require(!run.AddItem(null) && !run.AddItem(run.Catalog[0], -1) && !run.AddItem(run.Catalog[0], 100), "Invalid item mutations cannot lose or overflow stacks.");
                while (run.Inventory.Count < RunProgression.InventoryCapacity)
                {
                    var item = ScriptableObject.CreateInstance<ItemDefinition>();
                    item.Id = "test-" + run.Inventory.Count;
                    item.DisplayName = item.Id;
                    item.Consumable = true;
                    temporaryItems.Add(item);
                    Require(run.AddItem(item), "Inventory accepts remaining slots.");
                }
                Require(!run.AddItem(run.Catalog[4]) && run.Inventory.Count == RunProgression.InventoryCapacity, "Full inventory rejects new stacks.");
                run.GrantExperience(int.MaxValue);
                Require(run.Level == RunProgression.MaximumLevel && run.Experience == 0 && run.ExperienceToNextLevel == 0, "Huge XP safely reaches the level cap.");
                Debug.Log("CRITICAL: progression checks passed (XP, queued upgrades, potion consumption, passives, capacity, overflow and notification reentrancy).");
            }
            finally
            {
                // Inactive Edit Mode objects do not receive the usual lifecycle callbacks.
                if (run != null) typeof(RunProgression).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
                UnityEngine.Object.DestroyImmediate(root);
                foreach (var item in temporaryItems) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Progression check failed: " + message);
        }
    }
}
