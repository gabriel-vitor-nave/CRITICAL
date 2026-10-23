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
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if ((input.x > 0 && transform.localScale.x < 0) || (input.x < 0 && transform.localScale.x > 0))
        {
            Flip();
        }

        anim.SetFloat("horizontal", Mathf.Abs(input.x));
        anim.SetFloat("vertical", input.y);

        // velocidade pela metade se tiver stunado
        float velocidadeAtual = velocidade;
        if (Stunned)
        {
            velocidadeAtual /= 2f;
        }

        rb.velocity = input * velocidadeAtual; 
    }


    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3 (transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }

    public void Invicibility(float stunTime) 
    {
        Stunned = true;
        
        StopAllCoroutines();
        StartCoroutine(InvincibilityCounter(stunTime));

    }

    IEnumerator InvincibilityCounter(float stunTime)
    {
        yield return new WaitForSeconds(stunTime);
        Stunned = false;
    }

}
