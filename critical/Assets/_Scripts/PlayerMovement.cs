using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float velocidade = 5f;
    public int facingDirection = 1; // 1 for right, -1 for left

    private bool Stunned;


    public Rigidbody2D rb;
    public Animator anim;

    void FixedUpdate()
    {
        if (Stunned != false) return;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if ((input.x > 0 && transform.localScale.x < 0) ||
            (input.x < 0 && transform.localScale.x > 0))
        {
            Flip();
        }

        anim.SetFloat("horizontal", Mathf.Abs(input.x));
        anim.SetFloat("vertical", input.y);

        rb.velocity = input * velocidade;
    }

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3 (transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    public void Knockback(Transform enemy, float force, float stunTime)
    {
        Stunned = true;
        Vector2 direction = transform.position - enemy.position;
        rb.velocity = direction.normalized * force;
        StartCoroutine(KnockbackCounter(stunTime));
    }

    IEnumerator KnockbackCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        rb.velocity = Vector2.zero;
        Stunned = false;
    }
}
