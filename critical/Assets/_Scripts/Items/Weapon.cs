using UnityEngine;

public class Weapon : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private float cooldown = 0.4f;
    [SerializeField] private float activeTime = 0.35f;

    public float Damage => damage;
    public float Cooldown => cooldown;
    public float ActiveTime => activeTime;

    private GameObject owner;

    public void SetOwner(GameObject ownerObject)
    {
        owner = ownerObject;
    }

    public void BeginSwing()
    {
        gameObject.SetActive(true);
    }

    public void EndSwing()
    {
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner != null && other.transform.root == owner.transform.root)
            return;

        EnemyBase enemy = other.GetComponentInParent<EnemyBase>();

        if (enemy == null)
            return;

        enemy.TakeDamage(damage);

        Debug.Log($"Weapon acertou {enemy.name} causando {damage} de dano.");
    }
}