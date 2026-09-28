using UnityEngine;

public class MilkCollectible : MonoBehaviour
{
    public float velocidadeRotacao = 100f;

    void Update()
    {
        transform.Rotate(0, velocidadeRotacao * Time.deltaTime, 0);
    }

    void OnTriggerEnter(Collider outro)
    {
        if (outro.CompareTag("Player"))
        {
            GameManager gameManager = FindFirstObjectByType<GameManager>();

            gameManager.ColetarLeite();

            Destroy(gameObject);
        }
    }
}