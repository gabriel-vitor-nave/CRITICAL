using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class RatAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float detectionRadius = 5f;
    [SerializeField] private float attackRadius = 0.75f;
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float idleWanderRadius = 1.5f;
    [SerializeField] private float wanderInterval = 2.5f;
    [SerializeField] private CombatController combat;
    [SerializeField] private Animator animator;
    private Rigidbody2D body;
    private Vector2 spawnPoint;
    private Vector2 wanderTarget;
    private float wanderTimer;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (combat == null) combat = GetComponent<CombatController>();
        if (animator == null)
        {
            foreach (Animator candidate in GetComponentsInChildren<Animator>(true))
            {
                if (candidate.GetComponent<SpriteRenderer>() != null)
                {
                    animator = candidate;
                    break;
                }
            }
        }
        spawnPoint = transform.position;
        wanderTarget = spawnPoint;
    }

    private void Update()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (combat != null && combat.IsAttacking)
        {
            body.velocity = Vector2.zero;
            if (animator != null) animator.SetBool("IsMoving", false);
            return;
        }

        Vector2 destination = spawnPoint;
        bool chasing = target != null && Vector2.Distance(transform.position, target.position) <= detectionRadius;
        if (chasing) destination = target.position;
        else
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = wanderInterval;
                wanderTarget = spawnPoint + Random.insideUnitCircle * idleWanderRadius;
            }
            destination = wanderTarget;
        }

        Vector2 toTarget = destination - (Vector2)transform.position;
        bool shouldMove = toTarget.sqrMagnitude > 0.05f && (!chasing || Vector2.Distance(transform.position, target.position) > attackRadius);
        Vector2 direction = shouldMove ? toTarget.normalized : Vector2.zero;

        if (chasing && Vector2.Distance(transform.position, target.position) <= attackRadius)
        {
            direction = ((Vector2)target.position - body.position).normalized;
            body.velocity = Vector2.zero;
            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                animator.SetFloat("Horizontal", direction.x);
                animator.SetFloat("Vertical", direction.y);
            }
            combat?.SetAim(direction);
            combat?.TryAttack();
            return;
        }

        body.velocity = direction * moveSpeed;
        if (animator != null)
        {
            animator.SetBool("IsMoving", direction.sqrMagnitude > 0.001f);
            animator.SetFloat("Horizontal", direction.x);
            animator.SetFloat("Vertical", direction.y);
        }
    }

    private void OnDisable() => body.velocity = Vector2.zero;
}
