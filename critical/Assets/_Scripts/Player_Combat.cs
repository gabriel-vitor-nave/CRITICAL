using UnityEngine;
using Critical.Feedback;
using Critical.Gameplay;
using Critical.UI;

public class Player_Combat : MonoBehaviour
{
    public int damage = 3;
    [Min(0.08f)] public float attackCooldown = 0.42f;
    [Range(0f, 1f)] public float criticalChance = 0.1f;
    [Min(0.3f)] public float reach = 1.45f;
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public float CooldownFraction => Mathf.Clamp01((nextAttack - Time.time) / Mathf.Max(0.08f, attackCooldown));
    private float nextAttack;
    private PlayerMovement movement;
    private PlayerHealth health;
    private readonly Collider2D[] hits = new Collider2D[32];
    private readonly DemoEnemy[] struck = new DemoEnemy[32];

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (GameHud.IsInputBlocked || Time.timeScale <= 0f || (health != null && health.IsDead)
            || (movement != null && !movement.enabled)) return;
        Vector2 direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (direction.sqrMagnitude > 0.01f) AimDirection = direction.normalized;
        if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Space)) TryAttack();
    }

    public bool TryAttack()
    {
        if (Time.time < nextAttack || Time.timeScale <= 0f || GameHud.IsInputBlocked
            || (health != null && health.IsDead) || (movement != null && !movement.enabled)) return false;
        nextAttack = Time.time + Mathf.Max(0.08f, attackCooldown);
        Vector2 origin = transform.position;
        var collider = GetComponent<Collider2D>();
        if (collider != null) origin = collider.bounds.center;
        WorldFeedback.Slash(origin, AimDirection, reach);
        int count = Physics2D.OverlapCircleNonAlloc(origin, reach, hits);
        int struckCount = 0;
        for (int i = 0; i < count; i++)
        {
            DemoEnemy enemy = hits[i].GetComponentInParent<DemoEnemy>();
            if (enemy == null || !enemy.IsAlive) continue;
            bool duplicate = false;
            for (int j = 0; j < struckCount; j++) if (struck[j] == enemy) duplicate = true;
            if (duplicate) continue;
            Vector2 enemyCenter = hits[i].bounds.center;
            Vector2 offset = enemyCenter - origin;
            if (offset.sqrMagnitude > 0.2f && Vector2.Dot(AimDirection, offset.normalized) < -0.1f) continue;
            if (!CombatVisibility.IsClear(origin, enemyCenter)) continue;
            struck[struckCount++] = enemy;
            bool critical = Random.value < Mathf.Clamp01(criticalChance);
            enemy.TakeDamage(Mathf.Max(1, damage) * (critical ? 2 : 1), AimDirection, critical);
        }
        for (int i = 0; i < struckCount; i++) struck[i] = null;
        return true;
    }
}
