using UnityEngine;

public class CombatController : MonoBehaviour
{
    [SerializeField] private Weapon weapon;
    [SerializeField] private Transform aim;
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTrigger = "Attack";

    public bool IsAttacking { get; private set; }

    public bool CanAttack =>
        weapon != null &&
        cooldown <= 0f &&
        !IsAttacking;

    private float cooldown;
    private float activeTimer;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>(true);

        if (weapon != null)
        {
            weapon.SetOwner(gameObject);
            weapon.EndSwing();
        }
    }

    public bool TryAttack()
    {
        if (!CanAttack)
            return false;

        IsAttacking = true;

        cooldown = weapon.Cooldown;
        activeTimer = weapon.ActiveTime;

        weapon.BeginSwing();

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);

        return true;
    }

    private void Update()
    {
        if (cooldown > 0f)
            cooldown -= Time.deltaTime;

        if (!IsAttacking)
            return;

        activeTimer -= Time.deltaTime;

        if (activeTimer <= 0f)
        {
            IsAttacking = false;
            weapon.EndSwing();
        }
    }

    public void SetAim(Vector2 direction)
    {
        if (aim != null && direction.sqrMagnitude > 0.001f)
        {
            aim.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg
            );
        }
    }
}