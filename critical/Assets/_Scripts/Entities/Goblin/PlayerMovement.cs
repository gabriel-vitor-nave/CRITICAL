using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 5f;
    [SerializeField] private float speedMultiplier = 1f;

    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    public Vector2 InputDirecao { get; private set; }

    private void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        InputDirecao = context.ReadValue<Vector2>().normalized;

        if (spriteRenderer != null)
        {
            if (InputDirecao.x < 0f)
                spriteRenderer.flipX = true;
            else if (InputDirecao.x > 0f)
                spriteRenderer.flipX = false;
        }
    }

    private void Update()
    {
        UpdateAnimationParameters();
    }

    private void FixedUpdate()
    {
        rb2D.velocity =
            InputDirecao * (speed * speedMultiplier);
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null)
            return;

        bool isMoving = InputDirecao != Vector2.zero;

        animator.SetBool("IsMoving", isMoving);

        if (isMoving)
        {
            animator.SetFloat("Horizontal", InputDirecao.x);
            animator.SetFloat("Vertical", InputDirecao.y);
        }
    }
}