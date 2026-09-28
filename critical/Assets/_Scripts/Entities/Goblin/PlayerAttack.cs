using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    public bool IsAttacking => combatController != null && combatController.IsAttacking;

    private CombatController combatController;
    private PlayerMovement movement;

    private void Awake()
    {
        combatController = GetComponent<CombatController>();
        movement = GetComponent<PlayerMovement>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        combatController.TryAttack();
    }

    public void SetAim(Vector2 direction)
    {
        combatController?.SetAim(direction);
    }
}