using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    public int damage = 1;

    private void OnCollisionEnter2D(Collision2D hit) 
    {
        if (hit.gameObject.CompareTag("Player"))
        {
            hit.gameObject.GetComponent<PlayerHealth>().ChangeHP(-damage);
        }
    }

    public void Attack()
    {
        Debug.Log("Enemy attacked player");
    }
}
