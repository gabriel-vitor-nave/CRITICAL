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

    [Header("Aim")]
    [SerializeField] private Transform aim;

    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private PlayerAttack playerAttack;

    public Vector2 InputDirecao { get; private set; }

    public Vector2 LastDirection => ultimaDirecao;
    private Vector2 ultimaDirecao = Vector2.down;

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        playerAttack = GetComponent<PlayerAttack>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        InputDirecao = context.ReadValue<Vector2>().normalized;

        if (InputDirecao != Vector2.zero)
        {
            ultimaDirecao = InputDirecao;

            if (aim != null)
            {
                float angle = Mathf.Atan2(ultimaDirecao.x, -ultimaDirecao.y) * Mathf.Rad2Deg;
                aim.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }

    private void Update()
    {
        AtualizarVisual();
        playerAttack?.SetAim(ultimaDirecao);
    }

    private void FixedUpdate()
    {
        rb2D.velocity = playerAttack != null && playerAttack.IsAttacking
            ? Vector2.zero
            : InputDirecao * (speed * speedMultiplier);
    }

    private void AtualizarVisual()
    {
        if (animator == null || spriteRenderer == null)
            return;

        if (playerAttack != null && playerAttack.IsAttacking)
        {
            animator.enabled = true;
            animator.SetBool("IsMoving", false);
            return;
        }

        bool estaAndando = InputDirecao != Vector2.zero;

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

        animator.SetBool("IsMoving", false);

        if (Mathf.Abs(ultimaDirecao.x) > Mathf.Abs(ultimaDirecao.y))
        {
            animator.enabled = false;
            spriteRenderer.sprite = spriteLado;
            spriteRenderer.flipX = ultimaDirecao.x < 0f;
            return;
        }

        if (ultimaDirecao.y > 0f)
        {
            animator.enabled = false;
            spriteRenderer.sprite = spriteCostas;
            spriteRenderer.flipX = false;
            return;
        }

        animator.enabled = true;
        animator.SetFloat("Horizontal", 0f);
        animator.SetFloat("Vertical", -1f);
        spriteRenderer.flipX = false;
    }
}
