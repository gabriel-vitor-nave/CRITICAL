using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 2.5f;

    [Header("Combat Settings")]
    public float attackDistance = 2f;
    public float attackCooldown = 1f; //segundos

    [Header("Detection Settings")]
    public float playerDetectRange = 5f;
    public Transform detectionPoint;
    public LayerMask playerLayer;

    private float attackCooldownTimer;
    private int facingDirection = 1; // 1 for right, -1 for left
    private EnemyState enemyState;

    private Rigidbody2D rb;
    private Transform player;
    private Animator anim;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // por padrão: idle
        enemyState = EnemyState.Idle;

        anim.SetBool("IsIdle", true);
        anim.SetBool("IsChasing", false);
        anim.SetBool("IsAttacking", false);
    }

    void Update()
    {
        CheckForPlayer();

        // clock do cd do ataque pra ir diminuindo com o tempo
        if (attackCooldownTimer > 0)
            attackCooldownTimer = Mathf.Max(0f, attackCooldownTimer - Time.deltaTime);

        if (enemyState == EnemyState.Chasing)
            Chase();
        else
        // se n tiver perseguindo, fica paradilson
            rb.velocity = Vector2.zero;
    }

    void Chase()
    {
        if (enemyState != EnemyState.Chasing) { return; }
        
        if ((player.position.x > transform.position.x && transform.localScale.x > 0) ||
            (player.position.x < transform.position.x && transform.localScale.x < 0))
                Flip();

        // exata direção do player
        Vector2 direction = (player.position - transform.position).normalized;
        rb.velocity = direction * speed;
    }

    private void CheckForPlayer()
    {
        //hitbox pra ver se o player ta lá
        Collider2D[] hits = Physics2D.OverlapCircleAll(detectionPoint.position, playerDetectRange, playerLayer);

        // se tiver alguém no hits, q com ctz vai ser o player pq ta na layer dele, pega a posição do player e vê se ta dentro da distância de ataque
        if (hits.Length > 0)
        {
            player = hits[0].transform;
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // se o player estiver longe demais, vai perseguir ele
            if (distanceToPlayer > attackDistance && enemyState != EnemyState.Attacking)
            {
                ChangeState(EnemyState.Chasing);
                return;
            }

            rb.velocity = Vector2.zero;
            if (enemyState == EnemyState.Attacking)
            {
                return;
            }

            // reseta o cd do ataque e muda o estado pra atacando, se n tiver no cd, se tiver, muda pro idle
            if (attackCooldownTimer <= 0f)
            {
                attackCooldownTimer = attackCooldown;
                ChangeState(EnemyState.Attacking);
            }
            else
            {
                ChangeState(EnemyState.Idle);
            }
        }
        else
        {
            player = null;
            rb.velocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
        }
    }

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }
    void ChangeState(EnemyState newState)
    {
        if (enemyState == newState)
        {
            return;
        }

        if (enemyState == EnemyState.Idle)
            anim.SetBool("IsIdle", false);
        else if (enemyState == EnemyState.Chasing)
            anim.SetBool("IsChasing", false);
        else if (enemyState == EnemyState.Attacking)
            anim.SetBool("IsAttacking", false);

        enemyState = newState;

        if (enemyState == EnemyState.Idle)
            anim.SetBool("IsIdle", true);
        else if (enemyState == EnemyState.Chasing)
            anim.SetBool("IsChasing", true);
        else if (enemyState == EnemyState.Attacking)
            anim.SetBool("IsAttacking", true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(detectionPoint.position, playerDetectRange);
    }
}

public enum EnemyState
{
    Idle,
    Chasing,
    Attacking
}