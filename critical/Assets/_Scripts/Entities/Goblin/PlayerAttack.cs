using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private GameObject melee;
    [SerializeField] private float attackTime = 0.35f;
    [SerializeField] private float attackCooldown = 0.4f;

    public bool IsAttacking { get; private set; }
    public bool CanAttack => cooldownTimer <= 0f;

    private float attackTimer;
    private float cooldownTimer;
    private Animator animator;
    private Weapon weaponComponent;
    private PlayerMovement movement;

    public void SetAim(Vector2 direction)
    {
        if (melee != null && direction.sqrMagnitude > 0.001f)
            melee.transform.parent.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg);
    }

    private void Awake()
    {
        if (melee != null)
            melee.SetActive(false);
        animator = GetComponentInChildren<Animator>();
        weaponComponent = melee != null ? melee.GetComponent<Weapon>() : null;
        movement = GetComponent<PlayerMovement>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || !CanAttack)
            return;

        IsAttacking = true;
        attackTimer = attackTime;
        cooldownTimer = attackCooldown;

        Vector2 attackDirection = movement != null ? movement.LastDirection : Vector2.down;
        int attackDirectionId = Mathf.Abs(attackDirection.x) > Mathf.Abs(attackDirection.y)
            ? 1
            : attackDirection.y > 0f ? 2 : 0;
        animator?.SetInteger("AttackDirection", attackDirectionId);
        animator?.SetFloat("Horizontal", attackDirection.x);
        animator?.SetFloat("Vertical", attackDirection.y);
        animator?.SetBool("IsMoving", false);

        if (melee != null)
            melee.SetActive(true);
        weaponComponent?.BeginSwing();
        animator?.SetTrigger("Attack");
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!IsAttacking)
            return;

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            IsAttacking = false;

            if (melee != null)
            {
                weaponComponent?.EndSwing();
                melee.SetActive(false);
            }
        }
    }
}
