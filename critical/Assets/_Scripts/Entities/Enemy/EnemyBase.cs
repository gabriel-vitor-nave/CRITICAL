using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0f;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        Debug.Log($"[Enemy] {name} nasceu com vida {CurrentHealth:0.##}/{MaxHealth:0.##}", this);
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentHealth <= 0f)
            return;

        float appliedDamage = Mathf.Min(damage, CurrentHealth);
        CurrentHealth -= appliedDamage;
        Debug.Log($"[Damage] {name} recebeu {appliedDamage:0.##} de dano. Vida: {CurrentHealth:0.##}/{MaxHealth:0.##}", this);

        if (CurrentHealth <= 0f)
        {
            CurrentHealth = 0f;
            Debug.Log($"[Death] {name} morreu.", this);
            Destroy(gameObject);
        }
    }
}
