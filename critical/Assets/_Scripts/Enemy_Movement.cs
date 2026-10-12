using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Movement : MonoBehaviour
{
    public float speed = 2.5f;
    public float attackRange = 2f;
    private int facingDirection = 1; // 1 for right, -1 for left
    
    private EnemyState enemyState;
    private Rigidbody2D rb;
    private Transform player;
    private Animator anim;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        ChangeState(EnemyState.Idle);
    }

    void Update()
    {
        if(enemyState == EnemyState.Chasing)
        {
            Chase();
        } else if(enemyState == EnemyState.Attacking)
        {
            rb.velocity = Vector2.zero;
        }
    }

    void Chase()
    {
        if(Vector2.Distance(transform.position, player.position) <= attackRange)
        {
            
            ChangeState(EnemyState.Attacking);
        }
        else if (enemyState == EnemyState.Chasing)
        {
            if ((player.position.x > transform.position.x && transform.localScale.x > 0) ||
                (player.position.x < transform.position.x && transform.localScale.x < 0))
            {
                Flip();
            }
            Vector2 direction = (player.position - transform.position).normalized;
            rb.velocity = direction * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D hit)
    {
        if (hit.CompareTag("Player"))
        {
            if (player == null)
            {
                player = hit.transform;
            }
            // chasing = true
            ChangeState(EnemyState.Chasing);
        }
    }

    private void OnTriggerExit2D(Collider2D hit)
    {
        if (hit.CompareTag("Player"))
        {
            rb.velocity = Vector2.zero;
            // chasing = false
            ChangeState(EnemyState.Idle);
        }
    }

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3 (transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    void ChangeState(EnemyState newState)
    {
        // sair da animação
        if(enemyState == EnemyState.Idle)
            anim.SetBool("IsIdle", false);
        else if (enemyState == EnemyState.Chasing)
            anim.SetBool("IsChasing", false);
        else if (enemyState == EnemyState.Attacking)
            anim.SetBool("IsAttacking", false);

        enemyState = newState;

        // entrar na animação
        if(enemyState == EnemyState.Idle)
            anim.SetBool("IsIdle", true);
        else if (enemyState == EnemyState.Chasing)
            anim.SetBool("IsChasing", true);
        else if (enemyState == EnemyState.Attacking)
            anim.SetBool("IsAttacking", true);
    }
}

public enum EnemyState
{
    Idle,
    Chasing,
    Attacking
}