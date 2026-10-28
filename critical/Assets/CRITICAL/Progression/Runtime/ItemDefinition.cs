using System;
using UnityEngine;

namespace Critical.Progression
{
    public enum ItemRarity { Common, Rare, Epic, Divine }
    public enum ItemEffect { Heal, Damage, MoveSpeed, MaxHealth, CriticalChance, AttackSpeed }

    [CreateAssetMenu(menuName = "CRITICAL/Item", fileName = "Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public ItemRarity Rarity;
        public bool Consumable;
        public ItemEffect Effect;
        [Min(0f)] public float Magnitude = 1f;
    }

    [Serializable]
    public sealed class ItemStack
    {
        public ItemDefinition Item;
        public int Count;

        public ItemStack(ItemDefinition item, int count) { Item = item; Count = count; }
    }

    [Serializable]
    public sealed class UpgradeDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public ItemEffect Effect;
        public float Magnitude;
    }
}
