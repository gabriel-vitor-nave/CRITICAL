using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float speedMultiplier = 1f;

    private Rigidbody2D rb2D;
    private SpriteRenderer spriteRenderer; 
    private Animator animator; 

    private Vector2 inputDirecao; 

    public void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        
        // Busca os componentes no objeto filho "Character"
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(); 
        animator = GetComponentInChildren<Animator>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        inputDirecao = context.ReadValue<Vector2>().normalized;

        // Controle de Flip do SpriteRenderer
        if (spriteRenderer != null)
        {
            if (inputDirecao.x < 0f) spriteRenderer.flipX = true;
            else if (inputDirecao.x > 0f) spriteRenderer.flipX = false;
        }
    }

    public void Update()
    {
        // Atualiza os parâmetros do Animator a cada frame
        UpdateAnimationParameters();
    }

    public void FixedUpdate()
    {
        rb2D.velocity = inputDirecao * (speed * speedMultiplier);
    }

    private void UpdateAnimationParameters()
    {
        if (animator == null) return;

        // Verifica se o jogador está aplicando movimento (se a direção não é zero)
        bool isMoving = inputDirecao != Vector2.zero;

        // Define o booleano IsMoving no seu Animator
        animator.SetBool("IsMoving", isMoving);

        // Atualiza a direção da Blend Tree APENAS se estiver se movendo
        // Isso trava a última direção olhada quando o jogador fica parado (Idle)
        if (isMoving)
        {
            animator.SetFloat("Horizontal", inputDirecao.x);
            animator.SetFloat("Vertical", inputDirecao.y);
        }
    }
}
