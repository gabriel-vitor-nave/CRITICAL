using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int leitesColetados = 0;

    public void ColetarLeite()
    {
        leitesColetados++;

        Debug.Log("Leites: " + leitesColetados);
    }
}
