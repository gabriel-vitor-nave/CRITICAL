using UnityEngine;
using UnityEngine.InputSystem; // Obrigatório para o CallbackContext

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float speedMultiplier = 1f;

    private Rigidbody2D rb2D;
    private Vector2 inputDirecao; 

    public void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        inputDirecao = context.ReadValue<Vector2>().normalized;
    }

    public void FixedUpdate()
    {
        rb2D.velocity = inputDirecao * (speed * speedMultiplier);
    }
}
