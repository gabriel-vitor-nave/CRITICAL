using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    public int damage = 1;
    public Transform attackPoint;
    public float weaponRange = 0.5f;
    public float stunTime = 0.5f;
    public LayerMask playerLayer;

    public void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);
        foreach (Collider2D hit in hits)
        {
            hit.GetComponentInParent<PlayerHealth>().ChangeHP(-damage);
            hit.GetComponentInParent<PlayerMovement>().Invicibility(stunTime);
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, weaponRange);
    }

}
