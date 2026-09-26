using UnityEngine;

public enum WeaponAttackType { Melee, Magic }

[CreateAssetMenu(menuName = "Critical/Combat/Weapon Definition", fileName = "Weapon_")]
public class WeaponDefinition : ScriptableObject
{
    public string displayName = "Weapon";
    public WeaponAttackType attackType = WeaponAttackType.Melee;
    [Min(0f)] public float damage = 25f;
    [Min(0.01f)] public float activeTime = 0.16f;
    [Min(0f)] public float cooldown = 0.34f;
    [Min(0.01f)] public float damageInterval = 0.2f;
    [Min(1)] public int maxTargetsPerSwing = 1;
}
