using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField] private float damage = 25f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();

        if (enemy == null)
            return;

        enemy.TakeDamage(damage);

        Debug.Log($"Weapon acertou {enemy.name} causando {damage} de dano.");
    }
}