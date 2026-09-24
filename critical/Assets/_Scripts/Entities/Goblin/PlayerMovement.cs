using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float speedMultiplier = 1f;

    [Header("Sprites de Idle")]
    [SerializeField] private Sprite spriteLado;
    [SerializeField] private Sprite spriteCostas;

    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    public Vector2 InputDirecao { get; private set; }

    // Guarda a última direção em que o personagem estava olhando.
    private Vector2 ultimaDirecao = Vector2.down;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        InputDirecao = context.ReadValue<Vector2>().normalized;

        // Só atualiza a direção quando existe movimento.
        if (InputDirecao != Vector2.zero)
        {
            ultimaDirecao = InputDirecao;
        }
    }

    private void Update()
    {
        AtualizarVisual();
    }

    private void FixedUpdate()
    {
        rb2D.velocity =
            InputDirecao * (speed * speedMultiplier);
    }

    private void AtualizarVisual()
    {
        if (animator == null || spriteRenderer == null)
            return;

        bool estaAndando = InputDirecao != Vector2.zero;

        // ==========================================
        // ANDANDO
        // ==========================================
        if (estaAndando)
        {
            animator.enabled = true;

            animator.SetBool("IsMoving", true);

            animator.SetFloat("Horizontal", InputDirecao.x);
            animator.SetFloat("Vertical", InputDirecao.y);

            if (InputDirecao.x < 0f)
                spriteRenderer.flipX = true;
            else if (InputDirecao.x > 0f)
                spriteRenderer.flipX = false;

            return;
        }

        // ==========================================
        // PARADO
        // ==========================================

        animator.SetBool("IsMoving", false);

        // --------------------------
        // PARADO PARA CIMA
        // --------------------------

        if (ultimaDirecao.y > 0f &&
            Mathf.Abs(ultimaDirecao.y) >= Mathf.Abs(ultimaDirecao.x))
        {
            animator.enabled = false;

            spriteRenderer.sprite = spriteCostas;
            spriteRenderer.flipX = false;

            return;
        }

        // --------------------------
        // PARADO PARA OS LADOS
        // --------------------------
        if (Mathf.Abs(ultimaDirecao.x) > Mathf.Abs(ultimaDirecao.y))
        {
            animator.enabled = false;

            spriteRenderer.sprite = spriteLado;

            if (ultimaDirecao.x < 0f)
                spriteRenderer.flipX = true;
            else
                spriteRenderer.flipX = false;

            return;
        }

        // --------------------------
        // PARADO PARA BAIXO
        // --------------------------

        animator.enabled = true;

        animator.SetBool("IsMoving", false);

        animator.SetFloat("Horizontal", 0f);
        animator.SetFloat("Vertical", -1f);

        spriteRenderer.flipX = false;
    }
}