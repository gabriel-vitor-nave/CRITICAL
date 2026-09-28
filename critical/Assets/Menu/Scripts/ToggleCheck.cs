using UnityEngine;
using UnityEngine.EventSystems;

public class ToggleCheck : MonoBehaviour, IPointerClickHandler
{
    public GameObject check;

    private bool ativado = false;

    void Start()
    {
        // Começa com o check escondido
        check.SetActive(false);
    }

    // Detecta o clique diretamente na imagem
    public void OnPointerClick(PointerEventData eventData)
    {
        AlternarCheck();
    }

    // Ativa/desativa o check
    public void AlternarCheck()
    {
        ativado = !ativado;

        check.SetActive(ativado);
    }
}