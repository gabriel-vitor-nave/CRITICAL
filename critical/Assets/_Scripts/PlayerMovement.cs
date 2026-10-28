using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public float velocidade = 5f;
    public int facingDirection = 1; // 1 for right, -1 for left

    private bool Stunned;


    public Rigidbody2D rb;
    public Animator anim;

    private void Awake() => BindComponents();

    // Unity can restore a live component after a script reload without calling Awake.
    private void OnEnable() => BindComponents();

    private void Reset() => BindComponents();

    private void BindComponents()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (rb != null) return;
        enabled = false;
        Debug.LogError("PlayerMovement precisa de um Rigidbody2D no mesmo objeto.", this);
    }

    void FixedUpdate()
    {
        if (rb == null)
        {
            BindComponents();
            if (rb == null) return;
        }
        if (Critical.UI.GameHud.IsInputBlocked || Time.timeScale <= 0f)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if ((input.x > 0 && transform.localScale.x < 0) || (input.x < 0 && transform.localScale.x > 0))
        {
            Flip();
        }

        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.SetFloat("horizontal", Mathf.Abs(input.x));
            anim.SetFloat("vertical", input.y);
        }

        // velocidade pela metade se tiver stunado
        float velocidadeAtual = velocidade;
        if (Stunned)
        {
            velocidadeAtual /= 2f;
        }

        rb.velocity = input * velocidadeAtual; 
    }


    private void OnDisable()
    {
        if (rb != null) rb.velocity = Vector2.zero;
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
