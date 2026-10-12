using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    public int damage = -1;

    private void OnCollisionEnter2D(Collision2D hit) 
    {
        hit.gameObject.GetComponent<PlayerHealth>().ChangeHP(damage);
        Debug.Log("Player tomou dano");
    }
}
