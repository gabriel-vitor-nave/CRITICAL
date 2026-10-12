using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float velocidade = 5f;
    public int facingDirection = 1; // 1 for right, -1 for left


    public Rigidbody2D rb;
    public Animator anim;

    void FixedUpdate()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        if(horizontalInput > 0 && transform.localScale.x < 0 || horizontalInput < 0 && transform.localScale.x > 0)
        {
            Flip();
        }

        anim.SetFloat("horizontal", Mathf.Abs(horizontalInput));
        anim.SetFloat("vertical", verticalInput);

        rb.velocity = new Vector2(horizontalInput * velocidade, verticalInput * velocidade);
    }

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3 (transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }
}
